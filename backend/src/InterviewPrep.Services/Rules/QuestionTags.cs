namespace InterviewPrep.Services.Rules;

/// <summary>Tags are case-insensitive: stored and matched trimmed and lowercase.</summary>
public static class QuestionTags
{
    public static string Normalize(string? tag) => tag?.Trim().ToLowerInvariant() ?? "";

    /// <summary>Normalizes every tag, dropping blanks and duplicates while keeping the first-seen order.</summary>
    public static List<string> NormalizeAll(IEnumerable<string>? tags) =>
        (tags ?? []).Select(Normalize).Where(t => t.Length > 0).Distinct().ToList();
}
