using InterviewPrep.Api.Common;

namespace InterviewPrep.Api.Features.Questions;

public sealed record QuestionResponse(
    Guid Id,
    Topic Topic,
    string Text,
    string Answer,
    Difficulty Difficulty,
    IReadOnlyList<string> Tags,
    QuestionSource Source);

/// <summary>
/// Topic and difficulty arrive as strings so an unknown value becomes a validation problem
/// instead of a generic JSON binding failure.
/// </summary>
public sealed record CreateQuestionRequest(
    string? Topic,
    string? Text,
    string? Answer,
    string? Difficulty,
    IReadOnlyList<string>? Tags = null);

public sealed record TopicSummary(Topic Topic, string Name, int QuestionCount);

public static class QuestionMappings
{
    public static QuestionResponse ToResponse(this Question q) =>
        new(q.Id, q.Topic, q.Text, q.Answer, q.Difficulty, q.Tags, q.Source);
}
