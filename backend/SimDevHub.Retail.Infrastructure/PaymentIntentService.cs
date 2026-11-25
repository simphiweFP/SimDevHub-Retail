using Microsoft.EntityFrameworkCore;
using Npgsql;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Creates internal payment intents only. Never requests a charge or returns a gateway URL.
/// The caller's tenant and order IDs must come from an authenticated checkout workflow.
/// </summary>
public sealed class PaymentIntentService(RetailDbContext db, TimeProvider clock) : IPaymentIntentService
{
    public async Task<PaymentIntentResult> CreateAsync(CreatePaymentIntentRequest request, CancellationToken ct)
    {
        if (request.TenantId == Guid.Empty || request.OrderId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length is < 8 or > 100 ||
            !request.IdempotencyKey.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_') ||
            string.IsNullOrWhiteSpace(request.Provider) || request.Provider.Length > 60)
            return new(CreatePaymentIntentStatus.Invalid);

        // This initial integration-neutral placeholder does not expose live provider selection.
        if (request.Provider != "SandboxPlaceholder") return new(CreatePaymentIntentStatus.ProviderUnavailable);

        var existing = await db.PaymentIntents.AsNoTracking()
            .Where(x => x.TenantId == request.TenantId && x.OrderId == request.OrderId &&
                        x.IdempotencyKey == request.IdempotencyKey)
            .Select(x => new { x.Id, x.Provider, x.Amount, x.Currency })
            .SingleOrDefaultAsync(ct);
        if (existing is not null)
            return existing.Provider == request.Provider
                ? new(CreatePaymentIntentStatus.Replayed, existing.Id, existing.Amount, existing.Currency)
                : new(CreatePaymentIntentStatus.Invalid);

        var order = await db.RetailOrders.AsNoTracking()
            .Where(x => x.Id == request.OrderId && x.TenantId == request.TenantId &&
                        x.Tenant.IsActive)
            .Select(x => new { x.Status, x.UnitPrice, x.ReservedUntil })
            .SingleOrDefaultAsync(ct);
        if (order is null || order.Status != "AwaitingPayment")
            return new(CreatePaymentIntentStatus.OrderUnavailable);
        if (order.ReservedUntil <= clock.GetUtcNow())
            return new(CreatePaymentIntentStatus.Expired);
        if (order.UnitPrice <= 0) return new(CreatePaymentIntentStatus.Invalid);

        // The database ensures idempotence even if two independent application
        // instances receive the same key simultaneously.
        var intent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            OrderId = request.OrderId,
            IdempotencyKey = request.IdempotencyKey,
            Provider = request.Provider,
            Status = "Unconfigured",
            Amount = order.UnitPrice,
            Currency = "ZAR",
            CreatedAt = clock.GetUtcNow()
        };
        db.PaymentIntents.Add(intent);
        try
        {
            await db.SaveChangesAsync(ct);
            return new(CreatePaymentIntentStatus.Created, intent.Id, intent.Amount, intent.Currency);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            var winner = await db.PaymentIntents.AsNoTracking()
                .Where(x => x.TenantId == request.TenantId && x.OrderId == request.OrderId &&
                            x.IdempotencyKey == request.IdempotencyKey)
                .Select(x => new { x.Id, x.Provider, x.Amount, x.Currency })
                .SingleOrDefaultAsync(ct);
            return winner is null || winner.Provider != request.Provider
                ? new(CreatePaymentIntentStatus.Invalid)
                : new(CreatePaymentIntentStatus.Replayed, winner.Id, winner.Amount, winner.Currency);
        }
    }
}
