using System.Collections.Concurrent;
using InterviewPrep.Api.Models;

namespace InterviewPrep.Api.Services;

public sealed class InMemoryQuestionRepository : IQuestionRepository
{
    private readonly ConcurrentDictionary<Guid, Question> _questions = new();

    public InMemoryQuestionRepository()
    {
        Seed("C#", "What is the difference between a class and a struct?",
            "Classes are reference types allocated on the heap; structs are value types copied by value. Structs suit small, immutable data.",
            Difficulty.Junior);
        Seed("C#", "What does async/await do under the hood?",
            "The compiler rewrites the method into a state machine. await registers a continuation on the awaited task and frees the thread until it completes.",
            Difficulty.Middle);
        Seed("ASP.NET Core", "What are the service lifetimes in the built-in DI container?",
            "Transient (new instance each time), Scoped (one per request), Singleton (one for the app). Don't inject scoped services into singletons.",
            Difficulty.Middle);
        Seed("ASP.NET Core", "How does the middleware pipeline work?",
            "Each middleware receives HttpContext and a next delegate, can act before/after calling next, or short-circuit. Order of registration matters.",
            Difficulty.Middle);
        Seed("EF Core", "What is the N+1 query problem and how do you avoid it?",
            "Loading a list, then lazily querying related data per item. Avoid with Include, projections (Select), or split queries.",
            Difficulty.Senior);
        Seed("SQL", "What is the difference between a clustered and a non-clustered index?",
            "A clustered index defines the physical row order (one per table); a non-clustered index is a separate structure pointing to rows.",
            Difficulty.Middle);
    }

    public IReadOnlyList<Question> GetAll(string? topic = null) =>
        _questions.Values
            .Where(q => topic is null || q.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase))
            .OrderBy(q => q.Topic).ThenBy(q => q.Difficulty)
            .ToList();

    public Question? GetById(Guid id) => _questions.GetValueOrDefault(id);

    public IReadOnlyList<string> GetTopics() =>
        _questions.Values.Select(q => q.Topic).Distinct().Order().ToList();

    public Question Add(CreateQuestionRequest request)
    {
        var question = new Question(Guid.NewGuid(), request.Topic.Trim(), request.Text.Trim(),
            request.Answer.Trim(), request.Difficulty);
        _questions[question.Id] = question;
        return question;
    }

    private void Seed(string topic, string text, string answer, Difficulty difficulty) =>
        Add(new CreateQuestionRequest(topic, text, answer, difficulty));
}
