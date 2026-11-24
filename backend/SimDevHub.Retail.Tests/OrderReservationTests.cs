using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class OrderReservationTests
{
    [Fact]
    public async Task OnlyOneOrderCanReserveSameImei_AndPriceComesFromDatabase()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        db.Tenants.Add(new RetailTenant { Id = tenantId, Name = "Reservation Test", Slug = "order-" + tenantId.ToString("N")[..14] });
        db.CatalogueItems.Add(new CatalogueItem { Id = productId, TenantId = tenantId, Name = "Phone", Category = "Phones", Storage = "128GB", Price = 7999m, IsPublished = true });
        db.SerialisedDevices.Add(new SerialisedDevice { Id = deviceId, TenantId = tenantId, ProductId = productId,
            Imei = "490154203237518", Status = "Available", ReceivedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var service = new OrderReservationService(db, TimeProvider.System);
        var request = new ReserveOrderRequest(tenantId, productId, deviceId);
        var first = await service.ReserveAsync(request, CancellationToken.None);
        var second = await service.ReserveAsync(request, CancellationToken.None);
        Assert.Equal(ReserveOrderStatus.Reserved, first.Status);
        Assert.Equal(7999m, first.Total);
        Assert.NotNull(first.OrderId);
        Assert.Equal(ReserveOrderStatus.OutOfStock, second.Status);
        Assert.Equal("Reserved", (await db.SerialisedDevices.SingleAsync(x => x.Id == deviceId)).Status);
        Assert.Single(await db.RetailOrders.Where(x => x.TenantId == tenantId).ToListAsync());
        Assert.Single(await db.InventoryMovements.Where(x => x.DeviceId == deviceId && x.MovementType == "Reserved").ToListAsync());

        var differentTenant = await service.ReserveAsync(request with { TenantId = Guid.NewGuid() }, CancellationToken.None);
        Assert.Equal(ReserveOrderStatus.ProductUnavailable, differentTenant.Status);
    }

    [Fact]
    public async Task ConcurrentReservationsAllowOnlyOneWinner()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var setup = new RetailDbContext(options);
        await setup.Database.MigrateAsync();
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        setup.Tenants.Add(new RetailTenant { Id = tenantId, Name = "Concurrency", Slug = "concurrent-" + tenantId.ToString("N")[..12] });
        setup.CatalogueItems.Add(new CatalogueItem { Id = productId, TenantId = tenantId, Name = "Handset", Category = "Phones", Storage = "256GB", Price = 12500m, IsPublished = true });
        setup.SerialisedDevices.Add(new SerialisedDevice { Id = deviceId, TenantId = tenantId, ProductId = productId,
            Imei = "490154203237519", Status = "Available", ReceivedAt = DateTimeOffset.UtcNow });
        await setup.SaveChangesAsync();

        async Task<ReserveOrderResult> Attempt()
        {
            await using var db = new RetailDbContext(options);
            return await new OrderReservationService(db, TimeProvider.System).ReserveAsync(
                new ReserveOrderRequest(tenantId, productId, deviceId), CancellationToken.None);
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(results.Where(r => r.Status == ReserveOrderStatus.Reserved));
        Assert.Single(results.Where(r => r.Status == ReserveOrderStatus.OutOfStock));
        Assert.Equal(1, await setup.RetailOrders.CountAsync(x => x.DeviceId == deviceId));
    }
}
