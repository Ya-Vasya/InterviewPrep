namespace InterviewPrep.Api.Common;

public static class EnumParsing
{
    /// <summary>
    /// Parses an enum member name, ignoring case. Unlike <see cref="Enum.TryParse{TEnum}(string?, bool, out TEnum)"/>
    /// it rejects numbers and comma-separated lists, so only real member names are accepted.
    /// </summary>
    public static bool TryParseName<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        var name = Enum.GetNames<TEnum>()
            .FirstOrDefault(n => n.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (name is null)
        {
            result = default;
            return false;
        }

        result = Enum.Parse<TEnum>(name);
        return true;
    }

    public static string AllowedValues<TEnum>() where TEnum : struct, Enum =>
        string.Join(", ", Enum.GetNames<TEnum>());
}
