using InterviewPrep.Infrastructure.Options;

namespace InterviewPrep.Services.Helpers;

/// <summary><c>limit</c> and <c>offset</c> query parameters shared by list endpoints. The bounds come from configuration.</summary>
public static class Paging
{
    /// <summary>Validates the raw parameters; on failure adds errors and returns false.</summary>
    public static bool TryResolve(
        int? limit, int? offset, PagingOptions options, ValidationErrors errors, out int resolvedLimit, out int resolvedOffset)
    {
        resolvedLimit = limit ?? options.DefaultLimit;
        resolvedOffset = offset ?? 0;

        if (resolvedLimit < 1 || resolvedLimit > options.MaxLimit)
            errors.Add("limit", $"Limit must be between 1 and {options.MaxLimit}.");

        if (resolvedOffset < 0)
            errors.Add("offset", "Offset must be 0 or more.");

        return !errors.Any;
    }
}
