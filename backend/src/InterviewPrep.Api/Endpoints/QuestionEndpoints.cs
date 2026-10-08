using InterviewPrep.Api.Models;
using InterviewPrep.Api.Services;

namespace InterviewPrep.Api.Endpoints;

public static class QuestionEndpoints
{
    public static IEndpointRouteBuilder MapQuestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Questions");

        group.MapGet("/topics", (IQuestionRepository repo) => repo.GetTopics());

        group.MapGet("/questions", (string? topic, IQuestionRepository repo) => repo.GetAll(topic));

        group.MapGet("/questions/{id:guid}", (Guid id, IQuestionRepository repo) =>
            repo.GetById(id) is { } q ? Results.Ok(q) : Results.NotFound());

        group.MapPost("/questions", (CreateQuestionRequest request, IQuestionRepository repo) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var created = repo.Add(request);
            return Results.Created($"/api/questions/{created.Id}", created);
        });

        return app;
    }

    private static Dictionary<string, string[]> Validate(CreateQuestionRequest r)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(r.Topic)) errors["topic"] = ["Topic is required."];
        if (string.IsNullOrWhiteSpace(r.Text)) errors["text"] = ["Question text is required."];
        if (string.IsNullOrWhiteSpace(r.Answer)) errors["answer"] = ["Answer is required."];
        return errors;
    }
}
