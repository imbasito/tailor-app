using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace STailor.Infrastructure.Persistence;

public sealed class LocalTailorDbContextFactory : IDesignTimeDbContextFactory<LocalTailorDbContext>
{
    public LocalTailorDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("STAILOR_LOCAL_CONNECTION")
            ?? "Data Source=stailor-local.db";

        var optionsBuilder = new DbContextOptionsBuilder<LocalTailorDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        return new LocalTailorDbContext(optionsBuilder.Options);
    }
}

public sealed class CentralTailorDbContextFactory : IDesignTimeDbContextFactory<CentralTailorDbContext>
{
    public CentralTailorDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("STAILOR_CENTRAL_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=stailor;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<CentralTailorDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new CentralTailorDbContext(optionsBuilder.Options);
    }
}
