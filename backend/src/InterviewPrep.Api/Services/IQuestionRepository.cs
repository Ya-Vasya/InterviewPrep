using InterviewPrep.Api.Models;

namespace InterviewPrep.Api.Services;

public interface IQuestionRepository
{
    IReadOnlyList<Question> GetAll(string? topic = null);
    Question? GetById(Guid id);
    IReadOnlyList<string> GetTopics();
    Question Add(CreateQuestionRequest request);
}
