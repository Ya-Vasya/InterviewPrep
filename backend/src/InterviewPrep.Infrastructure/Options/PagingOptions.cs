namespace InterviewPrep.Infrastructure.Options;

/// <summary>Bound from the "Paging" section: <c>limit</c> defaults and bounds shared by the list endpoints.</summary>
public sealed class PagingOptions
{
    public const string SectionName = "Paging";

    public int DefaultLimit { get; set; }

    public int MaxLimit { get; set; }
}
