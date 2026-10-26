using InterviewPrep.Data.Models;
using InterviewPrep.Data.Models.Enums;
using InterviewPrep.Data.Repositories;
using InterviewPrep.Infrastructure.Options;
using InterviewPrep.Services.Dtos;
using InterviewPrep.Services.Exceptions;
using InterviewPrep.Services.Helpers;
using InterviewPrep.Services.Rules;
using Microsoft.Extensions.Options;

namespace InterviewPrep.Services;

public sealed class QuestionService(
    IQuestionRepository questions, IUnitOfWork unitOfWork, IOptions<PagingOptions> paging) : IQuestionService
{
    public async Task<IReadOnlyList<TopicSummary>> GetTopicsAsync(CancellationToken ct)
    {
        var counts = await questions.CountByTopicAsync(ct);

        return Enum.GetValues<Topic>()
            .Select(t => new TopicSummary(t, t.DisplayName(), counts.GetValueOrDefault(t)))
            .ToList();
    }

    public async Task<PagedResponse<QuestionResponse>> GetQuestionsAsync(
        string? topic, string? difficulty, string? tag, int? limit, int? offset, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        Topic? topicFilter = null;
        Difficulty? difficultyFilter = null;

        if (topic is not null)
        {
            if (EnumParsing.TryParseName<Topic>(topic, out var t))
                topicFilter = t;
            else
                errors.AddNotOneOf<Topic>("topic");
        }

        if (difficulty is not null)
        {
            if (EnumParsing.TryParseName<Difficulty>(difficulty, out var d))
                difficultyFilter = d;
            else
                errors.AddNotOneOf<Difficulty>("difficulty");
        }

        Paging.TryResolve(limit, offset, paging.Value, errors, out var take, out var skip);
        errors.ThrowIfAny();

        var (items, total) = await questions.SearchAsync(
            topicFilter, difficultyFilter, QuestionTags.Normalize(tag), skip, take, ct);

        return new PagedResponse<QuestionResponse>(items.Select(q => q.ToResponse()).ToList(), total, take, skip);
    }

    public async Task<QuestionResponse> GetQuestionAsync(Guid id, CancellationToken ct) =>
        (await questions.GetByIdAsync(id, ct) ?? throw new NotFoundException()).ToResponse();

    public async Task<QuestionResponse> CreateQuestionAsync(CreateQuestionRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var valid = QuestionValidator.Validate(request, errors);
        errors.ThrowIfAny();

        var question = new Question
        {
            Topic = valid!.Topic,
            Difficulty = valid.Difficulty,
            Text = valid.Text,
            Answer = valid.Answer,
            Tags = valid.Tags,
            Source = QuestionSource.Manual
        };

        questions.Add(question);
        await unitOfWork.SaveChangesAsync(ct);

        return question.ToResponse();
    }
}
