using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Internal read-only payment exception queue. Do not expose without an
/// authenticated, authorised tenant membership and least-privilege permission.
/// </summary>
public sealed class PaymentReconciliationReader(RetailDbContext db) : IPaymentReconciliationReader
{
    public async Task<IReadOnlyList<PaymentExceptionSummary>> GetUnresolvedAsync(
        Guid tenantId, int take, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || take is < 1 or > 100)
            return Array.Empty<PaymentExceptionSummary>();
        return await db.PaymentEvents.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Outcome == "ReviewRequired")
            .OrderByDescending(x => x.RecordedAt)
            .ThenBy(x => x.Id)
            .Take(take)
            .Select(x => new PaymentExceptionSummary(
                x.Id, x.OrderId, x.Provider, x.ProviderEventId,
                x.Amount, x.Currency, x.RecordedAt))
            .ToListAsync(cancellationToken);
    }
}
