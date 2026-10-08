namespace InterviewPrep.Api.Models;

public enum Difficulty
{
    Junior,
    Middle,
    Senior
}

public sealed record Question(
    Guid Id,
    string Topic,
    string Text,
    string Answer,
    Difficulty Difficulty);

public sealed record CreateQuestionRequest(
    string Topic,
    string Text,
    string Answer,
    Difficulty Difficulty);
