using System.Text;

namespace InterviewPrep.Api.Features.Questions;

public static class QuestionTexts
{
    /// <summary>
    /// The form in which two question texts count as the same: lowercase, whitespace collapsed, punctuation
    /// removed. <c>#</c> and <c>+</c> are kept because they are part of names ("C#" and "C" differ, so do "C++" and "C").
    /// </summary>
    public static string Normalize(string? text)
    {
        var normalized = new StringBuilder();
        var pendingSpace = false;

        foreach (var c in text ?? "")
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true;
            }
            else if (char.IsLetterOrDigit(c) || c is '#' or '+')
            {
                if (pendingSpace && normalized.Length > 0)
                    normalized.Append(' ');

                pendingSpace = false;
                normalized.Append(char.ToLowerInvariant(c));
            }
        }

        return normalized.ToString();
    }
}
