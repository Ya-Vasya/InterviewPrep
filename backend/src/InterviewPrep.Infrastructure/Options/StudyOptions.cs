namespace InterviewPrep.Infrastructure.Options;

/// <summary>Bound from the "Study" section of appsettings.json. No values are hardcoded here.</summary>
public sealed class StudyOptions
{
    public const string SectionName = "Study";

    /// <summary>Most new cards that may be introduced per day.</summary>
    public int NewCardsPerDay { get; set; }

    /// <summary>Most reviews of existing cards per day. Reviews that create a card count as new cards instead.</summary>
    public int ReviewsPerDay { get; set; }

    /// <summary>How many cards a study queue holds when the request gives no limit.</summary>
    public int DefaultQueueLimit { get; set; }

    /// <summary>The largest queue a request may ask for.</summary>
    public int MaxQueueLimit { get; set; }
}
