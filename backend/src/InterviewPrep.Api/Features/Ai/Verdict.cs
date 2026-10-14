using InterviewPrep.Api.Features.Study;

namespace InterviewPrep.Api.Features.Ai;

/// <summary>The AI's judgement of an answer attempt. Persisted as integers; do not renumber.</summary>
public enum Verdict
{
    Correct = 0,
    Partial = 1,
    Incorrect = 2
}

public static class VerdictExtensions
{
    /// <summary>incorrect is Again, partial is Hard, correct is Good. Only a suggestion: the user decides.</summary>
    public static Rating SuggestedRating(this Verdict verdict) => verdict switch
    {
        Verdict.Correct => Rating.Good,
        Verdict.Partial => Rating.Hard,
        _ => Rating.Again
    };
}
