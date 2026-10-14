namespace InterviewPrep.Api.Features.Ai;

/// <param name="Enabled">False when no API key is configured; the UI then hides the AI controls.</param>
/// <param name="UsedToday">Provider calls made during the current day of the requesting browser's time zone.</param>
public sealed record AiStatusResponse(bool Enabled, int UsedToday, int DailyCap);

/// <summary>Loosely typed so a missing or bad value becomes a validation problem, not a JSON binding failure.</summary>
public sealed record CheckAnswerRequest(Guid? QuestionId, string? Answer);

/// <param name="AttemptId">Send it with the review so the server can tell the user accepted the suggestion.</param>
/// <param name="Score">0-100.</param>
/// <param name="Covered">Key points the user got right.</param>
/// <param name="Missing">Key points the user left out.</param>
/// <param name="SuggestedRating">Only a suggestion. A check never rates a card by itself.</param>
public sealed record CheckAnswerResponse(
    Guid AttemptId,
    Verdict Verdict,
    int Score,
    IReadOnlyList<string> Covered,
    IReadOnlyList<string> Missing,
    string Feedback,
    Study.Rating SuggestedRating);

/// <summary>Loosely typed so a missing or bad value becomes a validation problem, not a JSON binding failure.</summary>
/// <param name="AttemptId">The partial or incorrect answer attempt to explain.</param>
public sealed record ExplainMistakesRequest(Guid? AttemptId);

/// <param name="Explanation">Concise markdown: per mistake what was wrong, why and the correct explanation.</param>
public sealed record ExplainMistakesResponse(Guid AttemptId, string Explanation);

/// <summary>A stored answer attempt, as listed on the card back.</summary>
public sealed record AnswerAttemptResponse(
    Guid Id,
    Guid QuestionId,
    string Answer,
    Verdict Verdict,
    int Score,
    IReadOnlyList<string> Covered,
    IReadOnlyList<string> Missing,
    string Feedback,
    DateTime CreatedAt,
    Study.Rating SuggestedRating)
{
    public static AnswerAttemptResponse From(AnswerAttempt a) =>
        new(a.Id, a.QuestionId, a.Answer, a.Verdict, a.Score, a.Covered, a.Missing, a.Feedback, a.CreatedAt,
            a.Verdict.SuggestedRating());
}
