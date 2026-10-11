namespace InterviewPrep.Api.Features.Study;

/// <summary>Bound from the "Study" configuration section.</summary>
public sealed class StudyOptions
{
    public const string SectionName = "Study";

    /// <summary>Most new cards that may be introduced per day.</summary>
    public int NewCardsPerDay { get; set; } = 20;

    /// <summary>Most reviews of existing cards per day. Reviews that create a card count as new cards instead.</summary>
    public int ReviewsPerDay { get; set; } = 200;
}
