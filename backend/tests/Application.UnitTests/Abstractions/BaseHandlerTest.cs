using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Application.UnitTests.Abstractions;

public abstract class BaseHandlerTest
{
    /// <summary>
    /// A context over its own throwaway database. Pass the same
    /// <paramref name="databaseName"/> twice to get a second context over the
    /// same data — worth doing when a test seeds and then acts, since sharing one
    /// context lets change tracking paper over mistakes a real request would hit.
    /// </summary>
    protected static TestDbContext CreateDbContext(string? databaseName = null)
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"poker-game-manager-{Guid.NewGuid()}")
            .Options;

        return new TestDbContext(options);
    }

    protected static string NewDatabaseName() => $"poker-game-manager-{Guid.NewGuid()}";

    protected static HybridCache CreateCache()
    {
        var services = new ServiceCollection();

#pragma warning disable EXTEXP0018
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}
