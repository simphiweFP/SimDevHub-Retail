using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

/// <summary>
/// Runs against the ephemeral PostgreSQL service provided by CI.
/// No live environment or merchant database may be used as a test target.
/// </summary>
public sealed class PostgresCatalogueIntegrationTests
{
    [Fact]
    public async Task MigrationAndSeed_ProduceIsolatedPublishedCatalogue()
    {
        var connectionString = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseNpgsql(connectionString).Options;

        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();
        await DevelopmentSeed.SeedAsync(db);

        var reader = new StorefrontCatalogueReader(db);
        var nova = await reader.GetPublishedAsync("nova-mobile", CancellationToken.None);
        Assert.NotNull(nova);
        Assert.Equal("Nova Mobile", nova.StoreName);
        Assert.Equal(4, nova.Products.Count);

        var secondTenant = Guid.NewGuid();
        db.Tenants.Add(new RetailTenant { Id = secondTenant, Name = "Other Retailer", Slug = "other-retailer" });
        db.CatalogueItems.Add(new CatalogueItem
        {
            Id = Guid.NewGuid(), TenantId = secondTenant, Name = "Other Retailer's Phone",
            Storage = "64GB", Category = "Phones", Price = 900m, IsPublished = true
        });
        await db.SaveChangesAsync();

        var result = await reader.GetPublishedAsync("nova-mobile", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(4, result.Products.Count);
        Assert.DoesNotContain(result.Products, p => p.Name == "Other Retailer's Phone");
        Assert.Null(await reader.GetPublishedAsync("not-a-real-store", CancellationToken.None));
    }
}
