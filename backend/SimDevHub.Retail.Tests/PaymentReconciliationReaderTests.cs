using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class PaymentReconciliationReaderTests
{
    [Fact]
    public async Task ReviewQueue_ContainsOnlyOwnTenantReviewEvents()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();
        var alpha = Guid.NewGuid();
        var beta = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var deviceA = Guid.NewGuid();
        var deviceB = Guid.NewGuid();
        var orderA = Guid.NewGuid();
        var orderB = Guid.NewGuid();
        db.Tenants.AddRange(
            new RetailTenant { Id = alpha, Name = "Review Alpha", Slug = "review-a-" + alpha.ToString("N")[..12] },
            new RetailTenant { Id = beta, Name = "Review Beta", Slug = "review-b-" + beta.ToString("N")[..12] });
        db.CatalogueItems.AddRange(
            new CatalogueItem { Id = productA, TenantId = alpha, Name = "A", Storage = "64GB", Category = "Phones", Price = 100m, IsPublished = true },
            new CatalogueItem { Id = productB, TenantId = beta, Name = "B", Storage = "64GB", Category = "Phones", Price = 200m, IsPublished = true });
        db.SerialisedDevices.AddRange(
            new SerialisedDevice { Id = deviceA, TenantId = alpha, ProductId = productA, Imei = "490154203237518", Status = "Reserved", ReceivedAt = DateTimeOffset.UtcNow },
            new SerialisedDevice { Id = deviceB, TenantId = beta, ProductId = productB, Imei = "490154203237519", Status = "Reserved", ReceivedAt = DateTimeOffset.UtcNow });
        db.RetailOrders.AddRange(
            new RetailOrder { Id = orderA, TenantId = alpha, ProductId = productA, DeviceId = deviceA, UnitPrice = 100m, Status = "AwaitingPayment", CreatedAt = DateTimeOffset.UtcNow, ReservedUntil = DateTimeOffset.UtcNow.AddMinutes(10) },
            new RetailOrder { Id = orderB, TenantId = beta, ProductId = productB, DeviceId = deviceB, UnitPrice = 200m, Status = "AwaitingPayment", CreatedAt = DateTimeOffset.UtcNow, ReservedUntil = DateTimeOffset.UtcNow.AddMinutes(10) });
        db.PaymentEvents.AddRange(
            new PaymentEvent { Id = Guid.NewGuid(), TenantId = alpha, OrderId = orderA, Provider = "Demo", ProviderEventId = Guid.NewGuid().ToString("N"), Outcome = "ReviewRequired", Amount = 90m, Currency = "ZAR", RecordedAt = DateTimeOffset.UtcNow },
            new PaymentEvent { Id = Guid.NewGuid(), TenantId = alpha, OrderId = orderA, Provider = "Demo", ProviderEventId = Guid.NewGuid().ToString("N"), Outcome = "Paid", Amount = 100m, Currency = "ZAR", RecordedAt = DateTimeOffset.UtcNow },
            new PaymentEvent { Id = Guid.NewGuid(), TenantId = beta, OrderId = orderB, Provider = "Demo", ProviderEventId = Guid.NewGuid().ToString("N"), Outcome = "ReviewRequired", Amount = 190m, Currency = "ZAR", RecordedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var reader = new PaymentReconciliationReader(db);
        var queue = await reader.GetUnresolvedAsync(alpha, 20, CancellationToken.None);
        Assert.Single(queue);
        Assert.Equal(orderA, queue[0].OrderId);
        Assert.Equal(90m, queue[0].Amount);
        Assert.Empty(await reader.GetUnresolvedAsync(Guid.Empty, 20, CancellationToken.None));
        Assert.Empty(await reader.GetUnresolvedAsync(alpha, 0, CancellationToken.None));
    }
}
