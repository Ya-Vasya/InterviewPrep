
namespace InterviewPrep.Api.Tests;

public class QuestionTextsTests
{
    [Theory]
    [InlineData("What is the difference between a class and a struct?", "what is the difference between a class and a struct")]
    [InlineData("  WHAT   is\ta\nTask?! ", "what is a task")]
    [InlineData("What is C#?", "what is c#")]
    [InlineData("What is C++, really?", "what is c++ really")]
    [InlineData("async/await", "asyncawait")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("?!...", "")]
    public void Normalize_LowercasesCollapsesWhitespaceAndStripsPunctuationExceptHashAndPlus(string? input, string expected) =>
        Assert.Equal(expected, QuestionTexts.Normalize(input));

    [Fact]
    public void Normalize_KeepsCSharpAndCApart() =>
        Assert.NotEqual(QuestionTexts.Normalize("What is C#?"), QuestionTexts.Normalize("What is C?"));
}
