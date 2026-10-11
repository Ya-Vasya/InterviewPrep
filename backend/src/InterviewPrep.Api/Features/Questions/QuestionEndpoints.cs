using InterviewPrep.Api.Common;
using InterviewPrep.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace InterviewPrep.Api.Features.Questions;

public static class QuestionEndpoints
{
    public static IEndpointRouteBuilder MapQuestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Questions");

        group.MapGet("/topics", GetTopics).WithName(nameof(GetTopics));
        group.MapGet("/questions", GetQuestions).WithName(nameof(GetQuestions));
        group.MapGet("/questions/{id:guid}", GetQuestion).WithName(nameof(GetQuestion));
        group.MapPost("/questions", CreateQuestion).WithName(nameof(CreateQuestion));

        return app;
    }

    private static async Task<Ok<List<TopicSummary>>> GetTopics(AppDbContext db, CancellationToken ct)
    {
        var counts = await db.Questions
            .GroupBy(q => q.Topic)
            .Select(g => new { Topic = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Topic, x => x.Count, ct);

        // Always return the full fixed set, including topics that have no questions yet.
        var topics = Enum.GetValues<Topic>()
            .Select(t => new TopicSummary(t, t.DisplayName(), counts.GetValueOrDefault(t)))
            .ToList();

        return TypedResults.Ok(topics);
    }

    private static async Task<Results<Ok<PagedResponse<QuestionResponse>>, ValidationProblem>> GetQuestions(
        string? topic, string? difficulty, string? tag, int? limit, int? offset,
        AppDbContext db, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var query = db.Questions.AsQueryable();

        if (topic is not null)
        {
            if (EnumParsing.TryParseName<Topic>(topic, out var t))
                query = query.Where(q => q.Topic == t);
            else
                errors.AddNotOneOf<Topic>("topic");
        }

        if (difficulty is not null)
        {
            if (EnumParsing.TryParseName<Difficulty>(difficulty, out var d))
                query = query.Where(q => q.Difficulty == d);
            else
                errors.AddNotOneOf<Difficulty>("difficulty");
        }

        Paging.TryResolve(limit, offset, errors, out var take, out var skip);

        if (errors.Any)
            return errors.ToProblem();

        if (QuestionTags.Normalize(tag) is { Length: > 0 } normalizedTag)
            query = query.Where(q => q.Tags.Contains(normalizedTag));

        var total = await query.CountAsync(ct);

        // Id is the final tie-breaker so pages never overlap or skip rows when texts are equal.
        var questions = await query
            .OrderBy(q => q.Topic).ThenBy(q => q.Difficulty).ThenBy(q => q.Text).ThenBy(q => q.Id)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        return TypedResults.Ok(new PagedResponse<QuestionResponse>(
            questions.Select(q => q.ToResponse()).ToList(), total, take, skip));
    }

    private static async Task<Results<Ok<QuestionResponse>, NotFound>> GetQuestion(
        Guid id, AppDbContext db, CancellationToken ct) =>
        await db.Questions.FindAsync([id], ct) is { } question
            ? TypedResults.Ok(question.ToResponse())
            : TypedResults.NotFound();

    private static async Task<Results<Created<QuestionResponse>, ValidationProblem>> CreateQuestion(
        CreateQuestionRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        if (QuestionValidator.Validate(request, errors) is not { } valid)
            return errors.ToProblem();

        var question = new Question
        {
            Topic = valid.Topic,
            Difficulty = valid.Difficulty,
            Text = valid.Text,
            Answer = valid.Answer,
            Tags = valid.Tags,
            Source = QuestionSource.Manual
        };

        db.Questions.Add(question);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created($"/api/questions/{question.Id}", question.ToResponse());
    }
}
