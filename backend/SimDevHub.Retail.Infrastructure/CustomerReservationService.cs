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
