using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class CustomerSandboxPaymentTests
{
    [Fact]
    public async Task OnlyPersistedOrderOwnerAtMatchingActiveStoreCanStartSandboxCheckout()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var buyer = Guid.NewGuid();
        var otherBuyer = Guid.NewGuid();
        var product = Guid.NewGuid();
        var device = Guid.NewGuid();
        var slug = "pay-owner-" + tenant.ToString("N")[..12];
        db.Tenants.AddRange(
            new RetailTenant { Id = tenant, Name = "Owner Sandbox", Slug = slug },
            new RetailTenant { Id = otherTenant, Name = "Another Store",
                Slug = "pay-other-" + otherTenant.ToString("N")[..12] });
        db.Users.AddRange(
            new RetailUser { Id = buyer, Email = "buyer-pay@example.test" },
            new RetailUser { Id = otherBuyer, Email = "different-pay@example.test" });
        db.ExternalAccounts.AddRange(
            new ExternalAccount { Id = Guid.NewGuid(), UserId = buyer,
                Issuer = "https://id.example.test", Subject = "customer-pay" },
            new ExternalAccount { Id = Guid.NewGuid(), UserId = otherBuyer,
                Issuer = "https://id.example.test", Subject = "other-pay" });
        db.CatalogueItems.Add(new CatalogueItem {
            Id = product, TenantId = tenant, Name = "Test phone",
            Storage = "128GB", Category = "Phones", Price = 2200m, IsPublished = true });
        db.SerialisedDevices.Add(new SerialisedDevice {
            Id = device, TenantId = tenant, ProductId = product,
            Imei = "490154203237518", Status = "Reserved", ReceivedAt = DateTimeOffset.UtcNow });
        var order = Guid.NewGuid();
        db.RetailOrders.Add(new RetailOrder {
            Id = order, TenantId = tenant, ProductId = product, DeviceId = device,
            CustomerUserId = buyer, Status = "AwaitingPayment", UnitPrice = 2200m,
            CreatedAt = DateTimeOffset.UtcNow, ReservedUntil = DateTimeOffset.UtcNow.AddMinutes(15) });
        await db.SaveChangesAsync();

        var settings = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> {
                ["Payments:PayfastSandbox:EnableCustomerCheckout"] = "true"
            }).Build();
        var checkout = new FakeCheckout();
        var service = new CustomerSandboxPayment(db, checkout, settings);
        var request = new CustomerSandboxPaymentRequest("https://id.example.test",
            "customer-pay", slug, order, "customer-sandbox-001");

        Assert.Equal(PayfastCheckoutStartStatus.OrderUnavailable,
            (await service.StartAsync(request with { Subject = "other-pay" },
                CancellationToken.None)).Status);
        Assert.Equal(PayfastCheckoutStartStatus.OrderUnavailable,
            (await service.StartAsync(request with { Issuer = "https://wrong-issuer.test" },
                CancellationToken.None)).Status);
        Assert.Equal(PayfastCheckoutStartStatus.OrderUnavailable,
            (await service.StartAsync(request with {
                StoreSlug = "pay-other-" + otherTenant.ToString("N")[..12]
            }, CancellationToken.None)).Status);
        Assert.Equal(PayfastCheckoutStartStatus.OrderUnavailable,
            (await service.StartAsync(request with { OrderId = Guid.NewGuid() },
                CancellationToken.None)).Status);
        Assert.Equal(PayfastCheckoutStartStatus.Invalid,
            (await service.StartAsync(request with { IdempotencyKey = "!" },
                CancellationToken.None)).Status);
        Assert.Equal(0, checkout.Calls);

        var result = await service.StartAsync(request, CancellationToken.None);
        Assert.Equal(PayfastCheckoutStartStatus.Ready, result.Status);
        Assert.Equal(1, checkout.Calls);
        Assert.Equal(tenant, checkout.LastRequest!.TenantId);
        Assert.Equal(order, checkout.LastRequest.OrderId);
        Assert.Equal("customer-sandbox-001", checkout.LastRequest.IdempotencyKey);

        await db.Users.Where(x => x.Id == buyer)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        Assert.Equal(PayfastCheckoutStartStatus.OrderUnavailable,
            (await service.StartAsync(request, CancellationToken.None)).Status);
        Assert.Equal(1, checkout.Calls);
    }

    [Fact]
    public async Task MissingOptInCannotCallInternalPaymentService()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>()).Build();
        var fake = new FakeCheckout();
        var result = await new CustomerSandboxPayment(db, fake, config).StartAsync(
            new CustomerSandboxPaymentRequest(
                "https://id.example.test", "subject", "valid-store",
                Guid.NewGuid(), "customer-sandbox-001"), CancellationToken.None);
        Assert.Equal(PayfastCheckoutStartStatus.Unavailable, result.Status);
        Assert.Equal(0, fake.Calls);
    }

    private sealed class FakeCheckout : IPayfastSandboxCheckoutStart
    {
        public int Calls { get; private set; }
        public StartPayfastSandboxCheckout? LastRequest { get; private set; }
        public Task<PayfastCheckoutStartResult> StartAsync(
            StartPayfastSandboxCheckout request, CancellationToken ct)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(new PayfastCheckoutStartResult(
                PayfastCheckoutStartStatus.Ready, Guid.NewGuid()));
        }
    }
}
