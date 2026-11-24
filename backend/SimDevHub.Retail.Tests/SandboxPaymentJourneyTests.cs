using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

/// <summary>
/// A real PostgreSQL transaction test using a test-only fake gateway.
/// No external payment network, merchant account or real charging is involved.
/// </summary>
public sealed class SandboxPaymentJourneyTests
{
    [Fact]
    public async Task ReservedDevice_IsAllocatedOnce_AfterAuthenticatedTestNotification()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var tenant = Guid.NewGuid();
        var product = Guid.NewGuid();
        var device = Guid.NewGuid();
        db.Tenants.Add(new RetailTenant
        {
            Id = tenant, Name = "Sandbox Journey", Slug = "journey-" + tenant.ToString("N")[..14]
        });
        db.CatalogueItems.Add(new CatalogueItem
        {
            Id = product, TenantId = tenant, Name = "Example Phone", Storage = "128GB",
            Category = "Phones", Price = 6499m, IsPublished = true
        });
        db.SerialisedDevices.Add(new SerialisedDevice
        {
            Id = device, TenantId = tenant, ProductId = product,
            Imei = "490154203237518", Status = "Available", ReceivedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var order = await new OrderReservationService(db, TimeProvider.System).ReserveAsync(
            new ReserveOrderRequest(tenant, product, device), CancellationToken.None);
        Assert.Equal(ReserveOrderStatus.Reserved, order.Status);
        Assert.NotNull(order.OrderId);

        var intent = await new PaymentIntentService(db, TimeProvider.System).CreateAsync(
            new CreatePaymentIntentRequest(
                tenant, order.OrderId!.Value, "journey-" + Guid.NewGuid().ToString("N"), "SandboxPlaceholder"),
            CancellationToken.None);
        Assert.Equal(CreatePaymentIntentStatus.Created, intent.Status);
        Assert.NotNull(intent.IntentId);

        // This state transition is ONLY a test substitute for future genuine
        // hosted-checkout creation with an independently verified provider.
        await db.PaymentIntents.Where(x => x.Id == intent.IntentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "AwaitingVerification"));

        var verified = new VerifiedPaymentEvent(
            tenant, order.OrderId.Value, intent.IntentId!.Value,
            "TestGateway", "sandbox-" + Guid.NewGuid().ToString("N"), 6499m, "ZAR");

        // A real gateway must match the intent's provider. The fixture tests
        // this invariant first, then switches the intent to the fake gateway.
        var coordinator = new GatewayNotificationCoordinator(
            new IPaymentGatewayVerifier[] { new SignedFixtureVerifier(verified) },
            new VerifiedPaymentProcessor(db, TimeProvider.System));
        var body = Encoding.UTF8.GetBytes("sandbox-completed:" + order.OrderId.Value.ToString("N"));
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("fixture-test-only-secret"), body));
        var notification = new GatewayNotification(
            "TestGateway", body,
            new Dictionary<string, string> { ["X-Fixture-Signature"] = signature });

        Assert.Equal(GatewayNotificationStatus.VerificationFailed,
            (await coordinator.HandleAsync(notification with { Body = Encoding.UTF8.GetBytes("forged") },
                CancellationToken.None)).Status);
        Assert.Equal(GatewayNotificationStatus.Processed,
            (await coordinator.HandleAsync(notification, CancellationToken.None)).Status);
        // A trusted notification from the wrong provider must never allocate.
        Assert.Equal("Reserved", (await db.SerialisedDevices.AsNoTracking()
            .SingleAsync(d => d.Id == device)).Status);

        await db.PaymentIntents.Where(x => x.Id == intent.IntentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Provider, "TestGateway"));
        var paid = await coordinator.HandleAsync(notification, CancellationToken.None);
        Assert.Equal(PaymentConfirmationResult.Paid, paid.PaymentResult);
        var repeat = await coordinator.HandleAsync(notification, CancellationToken.None);
        Assert.Equal(PaymentConfirmationResult.Duplicate, repeat.PaymentResult);

        Assert.Equal("Paid", (await db.RetailOrders.AsNoTracking()
            .SingleAsync(o => o.Id == order.OrderId)).Status);
        Assert.Equal("Allocated", (await db.SerialisedDevices.AsNoTracking()
            .SingleAsync(d => d.Id == device)).Status);
        Assert.Equal("Succeeded", (await db.PaymentIntents.AsNoTracking()
            .SingleAsync(i => i.Id == intent.IntentId)).Status);
        Assert.Single(await db.PaymentEvents.Where(x =>
            x.OrderId == order.OrderId && x.Outcome == "Paid").ToListAsync());
        Assert.Single(await db.InventoryMovements.Where(x =>
            x.DeviceId == device && x.MovementType == "PaymentAllocated").ToListAsync());
    }

    private sealed class SignedFixtureVerifier(VerifiedPaymentEvent expected) : IPaymentGatewayVerifier
    {
        public string Provider => "TestGateway";

        public Task<VerifiedPaymentEvent?> VerifyAsync(
            GatewayNotification notification, CancellationToken ct)
        {
            if (!notification.Headers.TryGetValue("X-Fixture-Signature", out var submitted) ||
                submitted.Length != 64 || !submitted.All(Uri.IsHexDigit))
                return Task.FromResult<VerifiedPaymentEvent?>(null);
            var actual = Convert.FromHexString(submitted);
            var signature = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes("fixture-test-only-secret"), notification.Body.Span);
            return Task.FromResult<VerifiedPaymentEvent?>(
                CryptographicOperations.FixedTimeEquals(actual, signature) ? expected : null);
        }
    }
}
