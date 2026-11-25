using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class PaymentIntentServiceTests
{
    [Fact]
    public async Task IntentIsServerPricedIdempotentAndScopedToOrder()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        db.Tenants.Add(new RetailTenant { Id = tenantId, Name = "Intent Store", Slug = "intent-" + tenantId.ToString("N")[..15] });
        db.CatalogueItems.Add(new CatalogueItem { Id = productId, TenantId = tenantId, Name = "Handset",
            Storage = "128GB", Category = "Phones", Price = 11000m, IsPublished = true });
        db.SerialisedDevices.Add(new SerialisedDevice { Id = deviceId, TenantId = tenantId, ProductId = productId,
            Imei = "490154203237518", Status = "Available", ReceivedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var reservation = await new OrderReservationService(db, TimeProvider.System)
            .ReserveAsync(new ReserveOrderRequest(tenantId, productId, deviceId), CancellationToken.None);
        Assert.Equal(ReserveOrderStatus.Reserved, reservation.Status);
        Assert.NotNull(reservation.OrderId);

        var service = new PaymentIntentService(db, TimeProvider.System);
        var request = new CreatePaymentIntentRequest(tenantId, reservation.OrderId.Value, "checkout-intent-001", "SandboxPlaceholder");
        var first = await service.CreateAsync(request, CancellationToken.None);
        var retry = await service.CreateAsync(request, CancellationToken.None);
        Assert.Equal(CreatePaymentIntentStatus.Created, first.Status);
        Assert.Equal(CreatePaymentIntentStatus.Replayed, retry.Status);
        Assert.Equal(first.IntentId, retry.IntentId);
        Assert.Equal(11000m, first.Amount);
        Assert.Equal("ZAR", first.Currency);
        Assert.Equal(CreatePaymentIntentStatus.ProviderUnavailable,
            (await service.CreateAsync(request with { Provider = "UnknownProvider" }, CancellationToken.None)).Status);
        Assert.Equal(CreatePaymentIntentStatus.OrderUnavailable,
            (await service.CreateAsync(request with { TenantId = Guid.NewGuid(), IdempotencyKey = "checkout-intent-002" },
                CancellationToken.None)).Status);
        Assert.Equal(1, await db.PaymentIntents.CountAsync(x => x.OrderId == reservation.OrderId));

        await db.RetailOrders.Where(x => x.Id == reservation.OrderId)
            .ExecuteUpdateAsync(x => x.SetProperty(o => o.ReservedUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal(CreatePaymentIntentStatus.Expired,
            (await service.CreateAsync(request with { IdempotencyKey = "checkout-intent-003" }, CancellationToken.None)).Status);
        Assert.Equal(1, await db.PaymentIntents.CountAsync(x => x.TenantId == tenantId));
    }

    [Fact]
    public async Task ConcurrentRequestsUseOneIdempotencyKey()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var setup = new RetailDbContext(options);
        await setup.Database.MigrateAsync();
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        setup.Tenants.Add(new RetailTenant { Id = tenantId, Name = "Race Store", Slug = "intent-race-" + tenantId.ToString("N")[..12] });
        setup.CatalogueItems.Add(new CatalogueItem { Id = productId, TenantId = tenantId, Name = "Device",
            Storage = "64GB", Category = "Phones", Price = 4500m, IsPublished = true });
        setup.SerialisedDevices.Add(new SerialisedDevice { Id = deviceId, TenantId = tenantId, ProductId = productId,
            Imei = "490154203237519", Status = "Available", ReceivedAt = DateTimeOffset.UtcNow });
        await setup.SaveChangesAsync();
        var reservation = await new OrderReservationService(setup, TimeProvider.System)
            .ReserveAsync(new ReserveOrderRequest(tenantId, productId, deviceId), CancellationToken.None);
        Assert.NotNull(reservation.OrderId);

        async Task<PaymentIntentResult> Attempt()
        {
            await using var db = new RetailDbContext(options);
            return await new PaymentIntentService(db, TimeProvider.System).CreateAsync(
                new CreatePaymentIntentRequest(tenantId, reservation.OrderId.Value, "parallel-intent-key-001", "SandboxPlaceholder"),
                CancellationToken.None);
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(results.Where(x => x.Status == CreatePaymentIntentStatus.Created));
        Assert.Single(results.Where(x => x.Status == CreatePaymentIntentStatus.Replayed));
        Assert.Equal(results[0].IntentId, results[1].IntentId);
        Assert.Equal(1, await setup.PaymentIntents.CountAsync(x => x.OrderId == reservation.OrderId));
    }
}
