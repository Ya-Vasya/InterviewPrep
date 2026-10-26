namespace InterviewPrep.Services.Dtos;

/// <summary>A page of a list endpoint. <c>Total</c> counts all matches, not just this page.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Limit, int Offset);
