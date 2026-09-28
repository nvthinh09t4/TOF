using Microsoft.EntityFrameworkCore;
using HomNayLive.Persistence;

namespace HomNayLive.Persistence.Tests.Infrastructure;

public sealed class SqlServerLocalDbFixture : IDisposable
{
    public string ConnectionString { get; }
    public string DatabaseName { get; }

    public SqlServerLocalDbFixture()
    {
        DatabaseName = $"HomNayLive_{Guid.NewGuid():N}";
        ConnectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True;";

        using var context = new HomNayLiveDbContext(new DbContextOptionsBuilder<HomNayLiveDbContext>()
            .UseSqlServer(ConnectionString)
            .Options);

        context.Database.EnsureCreated();
    }

    public HomNayLiveDbContext CreateContext()
    {
        return new HomNayLiveDbContext(new DbContextOptionsBuilder<HomNayLiveDbContext>()
            .UseSqlServer(ConnectionString)
            .Options);
    }

    public void Dispose()
    {
        using var context = CreateContext();
        context.Database.EnsureDeleted();
    }
}
