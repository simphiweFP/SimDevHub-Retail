using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class ReservationExpirySafetyTests
{
    [Fact]
    public async Task BatchExpiresOnlyOverdueUnpaidOrders_LeavesPaidAndFutureStockUntouched()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;

        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var tenant = Guid.NewGuid();
        var product = Guid.NewGuid();
        var overdueDevice = Guid.NewGuid();
        var futureDevice = Guid.NewGuid();
        var paidDevice = Guid.NewGuid();
        db.Tenants.Add(new RetailTenant { Id = tenant, Name = "Expiry Safety",
            Slug = "expiry-safe-" + tenant.ToString("N")[..12] });
        db.CatalogueItems.Add(new CatalogueItem { Id = product, TenantId = tenant,
            Name = "Test Device", Storage = "128GB", Category = "Phones", Price = 3000m, IsPublished = true });
        var now = DateTimeOffset.UtcNow;
        db.SerialisedDevices.AddRange(
            new SerialisedDevice { Id = overdueDevice, TenantId = tenant, ProductId = product,
                Imei = "490154203237510", Status = "Reserved", ReceivedAt = now },
            new SerialisedDevice { Id = futureDevice, TenantId = tenant, ProductId = product,
                Imei = "490154203237511", Status = "Reserved", ReceivedAt = now },
            new SerialisedDevice { Id = paidDevice, TenantId = tenant, ProductId = product,
                Imei = "490154203237512", Status = "Allocated", ReceivedAt = now });
        var overdueOrder = Guid.NewGuid();
        var futureOrder = Guid.NewGuid();
        var paidOrder = Guid.NewGuid();
        db.RetailOrders.AddRange(
            new RetailOrder { Id = overdueOrder, TenantId = tenant, ProductId = product,
                DeviceId = overdueDevice, Status = "AwaitingPayment", UnitPrice = 3000m,
                CreatedAt = now.AddMinutes(-30), ReservedUntil = now.AddMinutes(-15) },
            new RetailOrder { Id = futureOrder, TenantId = tenant, ProductId = product,
                DeviceId = futureDevice, Status = "AwaitingPayment", UnitPrice = 3000m,
                CreatedAt = now, ReservedUntil = now.AddMinutes(15) },
            new RetailOrder { Id = paidOrder, TenantId = tenant, ProductId = product,
                DeviceId = paidDevice, Status = "Paid", UnitPrice = 3000m,
                CreatedAt = now.AddMinutes(-30), ReservedUntil = now.AddMinutes(-15) });
        await db.SaveChangesAsync();

        var count = await new ReservationExpiryService(db, TimeProvider.System)
            .ReleaseExpiredAsync(1, CancellationToken.None);
        Assert.Equal(1, count);
        Assert.Equal("Expired", (await db.RetailOrders.AsNoTracking()
            .SingleAsync(x => x.Id == overdueOrder)).Status);
        Assert.Equal("Available", (await db.SerialisedDevices.AsNoTracking()
            .SingleAsync(x => x.Id == overdueDevice)).Status);
        Assert.Equal("AwaitingPayment", (await db.RetailOrders.AsNoTracking()
            .SingleAsync(x => x.Id == futureOrder)).Status);
        Assert.Equal("Reserved", (await db.SerialisedDevices.AsNoTracking()
            .SingleAsync(x => x.Id == futureDevice)).Status);
        Assert.Equal("Paid", (await db.RetailOrders.AsNoTracking()
            .SingleAsync(x => x.Id == paidOrder)).Status);
        Assert.Equal("Allocated", (await db.SerialisedDevices.AsNoTracking()
            .SingleAsync(x => x.Id == paidDevice)).Status);
        Assert.Equal(0, await new ReservationExpiryService(db, TimeProvider.System)
            .ReleaseExpiredAsync(10, CancellationToken.None));
        Assert.Equal(1, await db.InventoryMovements.CountAsync(x =>
            x.DeviceId == overdueDevice && x.MovementType == "ReservationExpired"));
        Assert.Equal(0, await db.InventoryMovements.CountAsync(x =>
            (x.DeviceId == futureDevice || x.DeviceId == paidDevice) &&
            x.MovementType == "ReservationExpired"));
    }

    [Fact]
    public async Task ParallelExpiryWorkersCannotReleaseSameOrderTwice()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var setup = new RetailDbContext(options);
        await setup.Database.MigrateAsync();

        var tenant = Guid.NewGuid();
        var product = Guid.NewGuid();
        var device = Guid.NewGuid();
        var order = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        setup.Tenants.Add(new RetailTenant { Id = tenant, Name = "Expiry Race",
            Slug = "expiry-race-" + tenant.ToString("N")[..12] });
        setup.CatalogueItems.Add(new CatalogueItem { Id = product, TenantId = tenant,
            Name = "Device", Category = "Phones", Storage = "64GB", Price = 2500m, IsPublished = true });
        setup.SerialisedDevices.Add(new SerialisedDevice { Id = device, TenantId = tenant,
            ProductId = product, Imei = "490154203237513", Status = "Reserved", ReceivedAt = now });
        setup.RetailOrders.Add(new RetailOrder { Id = order, TenantId = tenant,
            ProductId = product, DeviceId = device, Status = "AwaitingPayment", UnitPrice = 2500m,
            CreatedAt = now.AddMinutes(-30), ReservedUntil = now.AddMinutes(-10) });
        await setup.SaveChangesAsync();

        async Task<int> Run()
        {
            await using var db = new RetailDbContext(options);
            return await new ReservationExpiryService(db, TimeProvider.System)
                .ReleaseExpiredAsync(200, CancellationToken.None);
        }

        var outcomes = await Task.WhenAll(Run(), Run());
        // This test database is shared with other integration tests; another
        // expired order can be swept in the same run. Verify this device's
        // release exactly once instead of assuming the global sweep count.
        Assert.True(outcomes.Sum() >= 1);
        Assert.Equal(1, await setup.InventoryMovements.CountAsync(x =>
            x.DeviceId == device && x.MovementType == "ReservationExpired"));
        Assert.Equal("Available", (await setup.SerialisedDevices.AsNoTracking()
            .SingleAsync(x => x.Id == device)).Status);
    }
}
