using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Resolves customer identity and active retailer from persistence before
/// atomically reserving a handset with the owner set in the SAME transaction.
/// Never accepts arbitrary customer/tenant IDs from the external request.
/// </summary>
public sealed class CustomerReservationService(
    RetailDbContext db, IOrderReservationService reservations) : ICustomerReservationService
{
    public async Task<ReserveOrderResult> ReserveAsync(CustomerReserveRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Issuer) || request.Issuer.Length > 320 ||
            string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Length > 320 ||
            string.IsNullOrWhiteSpace(request.StoreSlug) || request.StoreSlug.Length is < 2 or > 80 ||
            !request.StoreSlug.All(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') ||
            request.ProductId == Guid.Empty || request.DeviceId == Guid.Empty)
            return new(ReserveOrderStatus.InvalidRequest);

        var customerId = await db.ExternalAccounts.AsNoTracking()
            .Where(x => x.Issuer == request.Issuer && x.Subject == request.Subject && x.User.IsActive)
            .Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(ct);
        if (customerId is null)
            return new(ReserveOrderStatus.CustomerUnavailable);

        var tenantId = await db.Tenants.AsNoTracking()
            .Where(x => x.Slug == request.StoreSlug && x.IsActive)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (tenantId is null)
            return new(ReserveOrderStatus.StoreUnavailable);

        return await reservations.ReserveAsync(
            new ReserveOrderRequest(tenantId.Value, request.ProductId, request.DeviceId, customerId.Value), ct);
    }
}

public sealed class CustomerProductReservationService(
    RetailDbContext db, ICustomerReservationService reservations)
    : ICustomerProductReservationService
{
    public async Task<ReserveOrderResult> ReserveOneAsync(CustomerProductReserveRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Issuer) || request.Issuer.Length > 320 ||
            string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Length > 320 ||
            string.IsNullOrWhiteSpace(request.StoreSlug) || request.StoreSlug.Length is < 2 or > 80 ||
            !request.StoreSlug.All(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') ||
            request.ProductId == Guid.Empty)
            return new(ReserveOrderStatus.InvalidRequest);

        // Do not disclose IMEI, internal device IDs or cross-tenant inventory.
        var candidateIds = await db.SerialisedDevices.AsNoTracking()
            .Where(d => d.ProductId == request.ProductId && d.Status == "Available" &&
                        d.Tenant.IsActive && d.Tenant.Slug == request.StoreSlug &&
                        d.Product.IsPublished && d.Product.Price >= 0)
            .OrderBy(d => d.ReceivedAt).ThenBy(d => d.Id)
            .Select(d => d.Id).Take(8).ToListAsync(ct);

        if (candidateIds.Count == 0)
        {
            var customerExists = await db.ExternalAccounts.AsNoTracking()
                .AnyAsync(x => x.Issuer == request.Issuer && x.Subject == request.Subject &&
                               x.User.IsActive, ct);
            if (!customerExists) return new(ReserveOrderStatus.CustomerUnavailable);
            var storeExists = await db.Tenants.AsNoTracking()
                .AnyAsync(x => x.Slug == request.StoreSlug && x.IsActive, ct);
            if (!storeExists) return new(ReserveOrderStatus.StoreUnavailable);
            var productExists = await db.CatalogueItems.AsNoTracking()
                .AnyAsync(x => x.Id == request.ProductId && x.Tenant.Slug == request.StoreSlug &&
                               x.Tenant.IsActive && x.IsPublished && x.Price >= 0, ct);
            return new(productExists ? ReserveOrderStatus.OutOfStock : ReserveOrderStatus.ProductUnavailable);
        }

        // The existing conditional database UPDATE remains the only allocator.
        // If another shopper won the race, retry a different device.
        foreach (var deviceId in candidateIds)
        {
            var outcome = await reservations.ReserveAsync(
                new CustomerReserveRequest(request.Issuer, request.Subject,
                    request.StoreSlug, request.ProductId, deviceId), ct);
            if (outcome.Status != ReserveOrderStatus.OutOfStock) return outcome;
        }
        return new(ReserveOrderStatus.OutOfStock);
    }
}
