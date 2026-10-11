using InterviewPrep.Api.Common;
using InterviewPrep.Api.Features.Questions;

namespace InterviewPrep.Api.Tests;

public class QuestionTagsTests
{
    [Theory]
    [InlineData("  Async ", "async")]
    [InlineData("EF Core", "ef core")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Normalize_TrimsAndLowercases(string? tag, string expected) =>
        Assert.Equal(expected, QuestionTags.Normalize(tag));

    [Fact]
    public void NormalizeAll_DropsBlanksAndDuplicates_KeepingFirstSeenOrder() =>
        Assert.Equal(["lambdas", "closures"], QuestionTags.NormalizeAll(["  Lambdas ", "", "lambdas", "Closures", "  "]));

    [Fact]
    public void NormalizeAll_OfNull_IsEmpty() =>
        Assert.Empty(QuestionTags.NormalizeAll(null));
}

public class ValidationErrorsTests
{
    [Fact]
    public void TryParseRequired_ParsesAValidName_CaseInsensitively()
    {
        var errors = new ValidationErrors();

        Assert.True(errors.TryParseRequired<Topic>("topic", " sql ", out var topic));

        Assert.Equal(Topic.Sql, topic);
        Assert.False(errors.Any);
    }

    [Theory]
    [InlineData(null, "Topic is required.")]
    [InlineData("  ", "Topic is required.")]
    [InlineData("Cobol", "Topic must be one of: CSharp, AspNetCore, Sql, SystemDesign.")]
    [InlineData("1", "Topic must be one of: CSharp, AspNetCore, Sql, SystemDesign.")]
    public void TryParseRequired_RejectsMissingAndUnknownValues(string? value, string message)
    {
        var errors = new ValidationErrors();

        Assert.False(errors.TryParseRequired<Topic>("topic", value, out _));

        Assert.True(errors.Any);
        Assert.Equal([message], errors.ToProblem().ProblemDetails.Errors["topic"]);
    }

    [Fact]
    public void Add_KeepsTheLastMessageForAField()
    {
        var errors = new ValidationErrors();
        errors.Add("text", "first");
        errors.Add("text", "second");

        Assert.Equal(["second"], errors.ToProblem().ProblemDetails.Errors["text"]);
    }
}

public class PagingTests
{
    [Theory]
    [InlineData(null, null, 50, 0)]
    [InlineData(1, 0, 1, 0)]
    [InlineData(200, 30, 200, 30)]
    public void TryResolve_AppliesDefaultsAndAcceptsValidValues(int? limit, int? offset, int expectedLimit, int expectedOffset)
    {
        var errors = new ValidationErrors();

        Assert.True(Paging.TryResolve(limit, offset, errors, out var l, out var o));

        Assert.Equal((expectedLimit, expectedOffset), (l, o));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(201, null)]
    [InlineData(null, -1)]
    public void TryResolve_RejectsOutOfRangeValues(int? limit, int? offset)
    {
        var errors = new ValidationErrors();

        Assert.False(Paging.TryResolve(limit, offset, errors, out _, out _));
        Assert.True(errors.Any);
    }
}
