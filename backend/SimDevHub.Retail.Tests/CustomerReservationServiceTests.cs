using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class CustomerReservationServiceTests
{
    [Fact]
    public async Task VerifiedCustomerReservesOneDeviceAndOwnsResultingOrder()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var tenantId = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var buyer = Guid.NewGuid();
        var product = Guid.NewGuid();
        var device = Guid.NewGuid();
        var slug = "buyer-reserve-" + tenantId.ToString("N")[..12];
        db.Tenants.AddRange(
            new RetailTenant { Id = tenantId, Name = "Buyer Store", Slug = slug },
            new RetailTenant { Id = otherTenant, Name = "Other Store",
                Slug = "other-" + otherTenant.ToString("N")[..12] });
        db.Users.Add(new RetailUser { Id = buyer, Email = "buyer@example.test" });
        db.ExternalAccounts.Add(new ExternalAccount {
            Id = Guid.NewGuid(), UserId = buyer, Issuer = "https://id.example.test",
            Subject = "buyer-oidc"
        });
        db.CatalogueItems.Add(new CatalogueItem {
            Id = product, TenantId = tenantId, Name = "Mobile phone",
            Storage = "128GB", Category = "Phones", Price = 12500m, IsPublished = true
        });
        db.SerialisedDevices.Add(new SerialisedDevice {
            Id = device, TenantId = tenantId, ProductId = product,
            Imei = "490154203237518", Status = "Available", ReceivedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new CustomerReservationService(db,
            new OrderReservationService(db, TimeProvider.System));
        var request = new CustomerReserveRequest("https://id.example.test",
            "buyer-oidc", slug, product, device);

        Assert.Equal(ReserveOrderStatus.CustomerUnavailable,
            (await service.ReserveAsync(request with { Subject = "unknown" },
                CancellationToken.None)).Status);
        Assert.Equal(ReserveOrderStatus.StoreUnavailable,
            (await service.ReserveAsync(request with { StoreSlug = "unknown-store" },
                CancellationToken.None)).Status);
        Assert.Equal(ReserveOrderStatus.ProductUnavailable,
            (await service.ReserveAsync(request with {
                StoreSlug = "other-" + otherTenant.ToString("N")[..12]
            }, CancellationToken.None)).Status);
        Assert.Equal("Available", (await db.SerialisedDevices.AsNoTracking()
            .SingleAsync(x => x.Id == device)).Status);

        var result = await service.ReserveAsync(request, CancellationToken.None);
        Assert.Equal(ReserveOrderStatus.Reserved, result.Status);
        Assert.NotNull(result.OrderId);
        Assert.Equal(12500m, result.Total);

        var order = await db.RetailOrders.AsNoTracking()
            .SingleAsync(x => x.Id == result.OrderId);
        Assert.Equal(buyer, order.CustomerUserId);
        Assert.Equal(tenantId, order.TenantId);
        Assert.Equal("AwaitingPayment", order.Status);
        Assert.Equal("Reserved", (await db.SerialisedDevices.AsNoTracking()
            .SingleAsync(x => x.Id == device)).Status);
        Assert.Single(await db.InventoryMovements.Where(x => x.DeviceId == device &&
            x.MovementType == "Reserved").ToListAsync());

        var access = new CustomerOrderAccess(db);
        Assert.True(await access.OwnsOrderAsync("https://id.example.test",
            "buyer-oidc", tenantId, order.Id, CancellationToken.None));
        Assert.False(await access.OwnsOrderAsync("https://id.example.test",
            "another-user", tenantId, order.Id, CancellationToken.None));

        Assert.Equal(ReserveOrderStatus.OutOfStock,
            (await service.ReserveAsync(request, CancellationToken.None)).Status);
        Assert.Equal(1, await db.RetailOrders.CountAsync(x => x.DeviceId == device));
    }

    [Fact]
    public async Task InactiveCustomerCannotReserveStock()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var buyer = Guid.NewGuid();
        db.Users.Add(new RetailUser { Id = buyer, Email = "inactive@example.test", IsActive = false });
        db.ExternalAccounts.Add(new ExternalAccount {
            Id = Guid.NewGuid(), UserId = buyer,
            Issuer = "https://id.example.test", Subject = "inactive"
        });
        await db.SaveChangesAsync();
        var service = new CustomerReservationService(db,
            new OrderReservationService(db, TimeProvider.System));
        var result = await service.ReserveAsync(new CustomerReserveRequest(
            "https://id.example.test", "inactive", "valid-store",
            Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(ReserveOrderStatus.CustomerUnavailable, result.Status);
    }
}
