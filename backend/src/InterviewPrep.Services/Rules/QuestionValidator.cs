using InterviewPrep.Data.Models;
using InterviewPrep.Data.Models.Enums;
using InterviewPrep.Services.Dtos;
using InterviewPrep.Services.Helpers;

namespace InterviewPrep.Services.Rules;

/// <summary>A create request that passed validation: parsed enums, trimmed text, normalized tags.</summary>
public sealed record ValidQuestion(Topic Topic, Difficulty Difficulty, string Text, string Answer, List<string> Tags);

public static class QuestionValidator
{
    /// <summary>Returns the parsed question, or null after adding every problem found to <paramref name="errors"/>.</summary>
    public static ValidQuestion? Validate(CreateQuestionRequest r, ValidationErrors errors)
    {
        errors.TryParseRequired<Topic>("topic", r.Topic, out var topic);
        errors.TryParseRequired<Difficulty>("difficulty", r.Difficulty, out var difficulty);

        var text = r.Text?.Trim() ?? "";
        if (text.Length == 0)
            errors.Add("text", "Question text is required.");
        else if (text.Length > QuestionLimits.TextMaxLength)
            errors.Add("text", $"Question text must be at most {QuestionLimits.TextMaxLength} characters.");

        var answer = r.Answer?.Trim() ?? "";
        if (answer.Length == 0)
            errors.Add("answer", "Answer is required.");
        else if (answer.Length > QuestionLimits.AnswerMaxLength)
            errors.Add("answer", $"Answer must be at most {QuestionLimits.AnswerMaxLength} characters.");

        var tags = QuestionTags.NormalizeAll(r.Tags);
        if (tags.Count > QuestionLimits.MaxTags)
            errors.Add("tags", $"At most {QuestionLimits.MaxTags} tags are allowed.");
        else if (tags.Any(t => t.Length > QuestionLimits.TagMaxLength))
            errors.Add("tags", $"Each tag must be at most {QuestionLimits.TagMaxLength} characters.");

        return errors.Any ? null : new ValidQuestion(topic, difficulty, text, answer, tags);
    }
}
