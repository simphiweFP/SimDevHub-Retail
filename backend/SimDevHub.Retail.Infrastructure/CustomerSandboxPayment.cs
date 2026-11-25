using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SimDevHub.Retail.Application;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Owner and tenant are resolved from the persisted external identity and store.
/// Never accept browser-supplied user ID, tenant ID, merchant or amount.
/// </summary>
public sealed class CustomerSandboxPayment(
    RetailDbContext db, IPayfastSandboxCheckoutStart checkout,
    IConfiguration config) : ICustomerSandboxPayment
{
    public async Task<PayfastCheckoutStartResult> StartAsync(
        CustomerSandboxPaymentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Issuer) ||
            string.IsNullOrWhiteSpace(request.Subject) ||
            string.IsNullOrWhiteSpace(request.StoreSlug) ||
            request.StoreSlug.Length is < 2 or > 80 ||
            !request.StoreSlug.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') ||
            request.OrderId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey) ||
            request.IdempotencyKey.Length is < 8 or > 100 ||
            !request.IdempotencyKey.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            return new(PayfastCheckoutStartStatus.Invalid);

        if (!string.Equals(config["Payments:PayfastSandbox:EnableCustomerCheckout"],
                "true", StringComparison.OrdinalIgnoreCase))
            return new(PayfastCheckoutStartStatus.Unavailable);

        var owned = await db.RetailOrders.AsNoTracking()
            .Where(o => o.Id == request.OrderId && o.Tenant.IsActive &&
                        o.Tenant.Slug == request.StoreSlug &&
                        o.CustomerUserId != null && o.Customer!.IsActive &&
                        o.Customer.ExternalAccounts.Any(x => x.Issuer == request.Issuer &&
                                                            x.Subject == request.Subject))
            .Select(o => new { o.TenantId }).SingleOrDefaultAsync(ct);

        if (owned is null)
            return new(PayfastCheckoutStartStatus.OrderUnavailable);

        return await checkout.StartAsync(new StartPayfastSandboxCheckout(
            owned.TenantId, request.OrderId, request.IdempotencyKey), ct);
    }
}
