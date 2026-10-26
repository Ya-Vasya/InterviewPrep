namespace InterviewPrep.Services.Helpers;

/// <summary>Day boundaries in the user's time zone, which the browser sends as an IANA id.</summary>
public static class StudyDay
{
    public static bool TryGetZone(string? id, out TimeZoneInfo zone)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            zone = TimeZoneInfo.Utc;
            return true;
        }

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
            return true;
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
    }

    /// <summary>The UTC instant at which the user's current local day began.</summary>
    public static DateTime StartOfDayUtc(DateTime nowUtc, TimeZoneInfo zone)
    {
        var localMidnight = DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), zone).Date,
            DateTimeKind.Unspecified);

        // Some zones skip midnight on a DST change; the day then starts when the clock jumps forward.
        while (zone.IsInvalidTime(localMidnight))
            localMidnight = localMidnight.AddMinutes(30);

        return TimeZoneInfo.ConvertTimeToUtc(localMidnight, zone);
    }
}
