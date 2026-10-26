using InterviewPrep.Data.Models.Enums;

namespace InterviewPrep.Data.Models;

/// <summary>EF entity. Never serialized directly; see the question DTOs in the Services layer.</summary>
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
