using InterviewPrep.Services.Dtos;

namespace InterviewPrep.Services;

public interface IQuestionService
{
    /// <summary>Always the full fixed set of topics, including those that have no questions yet.</summary>
    Task<IReadOnlyList<TopicSummary>> GetTopicsAsync(CancellationToken ct);

    /// <exception cref="Exceptions.ValidationException">Unknown topic or difficulty, or paging out of range.</exception>
    Task<PagedResponse<QuestionResponse>> GetQuestionsAsync(
        string? topic, string? difficulty, string? tag, int? limit, int? offset, CancellationToken ct);

    /// <exception cref="Exceptions.NotFoundException">No question has this id.</exception>
    Task<QuestionResponse> GetQuestionAsync(Guid id, CancellationToken ct);

    /// <summary>Saves a hand-written question (source Manual). Does no duplicate check.</summary>
    /// <exception cref="Exceptions.ValidationException">The request breaks a rule.</exception>
    Task<QuestionResponse> CreateQuestionAsync(CreateQuestionRequest request, CancellationToken ct);
}
