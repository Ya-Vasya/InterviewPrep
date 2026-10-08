using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace InterviewPrep.Api.Tests;

/// <summary>
/// Hosts the real app against a private in-memory SQLite database. Use as an
/// <c>IClassFixture</c> so each test class gets its own database and tests cannot affect other classes.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // A shared-cache in-memory database lives only while at least one connection is open.
    private readonly SqliteConnection _keepAlive =
        new($"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive.Open();

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Default"] = _keepAlive.ConnectionString }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _keepAlive.Dispose();
    }
}
