using InterviewPrep.Api.Common;
using InterviewPrep.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace InterviewPrep.Api.Features.Questions;

public static class QuestionEndpoints
{
    public static IEndpointRouteBuilder MapQuestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Questions");

        group.MapGet("/topics", GetTopics);
        group.MapGet("/questions", GetQuestions);
        group.MapGet("/questions/{id:guid}", GetQuestion);
        group.MapPost("/questions", CreateQuestion);

        return app;
    }

    private static async Task<IResult> GetTopics(AppDbContext db, CancellationToken ct)
    {
        var counts = await db.Questions
            .GroupBy(q => q.Topic)
            .Select(g => new { Topic = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Topic, x => x.Count, ct);

        // Always return the full fixed set, including topics that have no questions yet.
        var topics = Enum.GetValues<Topic>()
            .Select(t => new TopicSummary(t, t.DisplayName(), counts.GetValueOrDefault(t)))
            .ToList();

        return Results.Ok(topics);
    }

    private static async Task<IResult> GetQuestions(
        string? topic, string? difficulty, string? tag, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var query = db.Questions.AsQueryable();

        if (topic is not null)
        {
            if (EnumParsing.TryParseName<Topic>(topic, out var t))
                query = query.Where(q => q.Topic == t);
            else
                errors["topic"] = [$"Topic must be one of: {EnumParsing.AllowedValues<Topic>()}."];
        }

        if (difficulty is not null)
        {
            if (EnumParsing.TryParseName<Difficulty>(difficulty, out var d))
                query = query.Where(q => q.Difficulty == d);
            else
                errors["difficulty"] = [$"Difficulty must be one of: {EnumParsing.AllowedValues<Difficulty>()}."];
        }

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        if (NormalizeTag(tag) is { Length: > 0 } normalizedTag)
            query = query.Where(q => q.Tags.Contains(normalizedTag));

        var questions = await query
            .OrderBy(q => q.Topic).ThenBy(q => q.Difficulty).ThenBy(q => q.Text)
            .ToListAsync(ct);

        return Results.Ok(questions.Select(q => q.ToResponse()));
    }

    private static async Task<IResult> GetQuestion(Guid id, AppDbContext db, CancellationToken ct) =>
        await db.Questions.FindAsync([id], ct) is { } question
            ? Results.Ok(question.ToResponse())
            : Results.NotFound();

    private static async Task<IResult> CreateQuestion(
        CreateQuestionRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = Validate(request, out var topic, out var difficulty, out var tags);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var question = new Question
        {
            Topic = topic,
            Difficulty = difficulty,
            Text = request.Text!.Trim(),
            Answer = request.Answer!.Trim(),
            Tags = tags,
            Source = QuestionSource.Manual
        };

        db.Questions.Add(question);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/questions/{question.Id}", question.ToResponse());
    }

    private static Dictionary<string, string[]> Validate(
        CreateQuestionRequest r, out Topic topic, out Difficulty difficulty, out List<string> tags)
    {
        var errors = new Dictionary<string, string[]>();
        topic = default;
        difficulty = default;

        if (string.IsNullOrWhiteSpace(r.Topic))
            errors["topic"] = ["Topic is required."];
        else if (!EnumParsing.TryParseName(r.Topic, out topic))
            errors["topic"] = [$"Topic must be one of: {EnumParsing.AllowedValues<Topic>()}."];

        if (string.IsNullOrWhiteSpace(r.Difficulty))
            errors["difficulty"] = ["Difficulty is required."];
        else if (!EnumParsing.TryParseName(r.Difficulty, out difficulty))
            errors["difficulty"] = [$"Difficulty must be one of: {EnumParsing.AllowedValues<Difficulty>()}."];

        if (string.IsNullOrWhiteSpace(r.Text))
            errors["text"] = ["Question text is required."];
        else if (r.Text.Trim().Length > QuestionLimits.TextMaxLength)
            errors["text"] = [$"Question text must be at most {QuestionLimits.TextMaxLength} characters."];

        if (string.IsNullOrWhiteSpace(r.Answer))
            errors["answer"] = ["Answer is required."];
        else if (r.Answer.Trim().Length > QuestionLimits.AnswerMaxLength)
            errors["answer"] = [$"Answer must be at most {QuestionLimits.AnswerMaxLength} characters."];

        tags = (r.Tags ?? [])
            .Select(NormalizeTag)
            .Where(t => t.Length > 0)
            .Distinct()
            .ToList();

        if (tags.Count > QuestionLimits.MaxTags)
            errors["tags"] = [$"At most {QuestionLimits.MaxTags} tags are allowed."];
        else if (tags.Any(t => t.Length > QuestionLimits.TagMaxLength))
            errors["tags"] = [$"Each tag must be at most {QuestionLimits.TagMaxLength} characters."];

        return errors;
    }

    /// <summary>Tags are case-insensitive: stored and matched trimmed and lowercase.</summary>
    private static string NormalizeTag(string? tag) => tag?.Trim().ToLowerInvariant() ?? "";
}
