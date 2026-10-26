using InterviewPrep.Data.Models.Enums;

namespace InterviewPrep.Data.Models;

/// <summary>
/// One request that reached the provider, kept to enforce the daily cap and show usage. Append-only.
/// EF entity, never serialized.
/// </summary>
public sealed class AiCall
{
    public long Id { get; init; }
    public required DateTime CalledAt { get; init; }
    public required AiFeature Feature { get; init; }
}
