using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class CustomerOrderAccessTests
{
    [Fact]
    public async Task OnlyAuthenticatedOwnerCanSeeOrdersAcrossStoresAndUsers()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var deviceA = Guid.NewGuid();
        var deviceB = Guid.NewGuid();
        var slugA = "owner-" + tenantA.ToString("N")[..12];
        var slugB = "owner-" + tenantB.ToString("N")[..12];

        db.Tenants.AddRange(
            new RetailTenant { Id = tenantA, Name = "Tenant A", Slug = slugA },
            new RetailTenant { Id = tenantB, Name = "Tenant B", Slug = slugB });
        db.Users.AddRange(
            new RetailUser { Id = customerA, Email = "buyer-a@example.test" },
            new RetailUser { Id = customerB, Email = "buyer-b@example.test" });
        db.ExternalAccounts.AddRange(
            new ExternalAccount { Id = Guid.NewGuid(), UserId = customerA,
                Issuer = "https://issuer.example.test", Subject = "buyer-a" },
            new ExternalAccount { Id = Guid.NewGuid(), UserId = customerB,
                Issuer = "https://issuer.example.test", Subject = "buyer-b" });
        db.CatalogueItems.AddRange(
            new CatalogueItem { Id = productA, TenantId = tenantA,
                Name = "Phone A", Storage = "128GB", Category = "Phones", Price = 7000m },
            new CatalogueItem { Id = productB, TenantId = tenantB,
                Name = "Phone B", Storage = "128GB", Category = "Phones", Price = 8000m });
        db.SerialisedDevices.AddRange(
            new SerialisedDevice { Id = deviceA, TenantId = tenantA, ProductId = productA,
                Imei = "490154203237518", Status = "Reserved", ReceivedAt = DateTimeOffset.UtcNow },
            new SerialisedDevice { Id = deviceB, TenantId = tenantB, ProductId = productB,
                Imei = "490154203237519", Status = "Reserved", ReceivedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        RetailOrder Order(Guid tenant, Guid product, Guid device, Guid? owner) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenant, ProductId = product, DeviceId = device,
            CustomerUserId = owner, Status = "AwaitingPayment", UnitPrice = 7000m,
            CreatedAt = DateTimeOffset.UtcNow, ReservedUntil = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        var ownedA = Order(tenantA, productA, deviceA, customerA);
        var unownedA = Order(tenantA, productA, deviceA, null);
        var otherUser = Order(tenantA, productA, deviceA, customerB);
        var otherStore = Order(tenantB, productB, deviceB, customerA);
        db.RetailOrders.AddRange(ownedA, unownedA, otherUser, otherStore);
        await db.SaveChangesAsync();

        var access = new CustomerOrderAccess(db);
        var mine = await access.GetOwnedOrdersAsync("https://issuer.example.test", "buyer-a", slugA, 50,
            CancellationToken.None);
        Assert.Single(mine);
        Assert.Equal(ownedA.Id, mine[0].OrderId);
        Assert.Equal("ZAR", mine[0].Currency);

        Assert.True(await access.OwnsOrderAsync("https://issuer.example.test", "buyer-a",
            tenantA, ownedA.Id, CancellationToken.None));
        Assert.False(await access.OwnsOrderAsync("https://issuer.example.test", "buyer-b",
            tenantA, ownedA.Id, CancellationToken.None));
        Assert.False(await access.OwnsOrderAsync("https://issuer.example.test", "buyer-a",
            tenantB, ownedA.Id, CancellationToken.None));
        Assert.False(await access.OwnsOrderAsync("https://issuer.example.test", "buyer-a",
            tenantA, unownedA.Id, CancellationToken.None));
        Assert.Empty(await access.GetOwnedOrdersAsync("https://issuer.example.test",
            "unknown", slugA, 50, CancellationToken.None));
        Assert.Empty(await access.GetOwnedOrdersAsync("https://other-issuer.example.test",
            "buyer-a", slugA, 50, CancellationToken.None));
        Assert.Empty(await access.GetOwnedOrdersAsync("https://issuer.example.test",
            "buyer-a", slugA, 101, CancellationToken.None));

        await db.Users.Where(x => x.Id == customerA)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        Assert.Empty(await access.GetOwnedOrdersAsync("https://issuer.example.test",
            "buyer-a", slugA, 50, CancellationToken.None));
        Assert.False(await access.OwnsOrderAsync("https://issuer.example.test", "buyer-a",
            tenantA, ownedA.Id, CancellationToken.None));
    }
}
