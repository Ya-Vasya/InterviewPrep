using Microsoft.EntityFrameworkCore;

namespace InterviewPrep.Api.Data;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        // The connection string is read lazily so tests can override it before the host is built.
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default"))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        return services;
    }
}
