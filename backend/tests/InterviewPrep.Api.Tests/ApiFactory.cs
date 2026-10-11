using InterviewPrep.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace InterviewPrep.Api.Tests;

/// <summary>
/// Hosts the real app against a private PostgreSQL database. Use as an <c>IClassFixture</c> so each
/// test class gets its own database and tests cannot affect other classes.
/// The app runs on a <see cref="FakeClock"/>.
/// </summary>
/// <remarks>
/// One PostgreSQL container is started lazily and shared by every factory (starting one per class would
/// be slow); each factory creates its own database inside it. Requires a running Docker daemon.
/// The container is removed when the test run ends.
/// </remarks>
public class ApiFactory : WebApplicationFactory<Program>
{
    private static readonly Lazy<Task<PostgreSqlContainer>> SharedServer = new(async () =>
    {
        var container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await container.StartAsync();
        return container;
    });

    private readonly string _database = $"test_{Guid.NewGuid():N}";
    private string? _connectionString;

    public FakeClock Clock { get; } = new();

    /// <summary>Extra configuration for the app, e.g. <c>Study:NewCardsPerDay</c>.</summary>
    protected virtual IReadOnlyDictionary<string, string?> Settings { get; } = new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connectionString ??= CreateDatabase();

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Default"] = _connectionString });
            config.AddInMemoryCollection(Settings);
        });

        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
    }

    /// <summary>Deletes all cards and reviews and rewinds the clock, so a test starts from a clean slate.</summary>
    public void ResetStudyData()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ReviewLogs.ExecuteDelete();
        db.Cards.ExecuteDelete();
        Clock.Reset();
    }

    private string CreateDatabase()
    {
        // ConfigureWebHost is synchronous; blocking here is fine because it runs once per fixture.
        var server = SharedServer.Value.GetAwaiter().GetResult();

        using var connection = new NpgsqlConnection(server.GetConnectionString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{_database}\"";
        command.ExecuteNonQuery();

        return new NpgsqlConnectionStringBuilder(server.GetConnectionString()) { Database = _database }
            .ConnectionString;
    }
}
