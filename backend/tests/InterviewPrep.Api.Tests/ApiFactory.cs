using InterviewPrep.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewPrep.Api.Tests;

/// <summary>
/// Hosts the real app against a private in-memory SQLite database. Use as an
/// <c>IClassFixture</c> so each test class gets its own database and tests cannot affect other classes.
/// The app runs on a <see cref="FakeClock"/>.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    // A shared-cache in-memory database lives only while at least one connection is open.
    private readonly SqliteConnection _keepAlive =
        new($"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared");

    public FakeClock Clock { get; } = new();

    /// <summary>Extra configuration for the app, e.g. <c>Study:NewCardsPerDay</c>.</summary>
    protected virtual IReadOnlyDictionary<string, string?> Settings { get; } = new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive.Open();

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Default"] = _keepAlive.ConnectionString });
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _keepAlive.Dispose();
    }
}
