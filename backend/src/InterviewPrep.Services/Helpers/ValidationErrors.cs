using InterviewPrep.Services.Exceptions;

namespace InterviewPrep.Services.Helpers;

/// <summary>
/// Collects field errors for a request. Keeps one message per field, the last one added, so callers can check
/// "required" first and "valid" second. <see cref="ThrowIfAny"/> turns the collected errors into a 400.
/// </summary>
public sealed class ValidationErrors
{
    private readonly Dictionary<string, string[]> _errors = [];

    public bool Any => _errors.Count > 0;

    /// <summary>The errors collected so far, for callers that report them inside a larger response.</summary>
    public IReadOnlyDictionary<string, string[]> Errors => _errors;

    public void Add(string field, string message) => _errors[field] = [message];

    public void AddNotOneOf<TEnum>(string field) where TEnum : struct, Enum =>
        Add(field, $"{Label(field)} must be one of: {EnumParsing.AllowedValues<TEnum>()}.");

    /// <summary>Parses a required enum name, adding "is required" or "must be one of" on failure.</summary>
    public bool TryParseRequired<TEnum>(string field, string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;

        if (string.IsNullOrWhiteSpace(value))
            Add(field, $"{Label(field)} is required.");
        else if (EnumParsing.TryParseName(value, out result))
            return true;
        else
            AddNotOneOf<TEnum>(field);

        return false;
    }

    /// <summary>Resolves the IANA time zone the browser sent (UTC when absent), adding an error when it is unknown.</summary>
    public TimeZoneInfo RequireZone(string? timeZone)
    {
        if (!StudyDay.TryGetZone(timeZone, out var zone))
            Add("tz", "Unknown time zone.");

        return zone;
    }

    /// <exception cref="ValidationException">At least one error was added.</exception>
    public void ThrowIfAny()
    {
        if (Any)
            throw new ValidationException(_errors);
    }

    private static string Label(string field) => char.ToUpperInvariant(field[0]) + field[1..];
}
