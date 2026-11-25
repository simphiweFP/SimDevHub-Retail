using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class ReservationExpiryTests
{
    [Fact]
    public async Task ExpiredReservation_ReleasesDeviceExactlyOnce()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        db.Tenants.Add(new RetailTenant { Id = tenantId, Name = "Expiry Test", Slug = "expiry-" + tenantId.ToString("N")[..14] });
        db.CatalogueItems.Add(new CatalogueItem { Id = productId, TenantId = tenantId, Name = "Device",
            Storage = "128GB", Category = "Phones", Price = 5000m, IsPublished = true });
        db.SerialisedDevices.Add(new SerialisedDevice { Id = deviceId, TenantId = tenantId, ProductId = productId,
            Imei = "490154203237517", Status = "Available", ReceivedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var reserved = await new OrderReservationService(db, TimeProvider.System)
            .ReserveAsync(new ReserveOrderRequest(tenantId, productId, deviceId), CancellationToken.None);
        Assert.Equal(ReserveOrderStatus.Reserved, reserved.Status);
        Assert.NotNull(reserved.OrderId);

        await db.RetailOrders.Where(x => x.Id == reserved.OrderId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReservedUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
        var service = new ReservationExpiryService(db, TimeProvider.System);
        Assert.True(await service.ReleaseExpiredAsync(200, CancellationToken.None) >= 1);
        Assert.Equal(0, await service.ReleaseExpiredAsync(200, CancellationToken.None));

        db.ChangeTracker.Clear();
        Assert.Equal("Expired", (await db.RetailOrders.SingleAsync(x => x.Id == reserved.OrderId)).Status);
        Assert.Equal("Available", (await db.SerialisedDevices.SingleAsync(x => x.Id == deviceId)).Status);
        Assert.Single(await db.InventoryMovements.Where(x => x.DeviceId == deviceId && x.MovementType == "ReservationExpired").ToListAsync());
    }
}
