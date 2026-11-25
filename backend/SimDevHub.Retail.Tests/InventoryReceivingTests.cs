using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class InventoryReceivingTests
{
    [Fact]
    public async Task Receiving_IsScopedAndCreatesOneTraceableMovement()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return; // Runs with PostgreSQL in CI.
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var alpha = new RetailTenant { Id = Guid.NewGuid(), Name = "Receiving Alpha", Slug = "receiving-alpha" };
        var beta = new RetailTenant { Id = Guid.NewGuid(), Name = "Receiving Beta", Slug = "receiving-beta" };
        var product = new CatalogueItem
        {
            Id = Guid.NewGuid(), TenantId = alpha.Id, Name = "Test Handset",
            Storage = "128GB", Category = "Phones", Price = 3500m, IsPublished = true
        };
        db.Tenants.AddRange(alpha, beta);
        db.CatalogueItems.Add(product);
        await db.SaveChangesAsync();

        var receiving = new InventoryReceivingService(db, TimeProvider.System);
        const string imei = "490154203237518"; // Test-only IMEI; not tied to a real handset.
        var request = new ReceiveDeviceRequest(alpha.Id, product.Id, imei, "GRN-1001");

        Assert.Equal(ReceiveDeviceResult.ProductNotFound, await receiving.ReceiveAsync(
            request with { TenantId = beta.Id }, CancellationToken.None));
        Assert.Equal(ReceiveDeviceResult.InvalidImei, await receiving.ReceiveAsync(
            request with { Imei = "123" }, CancellationToken.None));
        Assert.Equal(ReceiveDeviceResult.Received, await receiving.ReceiveAsync(request, CancellationToken.None));
        Assert.Equal(ReceiveDeviceResult.DuplicateImei, await receiving.ReceiveAsync(request, CancellationToken.None));
        Assert.Single(await db.SerialisedDevices.Where(x => x.TenantId == alpha.Id).ToListAsync());
        Assert.Single(await db.InventoryMovements.Where(x => x.TenantId == alpha.Id && x.MovementType == "Received").ToListAsync());
        Assert.Empty(await db.SerialisedDevices.Where(x => x.TenantId == beta.Id).ToListAsync());
    }
}
