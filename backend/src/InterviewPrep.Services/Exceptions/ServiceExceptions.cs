namespace InterviewPrep.Services.Exceptions;

/// <summary>The request is wrong. Carries one message per field; the API answers 400.</summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>The thing asked for does not exist; the API answers 404.</summary>
public sealed class NotFoundException(string? message = null) : Exception(message ?? "Not found.");

/// <summary>The request clashes with the current state; the API answers 409.</summary>
public sealed class ConflictException(string title, string detail) : Exception(detail)
{
    public string Title { get; } = title;
}

/// <summary>No API key is configured, so AI features are unavailable; the API answers 501.</summary>
public sealed class AiNotConfiguredException()
    : Exception("No Anthropic API key is configured, so AI features are unavailable.");

/// <summary>The daily limit of provider calls is used up; the API answers 429.</summary>
public sealed class AiDailyCapReachedException(int dailyCap)
    : Exception($"The daily limit of {dailyCap} AI calls is used up. It resets with your next day.");

/// <summary>The model returned something unusable, twice; the API answers 502.</summary>
public sealed class AiInvalidOutputException()
    : Exception("The AI returned an answer that could not be understood, twice. Try again.");
