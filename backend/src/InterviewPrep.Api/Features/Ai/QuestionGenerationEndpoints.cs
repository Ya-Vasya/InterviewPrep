using InterviewPrep.Api.Common;
using InterviewPrep.Api.Data;
using InterviewPrep.Api.Features.Questions;
using InterviewPrep.Api.Features.Study;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InterviewPrep.Api.Features.Ai;

/// <summary>Generating Drafts and accepting them as questions (R3 3a).</summary>
public static class QuestionGenerationEndpoints
{
    private const int MaxAcceptedPerRequest = GenerateQuestionsLimits.MaxCount;

    public static RouteGroupBuilder MapQuestionGenerationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/questions/generate", GenerateQuestions).WithName("GenerateQuestions");
        group.MapPost("/questions/accept", AcceptDrafts).WithName("AcceptQuestionDrafts");

        return group;
    }

    /// <summary>One counted provider call. Nothing is stored; the user reviews the Drafts and accepts them separately.</summary>
    private static async Task<Results<Ok<GenerateQuestionsResponse>, ValidationProblem, ProblemHttpResult>> GenerateQuestions(
        GenerateQuestionsRequest request, string? tz,
        AppDbContext db, AiGateway gateway, TimeProvider time, IOptions<AiOptions> options, HttpContext http,
        CancellationToken ct)
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

        if (!StudyDay.TryGetZone(tz, out var zone))
            errors.Add("tz", "Unknown time zone.");

        if (errors.Any)
            return errors.ToProblem();

        // Duplicates are checked against every question of the topic; only the prompt is limited to 200 texts.
        var existing = await db.Questions
            .Where(q => q.Topic == topic)
            .Select(q => new { q.Text, q.Difficulty })
            .ToListAsync(ct);

        var promptTexts = existing
            .OrderBy(q => q.Difficulty == difficulty ? 0 : 1).ThenBy(q => q.Text, StringComparer.Ordinal)
            .Take(GenerateQuestionsLimits.MaxExistingTexts)
            .Select(q => q.Text);

        var prompt = GenerateQuestionsPrompt.Build(topic, difficulty, count, tag, focus, promptTexts) with
        {
            MaxOutputTokens = options.Value.GenerateOutputTokensFor(count)
        };

        var outcome = await gateway.RunAsync(
            prompt, GenerateQuestionsPrompt.Parse, StudyDay.StartOfDayUtc(time.GetUtcNow().UtcDateTime, zone), ct);

        if (!outcome.Succeeded)
            return outcome.ToProblem(http);

        var seen = existing.Select(q => QuestionTexts.Normalize(q.Text)).ToHashSet();
        var drafts = new List<DraftResponse>();
        var dropped = 0;

        foreach (var item in outcome.Value!.Items)
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

        return TypedResults.Ok(new GenerateQuestionsResponse(drafts, dropped));
    }

    /// <summary>
    /// Saves the valid Drafts as AI-sourced questions and reports the others by position, so one bad Draft does not
    /// lose the rest. A Draft whose text already exists (also from an earlier call or the same request) is rejected,
    /// which makes a double submit harmless.
    /// </summary>
    private static async Task<Results<Ok<AcceptDraftsResponse>, ValidationProblem>> AcceptDrafts(
        AcceptDraftsRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = new ValidationErrors();

        if (request.Drafts is null or { Count: 0 })
            errors.Add("drafts", "At least one draft is required.");
        else if (request.Drafts.Count > MaxAcceptedPerRequest)
            errors.Add("drafts", $"At most {MaxAcceptedPerRequest} drafts can be accepted at once.");

        if (errors.Any)
            return errors.ToProblem();

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
        var existing = await db.Questions
            .Where(q => topics.Contains(q.Topic))
            .Select(q => new { q.Topic, q.Text })
            .ToListAsync(ct);
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
            db.Questions.AddRange(saved);
            await db.SaveChangesAsync(ct);
        }

        return TypedResults.Ok(new AcceptDraftsResponse(
            saved.Select(q => q.ToResponse()).ToList(),
            rejected.OrderBy(r => r.Index).ToList()));
    }
}
