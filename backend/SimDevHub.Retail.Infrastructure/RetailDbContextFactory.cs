using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SimDevHub.Retail.Infrastructure;

public sealed class RetailDbContextFactory : IDesignTimeDbContextFactory<RetailDbContext>
{
    public RetailDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("RETAIL_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=simdevhub_retail;Username=retail_dev;Password=local-development-only";
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new RetailDbContext(options);
    }
}
