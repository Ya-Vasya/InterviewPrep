namespace InterviewPrep.Data.Models.Enums;

/// <summary>Which AI feature a provider call is for. Persisted as integers; do not renumber.</summary>
public enum AiFeature
{
    CheckAnswer = 0,
    ExplainMistakes = 1,
    GenerateQuestions = 2
}
