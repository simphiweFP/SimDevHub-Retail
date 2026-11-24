using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class StorefrontCatalogueReaderTests
{
    [Fact]
    public async Task PublishedProducts_AreScopedToRequestedTenant()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseSqlite(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var alpha = new RetailTenant { Id = Guid.NewGuid(), Name = "Alpha Phones", Slug = "alpha" };
        var beta = new RetailTenant { Id = Guid.NewGuid(), Name = "Beta Phones", Slug = "beta" };
        var inactive = new RetailTenant { Id = Guid.NewGuid(), Name = "Hidden Shop", Slug = "hidden", IsActive = false };
        db.Tenants.AddRange(alpha, beta, inactive);
        db.CatalogueItems.AddRange(
            new CatalogueItem { Id = Guid.NewGuid(), TenantId = alpha.Id, Name = "Public Alpha", Storage = "128GB", Category = "Phones", Price = 100m, IsPublished = true },
            new CatalogueItem { Id = Guid.NewGuid(), TenantId = alpha.Id, Name = "Private Alpha", Storage = "128GB", Category = "Phones", Price = 100m, IsPublished = false },
            new CatalogueItem { Id = Guid.NewGuid(), TenantId = beta.Id, Name = "Beta Phone", Storage = "256GB", Category = "Phones", Price = 200m, IsPublished = true },
            new CatalogueItem { Id = Guid.NewGuid(), TenantId = inactive.Id, Name = "Hidden Phone", Storage = "256GB", Category = "Phones", Price = 300m, IsPublished = true }
        );
        await db.SaveChangesAsync();

        var reader = new StorefrontCatalogueReader(db);
        var result = await reader.GetPublishedAsync("alpha", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("Alpha Phones", result.StoreName);
        Assert.Single(result.Products);
        Assert.Equal("Public Alpha", result.Products[0].Name);
        Assert.Null(await reader.GetPublishedAsync("hidden", CancellationToken.None));
        Assert.Null(await reader.GetPublishedAsync("missing", CancellationToken.None));
    }
}
