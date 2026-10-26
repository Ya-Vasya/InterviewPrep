using System.Text.Json.Serialization;
using InterviewPrep.Api.Middleware;
using InterviewPrep.Data;
using InterviewPrep.Infrastructure.Extensions;
using InterviewPrep.Services.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Enums travel as strings. Controllers read the MVC JSON options; the OpenAPI generator reads the shared HTTP ones.
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddConfigurationOptions();
builder.Services.AddDatabase();
builder.Services.AddRepositories();
builder.Services.AddAiProvider();
builder.Services.AddServices();

const string FrontendCors = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCors, policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
}

app.UseExceptionHandler();
app.UseStatusCodePages(); // empty 4xx/5xx responses (404, 405, ...) become ProblemDetails
app.UseCors(FrontendCors);

app.MapControllers();

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>.
public partial class Program { }
