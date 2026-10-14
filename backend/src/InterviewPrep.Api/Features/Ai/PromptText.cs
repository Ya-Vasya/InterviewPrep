namespace InterviewPrep.Api.Features.Ai;

public static class PromptText
{
    /// <summary>Angle brackets are escaped so the text cannot close its block and start a fake one.</summary>
    public static string Escape(string text) => text.Replace("<", "&lt;").Replace(">", "&gt;");
}
