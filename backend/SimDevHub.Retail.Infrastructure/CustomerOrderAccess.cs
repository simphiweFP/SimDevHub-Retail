using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Resolves the authenticated user via persisted issuer+subject. It never
/// trusts email addresses, user IDs, tenant IDs or order ownership from HTTP.
/// </summary>
public sealed class CustomerOrderAccess(RetailDbContext db) : ICustomerOrderAccess
{
    public async Task<IReadOnlyList<CustomerOrderSummary>> GetOwnedOrdersAsync(
        string issuer, string subject, string storeSlug, int take, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject) ||
            string.IsNullOrWhiteSpace(storeSlug) || take is < 1 or > 100)
            return Array.Empty<CustomerOrderSummary>();

        return await db.RetailOrders.AsNoTracking()
            .Where(o => o.CustomerUserId != null && o.Tenant.IsActive &&
                        o.Tenant.Slug == storeSlug && o.Customer!.IsActive &&
                        o.Customer.ExternalAccounts.Any(a => a.Issuer == issuer && a.Subject == subject))
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Take(take)
            .Select(o => new CustomerOrderSummary(
                o.Id, o.Status, o.UnitPrice, "ZAR", o.CreatedAt, o.ReservedUntil))
            .ToListAsync(ct);
    }

    public Task<bool> OwnsOrderAsync(
        string issuer, string subject, Guid tenantId, Guid orderId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject) ||
            tenantId == Guid.Empty || orderId == Guid.Empty)
            return Task.FromResult(false);

        return db.RetailOrders.AsNoTracking()
            .AnyAsync(o => o.Id == orderId && o.TenantId == tenantId &&
                           o.CustomerUserId != null && o.Tenant.IsActive &&
                           o.Customer!.IsActive &&
                           o.Customer.ExternalAccounts.Any(a =>
                               a.Issuer == issuer && a.Subject == subject), ct);
    }
}
