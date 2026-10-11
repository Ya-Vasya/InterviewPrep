namespace InterviewPrep.Api.Features.Study;

/// <summary>
/// The user's self-assessment of how well they recalled a card. Not the question's
/// <see cref="Common.Difficulty"/>. Persisted as integers; do not renumber.
/// </summary>
public enum Rating
{
    Again = 0,
    Hard = 1,
    Good = 2,
    Easy = 3
}

public enum ReviewSource
{
    Self = 0,
    Ai = 1
}
