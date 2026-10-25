using System.Text.Json.Serialization;
using InterviewPrep.Api.Data;
using InterviewPrep.Api.Features.Ai;
using InterviewPrep.Api.Features.Notes;
using InterviewPrep.Api.Features.Questions;
using InterviewPrep.Api.Features.Study;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddPersistence();
builder.Services.AddStudyFeature();
builder.Services.AddAiFeature();

const string FrontendCors = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCors, policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

await app.InitializeDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
}

app.UseExceptionHandler();
app.UseStatusCodePages(); // empty 4xx/5xx responses (404, 405, ...) become ProblemDetails
app.UseCors(FrontendCors);

app.MapGet("/api/health", () => TypedResults.Ok(new { status = "ok" }))
   .WithName("Health");

app.MapQuestionEndpoints();
app.MapStudyEndpoints();
app.MapAiEndpoints();
app.MapNoteEndpoints();

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program { }
