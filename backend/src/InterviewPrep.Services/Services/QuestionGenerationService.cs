using InterviewPrep.Data.Models;
using InterviewPrep.Data.Models.Enums;
using InterviewPrep.Data.Repositories;
using InterviewPrep.Infrastructure.Options;
using InterviewPrep.Services.Dtos;
using InterviewPrep.Services.Helpers;
using InterviewPrep.Services.Prompts;
using InterviewPrep.Services.Rules;
using Microsoft.Extensions.Options;

namespace InterviewPrep.Services;

public sealed class QuestionGenerationService(
    IQuestionRepository questions,
    IAiUsageService ai,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    IOptions<AiOptions> options) : IQuestionGenerationService
{
    private const int MaxAcceptedPerRequest = GenerateQuestionsLimits.MaxCount;

    public async Task<GenerateQuestionsResponse> GenerateAsync(
        GenerateQuestionsRequest request, string? timeZone, CancellationToken ct)
    {
        var errors = new ValidationErrors();

        errors.TryParseRequired<Topic>("topic", request.Topic, out var topic);
        errors.TryParseRequired<Difficulty>("difficulty", request.Difficulty, out var difficulty);

        var count = request.Count ?? GenerateQuestionsLimits.DefaultCount;
        if (count is < GenerateQuestionsLimits.MinCount or > GenerateQuestionsLimits.MaxCount)
        {
            errors.Add("count",
                $"Count must be between {GenerateQuestionsLimits.MinCount} and {GenerateQuestionsLimits.MaxCount}.");
        }

        var tag = QuestionTags.Normalize(request.Tag);
        if (tag.Length > QuestionLimits.TagMaxLength)
            errors.Add("tag", $"Tag must be at most {QuestionLimits.TagMaxLength} characters.");

        var focus = request.Focus?.Trim() ?? "";
        if (focus.Length > GenerateQuestionsLimits.MaxFocusLength)
            errors.Add("focus", $"Focus must be at most {GenerateQuestionsLimits.MaxFocusLength} characters.");

        var zone = errors.RequireZone(timeZone);
        errors.ThrowIfAny();

        // Duplicates are checked against every question of the topic; only the prompt is limited in size.
        var existing = await questions.GetByTopicsAsync([topic], ct);

        var promptTexts = existing
            .OrderBy(q => q.Difficulty == difficulty ? 0 : 1).ThenBy(q => q.Text, StringComparer.Ordinal)
            .Take(options.Value.MaxExistingQuestionTexts)
            .Select(q => q.Text);

        var prompt = GenerateQuestionsPrompt.Build(topic, difficulty, count, tag, focus, promptTexts) with
        {
            MaxOutputTokens = options.Value.GenerateOutputTokensFor(count)
        };

        var generated = await ai.RunAsync(
            prompt, GenerateQuestionsPrompt.Parse, StudyDay.StartOfDayUtc(time.GetUtcNow().UtcDateTime, zone), ct);

        var seen = existing.Select(q => QuestionTexts.Normalize(q.Text)).ToHashSet();
        var drafts = new List<DraftResponse>();
        var dropped = 0;

        foreach (var item in generated.Items)
        {
            if (drafts.Count == count)
                break;

            if (!seen.Add(QuestionTexts.Normalize(item.Text)))
            {
                dropped++;
                continue;
            }

            // The requested tag always comes first, so the Drafts of a run can be found by it.
            var tags = QuestionTags.NormalizeAll(tag.Length > 0 ? [tag, .. item.Tags] : item.Tags)
                .Take(QuestionLimits.MaxTags)
                .ToList();

            drafts.Add(new DraftResponse(topic, difficulty, item.Text, item.Answer, tags));
        }

        return new GenerateQuestionsResponse(drafts, dropped);
    }

    public async Task<AcceptDraftsResponse> AcceptAsync(AcceptDraftsRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();

        if (request.Drafts is null or { Count: 0 })
            errors.Add("drafts", "At least one draft is required.");
        else if (request.Drafts.Count > MaxAcceptedPerRequest)
            errors.Add("drafts", $"At most {MaxAcceptedPerRequest} drafts can be accepted at once.");

        errors.ThrowIfAny();

        var rejected = new List<RejectedDraft>();
        var valid = new List<(int Index, ValidQuestion Question)>();

        for (var i = 0; i < request.Drafts!.Count; i++)
        {
            var draftErrors = new ValidationErrors();
            if (request.Drafts[i] is not { } draft)
                draftErrors.Add("draft", "Draft is required.");
            else if (QuestionValidator.Validate(draft, draftErrors) is { } question)
            {
                valid.Add((i, question));
                continue;
            }

            rejected.Add(new RejectedDraft(i, draftErrors.Errors));
        }

        var topics = valid.Select(v => v.Question.Topic).Distinct().ToList();
        var existing = await questions.GetByTopicsAsync(topics, ct);
        var seen = existing.Select(q => (q.Topic, QuestionTexts.Normalize(q.Text))).ToHashSet();

        var saved = new List<Question>();
        foreach (var (index, q) in valid)
        {
            if (!seen.Add((q.Topic, QuestionTexts.Normalize(q.Text))))
            {
                rejected.Add(new RejectedDraft(index, new Dictionary<string, string[]>
                {
                    ["text"] = ["A question with this text already exists."]
                }));
                continue;
            }

            saved.Add(new Question
            {
                Topic = q.Topic,
                Difficulty = q.Difficulty,
                Text = q.Text,
                Answer = q.Answer,
                Tags = q.Tags,
                Source = QuestionSource.Ai
            });
        }

        if (saved.Count > 0)
        {
            questions.AddRange(saved);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return new AcceptDraftsResponse(
            saved.Select(q => q.ToResponse()).ToList(),
            rejected.OrderBy(r => r.Index).ToList());
    }
}
