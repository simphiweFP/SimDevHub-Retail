using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class PayfastSandboxCheckoutStartTests
{
    [Fact]
    public async Task ReservedOrder_UsesDatabasePriceAndIdempotentlyCreatesOneSandboxIntent()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var tenant = Guid.NewGuid();
        var product = Guid.NewGuid();
        var device = Guid.NewGuid();
        db.Tenants.Add(new RetailTenant {
            Id = tenant, Name = "Checkout Store", Slug = "checkout-" + tenant.ToString("N")[..12]
        });
        db.CatalogueItems.Add(new CatalogueItem {
            Id = product, TenantId = tenant, Name = "Test phone", Storage = "256GB",
            Category = "Phones", Price = 6499m, IsPublished = true
        });
        db.SerialisedDevices.Add(new SerialisedDevice {
            Id = device, TenantId = tenant, ProductId = product,
            Imei = "490154203237518", Status = "Available", ReceivedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var reservation = await new OrderReservationService(db, TimeProvider.System)
            .ReserveAsync(new ReserveOrderRequest(tenant, product, device), CancellationToken.None);
        Assert.Equal(ReserveOrderStatus.Reserved, reservation.Status);
        Assert.NotNull(reservation.OrderId);

        var config = Configuration(tenant);
        var source = new PayfastTrustedSourceValidator(config);
        var merchant = new ConfiguredPayfastSandboxMerchant(config);
        var service = new PayfastSandboxCheckoutStart(db, merchant, source, config, TimeProvider.System);
        var request = new StartPayfastSandboxCheckout(
            tenant, reservation.OrderId!.Value, "checkout-first-001");
        var first = await service.StartAsync(request, CancellationToken.None);
        Assert.Equal(PayfastCheckoutStartStatus.Ready, first.Status);
        Assert.NotNull(first.IntentId);
        Assert.NotNull(first.FormFields);
        Assert.Equal("https://sandbox.payfast.co.za/eng/process", first.FormAction);
        Assert.Equal("6499.00", first.FormFields["amount"]);
        Assert.Equal(first.IntentId.Value.ToString("N"), first.FormFields["m_payment_id"]);
        Assert.Equal("10000100", first.FormFields["merchant_id"]);
        Assert.False(first.FormFields.ContainsKey("passphrase"));

        var replay = await service.StartAsync(request, CancellationToken.None);
        Assert.Equal(PayfastCheckoutStartStatus.Replayed, replay.Status);
        Assert.Equal(first.IntentId, replay.IntentId);
        Assert.Equal(first.FormFields["signature"], replay.FormFields!["signature"]);
        Assert.Equal(PayfastCheckoutStartStatus.Conflict,
            (await service.StartAsync(request with { IdempotencyKey = "another-key-001" }, CancellationToken.None)).Status);
        Assert.Equal(PayfastCheckoutStartStatus.Unavailable,
            (await service.StartAsync(request with { TenantId = Guid.NewGuid() }, CancellationToken.None)).Status);
        Assert.Single(await db.PaymentIntents.Where(x => x.OrderId == reservation.OrderId &&
            x.Provider == "PayfastSandbox").ToListAsync());

        // Expiry prevents even a known key from generating a fresh checkout form.
        await db.RetailOrders.Where(x => x.Id == reservation.OrderId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReservedUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal(PayfastCheckoutStartStatus.Expired,
            (await service.StartAsync(request, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task DisabledSandboxOrMerchantMissingKeyCannotStartCheckout()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var tenant = Guid.NewGuid();
        var product = Guid.NewGuid();
        var device = Guid.NewGuid();
        db.Tenants.Add(new RetailTenant {
            Id = tenant, Name = "Disabled Store", Slug = "disabled-" + tenant.ToString("N")[..12]
        });
        db.CatalogueItems.Add(new CatalogueItem {
            Id = product, TenantId = tenant, Name = "Phone", Storage = "64GB",
            Category = "Phones", Price = 1000m, IsPublished = true
        });
        db.SerialisedDevices.Add(new SerialisedDevice {
            Id = device, TenantId = tenant, ProductId = product,
            Imei = "490154203237519", Status = "Available", ReceivedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var reservation = await new OrderReservationService(db, TimeProvider.System)
            .ReserveAsync(new ReserveOrderRequest(tenant, product, device), CancellationToken.None);
        Assert.NotNull(reservation.OrderId);

        var values = ConfigurationValues(tenant);
        values["Payments:PayfastSandbox:EnableCheckout"] = "false";
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var service = new PayfastSandboxCheckoutStart(db,
            new ConfiguredPayfastSandboxMerchant(config),
            new PayfastTrustedSourceValidator(config), config, TimeProvider.System);
        var request = new StartPayfastSandboxCheckout(
            tenant, reservation.OrderId.Value, "disabled-key-0001");
        Assert.Equal(PayfastCheckoutStartStatus.Unavailable,
            (await service.StartAsync(request, CancellationToken.None)).Status);

        values["Payments:PayfastSandbox:EnableCheckout"] = "true";
        values.Remove($"Payments:PayfastSandbox:Merchants:{tenant:N}:MerchantKey");
        config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        service = new PayfastSandboxCheckoutStart(db, new ConfiguredPayfastSandboxMerchant(config),
            new PayfastTrustedSourceValidator(config), config, TimeProvider.System);
        Assert.Equal(PayfastCheckoutStartStatus.Unavailable,
            (await service.StartAsync(request, CancellationToken.None)).Status);
        Assert.Empty(await db.PaymentIntents.Where(x => x.OrderId == reservation.OrderId).ToListAsync());
    }

    private static IConfiguration Configuration(Guid tenant) =>
        new ConfigurationBuilder().AddInMemoryCollection(ConfigurationValues(tenant)).Build();

    private static Dictionary<string, string?> ConfigurationValues(Guid tenant)
    {
        var prefix = $"Payments:PayfastSandbox:Merchants:{tenant:N}";
        return new Dictionary<string, string?>
        {
            ["Payments:PayfastSandbox:EnableCheckout"] = "true",
            ["Payments:PayfastSandbox:EnableWebhook"] = "true",
            ["Payments:PayfastSandbox:TrustedSourceCidrs"] = "197.97.145.144/28",
            ["Payments:PayfastSandbox:ReturnUrl"] = "https://store.example.test/pay/return",
            ["Payments:PayfastSandbox:CancelUrl"] = "https://store.example.test/pay/cancel",
            ["Payments:PayfastSandbox:NotifyUrl"] = "https://store.example.test/api/payments/payfast/sandbox/itn",
            [$"{prefix}:Enabled"] = "true",
            [$"{prefix}:MerchantId"] = "10000100",
            [$"{prefix}:MerchantKey"] = "test-only-sandbox-merchant-key",
            [$"{prefix}:Passphrase"] = "test-only-passphrase"
        };
    }
}
