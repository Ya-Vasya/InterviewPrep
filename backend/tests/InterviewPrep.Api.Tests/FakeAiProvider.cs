
namespace InterviewPrep.Api.Tests;

/// <summary>
/// Scriptable stand-in for the LLM. Replies are consumed in order; every request is recorded so tests can
/// check what the model was asked. Tests never call the real API.
/// </summary>
public sealed class FakeAiProvider : IAiProvider
{
    private readonly Queue<Func<AiRequest, string>> _script = new();

    public bool IsConfigured { get; set; } = true;

    public List<AiRequest> Requests { get; } = [];

    public void Reply(string json) => _script.Enqueue(_ => json);

    public void Fail(Exception exception) => _script.Enqueue(_ => throw exception);

    public void Reset()
    {
        IsConfigured = true;
        Requests.Clear();
        _script.Clear();
    }

    public async Task<string> CompleteJsonAsync(AiRequest request, CancellationToken ct)
    {
        await Task.Yield();
        Requests.Add(request);

        if (!_script.TryDequeue(out var next))
            throw new InvalidOperationException("FakeAiProvider was called but has no scripted reply.");

        return next(request);
    }
}
