namespace InterviewPrep.Api.Common;

/// <summary>A page of a list endpoint. <c>Total</c> counts all matches, not just this page.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Limit, int Offset);

/// <summary><c>limit</c> and <c>offset</c> query parameters shared by list endpoints.</summary>
public static class Paging
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    /// <summary>Validates the raw parameters; on failure adds errors and returns false.</summary>
    public static bool TryResolve(int? limit, int? offset, ValidationErrors errors, out int resolvedLimit, out int resolvedOffset)
    {
        resolvedLimit = limit ?? DefaultLimit;
        resolvedOffset = offset ?? 0;

        if (resolvedLimit is < 1 or > MaxLimit)
            errors.Add("limit", $"Limit must be between 1 and {MaxLimit}.");

        if (resolvedOffset < 0)
            errors.Add("offset", "Offset must be 0 or more.");

        return !errors.Any;
    }
}
