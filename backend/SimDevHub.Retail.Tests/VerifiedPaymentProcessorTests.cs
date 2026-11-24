using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class VerifiedPaymentProcessorTests
{
    [Fact]
    public async Task VerifiedPayment_IsIdempotentAndAllocatesOnce()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        db.Tenants.Add(new RetailTenant { Id = tenantId, Name = "Payment Demo", Slug = "pay-" + tenantId.ToString("N")[..14] });
        db.CatalogueItems.Add(new CatalogueItem { Id = productId, TenantId = tenantId, Name = "Phone",
            Storage = "256GB", Category = "Phones", Price = 8999m, IsPublished = true });
        db.SerialisedDevices.Add(new SerialisedDevice { Id = deviceId, TenantId = tenantId, ProductId = productId,
            Imei = "490154203237518", Status = "Available", ReceivedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var reserve = await new OrderReservationService(db, TimeProvider.System)
            .ReserveAsync(new ReserveOrderRequest(tenantId, productId, deviceId), CancellationToken.None);
        Assert.Equal(ReserveOrderStatus.Reserved, reserve.Status);
        Assert.NotNull(reserve.OrderId);

        var intentId = Guid.NewGuid();
        db.PaymentIntents.Add(new PaymentIntent
        {
            Id = intentId, TenantId = tenantId, OrderId = reserve.OrderId!.Value,
            Provider = "TestProvider", IdempotencyKey = Guid.NewGuid().ToString("N"),
            Status = "AwaitingVerification", Amount = 8999m, Currency = "ZAR", CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var process = new VerifiedPaymentProcessor(db, TimeProvider.System);
        var evt = new VerifiedPaymentEvent(tenantId, reserve.OrderId.Value, intentId, "TestProvider", Guid.NewGuid().ToString("N"), 8999m, "ZAR");
        Assert.Equal(PaymentConfirmationResult.Invalid, await process.ConfirmAsync(
            evt with { Currency = "USD" }, CancellationToken.None));
        Assert.Equal(PaymentConfirmationResult.Invalid, await process.ConfirmAsync(
            evt with { PaymentIntentId = Guid.NewGuid() }, CancellationToken.None));
        Assert.Equal(PaymentConfirmationResult.Invalid, await process.ConfirmAsync(
            evt with { Provider = "OtherProvider" }, CancellationToken.None));
        Assert.Equal(PaymentConfirmationResult.Invalid, await process.ConfirmAsync(
            evt with { TenantId = Guid.NewGuid() }, CancellationToken.None));
        Assert.Equal(PaymentConfirmationResult.Paid, await process.ConfirmAsync(evt, CancellationToken.None));
        Assert.Equal(PaymentConfirmationResult.Duplicate, await process.ConfirmAsync(evt, CancellationToken.None));
        Assert.Equal("Paid", (await db.RetailOrders.AsNoTracking().SingleAsync(o => o.Id == reserve.OrderId)).Status);
        Assert.Equal("Allocated", (await db.SerialisedDevices.AsNoTracking().SingleAsync(d => d.Id == deviceId)).Status);
        Assert.Equal("Succeeded", (await db.PaymentIntents.AsNoTracking().SingleAsync(i => i.Id == intentId)).Status);
        Assert.Single(await db.InventoryMovements.Where(m => m.DeviceId == deviceId && m.MovementType == "PaymentAllocated").ToListAsync());
        Assert.Single(await db.PaymentEvents.Where(e => e.OrderId == reserve.OrderId && e.Outcome == "Paid").ToListAsync());

        // A different provider notification cannot allocate again.
        Assert.Equal(PaymentConfirmationResult.ReviewRequired, await process.ConfirmAsync(
            evt with { ProviderEventId = Guid.NewGuid().ToString("N") }, CancellationToken.None));
        Assert.Single(await db.InventoryMovements.Where(m => m.DeviceId == deviceId && m.MovementType == "PaymentAllocated").ToListAsync());
    }

    [Fact]
    public async Task MismatchedAndLatePayments_AreRecordedButNotFulfilled()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        db.Tenants.Add(new RetailTenant { Id = tenantId, Name = "Payment Exceptions", Slug = "exception-" + tenantId.ToString("N")[..12] });
        db.CatalogueItems.Add(new CatalogueItem { Id = productId, TenantId = tenantId, Name = "Test",
            Storage = "128GB", Category = "Phones", Price = 1000m, IsPublished = true });
        db.SerialisedDevices.Add(new SerialisedDevice { Id = deviceId, TenantId = tenantId, ProductId = productId,
            Imei = "490154203237519", Status = "Available", ReceivedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var reserve = await new OrderReservationService(db, TimeProvider.System)
            .ReserveAsync(new ReserveOrderRequest(tenantId, productId, deviceId), CancellationToken.None);
        Assert.Equal(ReserveOrderStatus.Reserved, reserve.Status);
        var intentId = Guid.NewGuid();
        db.PaymentIntents.Add(new PaymentIntent
        {
            Id = intentId, TenantId = tenantId, OrderId = reserve.OrderId!.Value,
            Provider = "TestProvider", IdempotencyKey = Guid.NewGuid().ToString("N"),
            Status = "AwaitingVerification", Amount = 1000m, Currency = "ZAR", CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var processor = new VerifiedPaymentProcessor(db, TimeProvider.System);
        var evt = new VerifiedPaymentEvent(tenantId, reserve.OrderId!.Value, intentId, "TestProvider", Guid.NewGuid().ToString("N"), 900m, "ZAR");
        Assert.Equal(PaymentConfirmationResult.ReviewRequired, await processor.ConfirmAsync(evt, CancellationToken.None));
        Assert.Equal("AwaitingPayment", (await db.RetailOrders.AsNoTracking().SingleAsync(o => o.Id == reserve.OrderId)).Status);
        Assert.Equal("Reserved", (await db.SerialisedDevices.AsNoTracking().SingleAsync(d => d.Id == deviceId)).Status);

        await db.RetailOrders.Where(o => o.Id == reserve.OrderId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.ReservedUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal(PaymentConfirmationResult.ReviewRequired, await processor.ConfirmAsync(
            evt with { ProviderEventId = Guid.NewGuid().ToString("N"), Amount = 1000m }, CancellationToken.None));
        Assert.Empty(await db.InventoryMovements.Where(m => m.DeviceId == deviceId && m.MovementType == "PaymentAllocated").ToListAsync());
        Assert.Equal(2, await db.PaymentEvents.CountAsync(e => e.OrderId == reserve.OrderId && e.Outcome == "ReviewRequired"));
    }
}
