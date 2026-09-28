using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HomNayLive.Persistence;

public class HomNayLiveDesignTimeFactory : IDesignTimeDbContextFactory<HomNayLiveDbContext>
{
    public HomNayLiveDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("HOMNAY_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            connection = "Server=(localdb)\\MSSQLLocalDB;Database=HomNayLive;Trusted_Connection=True;TrustServerCertificate=True;";
        }

        var optionsBuilder = new DbContextOptionsBuilder<HomNayLiveDbContext>();
        optionsBuilder.UseSqlServer(connection);
        return new HomNayLiveDbContext(optionsBuilder.Options);
    }
}
