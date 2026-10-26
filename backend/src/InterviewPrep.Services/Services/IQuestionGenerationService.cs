using InterviewPrep.Services.Dtos;

namespace InterviewPrep.Services;

/// <summary>Generating Drafts with the AI and accepting them as questions.</summary>
public interface IQuestionGenerationService
{
    /// <summary>One counted provider call. Nothing is stored; the user reviews the Drafts and accepts them separately.</summary>
    /// <exception cref="Exceptions.ValidationException">The request breaks a rule.</exception>
    Task<GenerateQuestionsResponse> GenerateAsync(GenerateQuestionsRequest request, string? timeZone, CancellationToken ct);

    /// <summary>
    /// Saves the valid Drafts as AI-sourced questions and reports the others by position, so one bad Draft does not
    /// lose the rest. A Draft whose text already exists (also from an earlier call or the same request) is rejected,
    /// which makes a double submit harmless.
    /// </summary>
    /// <exception cref="Exceptions.ValidationException">There are no Drafts, or too many.</exception>
    Task<AcceptDraftsResponse> AcceptAsync(AcceptDraftsRequest request, CancellationToken ct);
}
