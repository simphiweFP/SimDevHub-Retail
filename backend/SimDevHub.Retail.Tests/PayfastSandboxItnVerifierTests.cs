using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class PayfastSandboxItnVerifierTests
{
    [Fact]
    public async Task ItnRequiresSignatureTrustedSourceServerConfirmationAndMatchingIntent()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;

        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var tenant = Guid.NewGuid();
        var product = Guid.NewGuid();
        var device = Guid.NewGuid();
        var order = Guid.NewGuid();
        var intentId = Guid.NewGuid();
        db.Tenants.Add(new RetailTenant { Id = tenant, Name = "ITN Test", Slug = "itn-" + tenant.ToString("N")[..12] });
        db.CatalogueItems.Add(new CatalogueItem
        {
            Id = product, TenantId = tenant, Name = "Demo Phone",
            Category = "Phones", Storage = "128GB", Price = 6499m, IsPublished = true
        });
        db.SerialisedDevices.Add(new SerialisedDevice
        {
            Id = device, TenantId = tenant, ProductId = product,
            Imei = "490154203237518", Status = "Reserved", ReceivedAt = DateTimeOffset.UtcNow
        });
        db.RetailOrders.Add(new RetailOrder
        {
            Id = order, TenantId = tenant, ProductId = product, DeviceId = device,
            UnitPrice = 6499m, Status = "AwaitingPayment",
            CreatedAt = DateTimeOffset.UtcNow, ReservedUntil = DateTimeOffset.UtcNow.AddMinutes(10)
        });
        db.PaymentIntents.Add(new PaymentIntent
        {
            Id = intentId, TenantId = tenant, OrderId = order,
            Provider = "PayfastSandbox", Status = "AwaitingVerification",
            IdempotencyKey = "itn-test-12345678", Currency = "ZAR",
            Amount = 6499m, CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var source = new MutableSource { Allowed = true };
        var server = new MutableConfirmation { Allowed = true };
        var merchant = new FakeMerchant(tenant);
        var verifier = new PayfastSandboxItnVerifier(db, source, server, merchant);
        var body = SignedForm(intentId.ToString("N"), "6499.00", "10000100");
        var notification = new GatewayNotification("PayfastSandbox", body, new Dictionary<string, string>());

        var accepted = await verifier.VerifyAsync(notification, CancellationToken.None);
        Assert.NotNull(accepted);
        Assert.Equal(tenant, accepted.TenantId);
        Assert.Equal(order, accepted.OrderId);
        Assert.Equal(intentId, accepted.PaymentIntentId);
        Assert.Equal("PayfastSandbox", accepted.Provider);
        Assert.Equal(6499m, accepted.Amount);
        Assert.Equal(tenant, merchant.LastTenantId);

        // No tenant merchant configuration is allowed to fall back to a global
        // credential. Both absence and incorrect merchant ownership must fail.
        merchant.Credentials = null;
        Assert.Null(await verifier.VerifyAsync(notification, CancellationToken.None));
        merchant.Credentials = new("20000200", "test-only-passphrase");
        Assert.Null(await verifier.VerifyAsync(notification, CancellationToken.None));
        merchant.Credentials = new("10000100", "wrong-tenant-passphrase");
        Assert.Null(await verifier.VerifyAsync(notification, CancellationToken.None));
        merchant.Credentials = new("10000100", "test-only-passphrase");

        source.Allowed = false;
        Assert.Null(await verifier.VerifyAsync(notification, CancellationToken.None));
        source.Allowed = true;
        server.Allowed = false;
        Assert.Null(await verifier.VerifyAsync(notification, CancellationToken.None));
        server.Allowed = true;

        Assert.Null(await verifier.VerifyAsync(notification with
        {
            Body = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(body).Replace("6499.00", "6999.00"))
        }, CancellationToken.None));
        Assert.Null(await verifier.VerifyAsync(notification with
        {
            Body = SignedForm(intentId.ToString("N"), "6499.01", "10000100")
        }, CancellationToken.None));
        Assert.Null(await verifier.VerifyAsync(notification with
        {
            Body = SignedForm(intentId.ToString("N"), "6499.00", "99999999")
        }, CancellationToken.None));
        Assert.Null(await verifier.VerifyAsync(notification with
        {
            Body = SignedForm(Guid.NewGuid().ToString("N"), "6499.00", "10000100")
        }, CancellationToken.None));
        Assert.Null(await verifier.VerifyAsync(notification with
        {
            Body = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(body) + "&merchant_id=10000100")
        }, CancellationToken.None));
        Assert.Equal("AwaitingPayment", (await db.RetailOrders.AsNoTracking().SingleAsync(x => x.Id == order)).Status);
        Assert.Equal("Reserved", (await db.SerialisedDevices.AsNoTracking().SingleAsync(x => x.Id == device)).Status);
    }

    private static byte[] SignedForm(string intent, string gross, string merchantId)
    {
        const string passphrase = "test-only-passphrase";
        var fields = new (string Key, string Value)[]
        {
            ("m_payment_id", intent),
            ("pf_payment_id", "itn-test-event-123"),
            ("payment_status", "COMPLETE"),
            ("amount_gross", gross),
            ("merchant_id", merchantId)
        };
        var canonical = string.Join("&", fields.Select(x =>
            x.Key + "=" + WebUtility.UrlEncode(x.Value)));
        var signature = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(
            canonical + "&passphrase=" + WebUtility.UrlEncode(passphrase)))).ToLowerInvariant();
        return Encoding.UTF8.GetBytes(canonical + "&signature=" + signature);
    }

    private sealed class FakeMerchant(Guid expectedTenant) : IPayfastSandboxMerchant
    {
        public Guid? LastTenantId { get; private set; }
        public PayfastMerchantSettings? Credentials { get; set; } =
            new("10000100", "test-only-passphrase");

        public Task<PayfastMerchantSettings?> GetCredentialsAsync(Guid tenantId, CancellationToken ct)
        {
            LastTenantId = tenantId;
            return Task.FromResult(tenantId == expectedTenant ? Credentials : null);
        }
    }

    private sealed class MutableSource : IPayfastSourceValidator
    {
        public bool Allowed { get; set; }
        public Task<bool> IsTrustedSourceAsync(GatewayNotification _, CancellationToken ct) =>
            Task.FromResult(Allowed);
    }

    private sealed class MutableConfirmation : IPayfastServerConfirmation
    {
        public bool Allowed { get; set; }
        public Task<bool> ValidateAsync(ReadOnlyMemory<byte> _, CancellationToken ct) =>
            Task.FromResult(Allowed);
    }
}
