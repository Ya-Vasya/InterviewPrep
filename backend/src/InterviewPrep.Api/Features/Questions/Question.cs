using InterviewPrep.Api.Common;

namespace InterviewPrep.Api.Features.Questions;

public enum QuestionSource
{
    Seed = 0,
    Manual = 1,
    Ai = 2
}

/// <summary>EF entity. Never serialized directly; see <see cref="QuestionResponse"/>.</summary>
public sealed class Question
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Topic Topic { get; set; }
    public required Difficulty Difficulty { get; set; }
    public required string Text { get; set; }

    /// <summary>Reference answer, markdown.</summary>
    public required string Answer { get; set; }

    /// <summary>Free-form subtopic tags, stored lowercase.</summary>
    public List<string> Tags { get; set; } = [];

    public required QuestionSource Source { get; init; }
}
