using Microsoft.EntityFrameworkCore;
using Npgsql;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Internal verified-payment transition. NO browser, anonymous API, or untrusted
/// webhook is allowed to call this directly. Signature validation and independent
/// provider lookups belong to a future payment adapter.
/// </summary>
public sealed class VerifiedPaymentProcessor(RetailDbContext db, TimeProvider clock) : IVerifiedPaymentProcessor
{
    public async Task<PaymentConfirmationResult> ConfirmAsync(VerifiedPaymentEvent payment, CancellationToken ct)
    {
        if (payment.TenantId == Guid.Empty || payment.OrderId == Guid.Empty || payment.PaymentIntentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(payment.Provider) || string.IsNullOrWhiteSpace(payment.ProviderEventId) ||
            payment.Provider.Length is < 2 or > 60 || payment.ProviderEventId.Length is < 4 or > 160 ||
            payment.Amount <= 0 || payment.Currency != "ZAR")
            return PaymentConfirmationResult.Invalid;

        // Provider and event ID are uniquely indexed; collisions are not silently fulfilled twice.
        if (await db.PaymentEvents.AsNoTracking().AnyAsync(
            x => x.Provider == payment.Provider && x.ProviderEventId == payment.ProviderEventId, ct))
            return PaymentConfirmationResult.Duplicate;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var order = await db.RetailOrders.AsNoTracking()
                .Where(x => x.Id == payment.OrderId && x.TenantId == payment.TenantId)
                .Select(x => new { x.Id, x.TenantId, x.DeviceId, x.Status, x.UnitPrice, x.ReservedUntil })
                .SingleOrDefaultAsync(ct);
            if (order is null) return PaymentConfirmationResult.OrderNotFound;

            var intent = await db.PaymentIntents.AsNoTracking()
                .Where(x => x.Id == payment.PaymentIntentId && x.TenantId == payment.TenantId &&
                            x.OrderId == payment.OrderId)
                .Select(x => new { x.Id, x.Provider, x.Status, x.Amount, x.Currency })
                .SingleOrDefaultAsync(ct);
            // Never fulfil based on an event that points to another order, store or provider.
            if (intent is null || intent.Provider != payment.Provider)
                return PaymentConfirmationResult.Invalid;

            var now = clock.GetUtcNow();
            var outcome = "ReviewRequired";
            if (order.Status == "AwaitingPayment" &&
                order.ReservedUntil > now && order.UnitPrice == payment.Amount &&
                intent.Status == "AwaitingVerification" && intent.Amount == payment.Amount &&
                intent.Currency == payment.Currency)
            {
                var claimed = await db.RetailOrders
                    .Where(x => x.Id == order.Id && x.TenantId == payment.TenantId &&
                                x.Status == "AwaitingPayment" && x.ReservedUntil > now &&
                                x.UnitPrice == payment.Amount)
                    .ExecuteUpdateAsync(x => x.SetProperty(o => o.Status, "Paid"), ct);
                if (claimed == 1)
                {
                    var deviceClaimed = await db.SerialisedDevices
                        .Where(x => x.Id == order.DeviceId && x.TenantId == payment.TenantId &&
                                    x.Status == "Reserved")
                        .ExecuteUpdateAsync(x => x.SetProperty(d => d.Status, "Allocated"), ct);
                    if (deviceClaimed != 1)
                        throw new InvalidOperationException("Payment allocation mismatch: rollback and investigate.");

                    var intentClaimed = await db.PaymentIntents
                        .Where(x => x.Id == intent.Id && x.TenantId == payment.TenantId &&
                                    x.OrderId == order.Id && x.Status == "AwaitingVerification" &&
                                    x.Provider == payment.Provider)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Succeeded"), ct);
                    if (intentClaimed != 1)
                        throw new InvalidOperationException("Payment intent state mismatch: transaction rolled back.");

                    db.InventoryMovements.Add(new InventoryMovement
                    {
                        Id = Guid.NewGuid(), TenantId = payment.TenantId,
                        DeviceId = order.DeviceId, MovementType = "PaymentAllocated",
                        Reference = order.Id.ToString("N"), OccurredAt = now
                    });
                    outcome = "Paid";
                }
            }

            // Even a late/mismatched verified payment must be recorded for manual
            // reconciliation; never release or reallocate the stock automatically.
            db.PaymentEvents.Add(new PaymentEvent
            {
                Id = Guid.NewGuid(), TenantId = payment.TenantId, OrderId = payment.OrderId,
                PaymentIntentId = intent.Id, Provider = payment.Provider, ProviderEventId = payment.ProviderEventId,
                Amount = payment.Amount, Currency = payment.Currency, Outcome = outcome,
                RecordedAt = now
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return outcome == "Paid"
                ? PaymentConfirmationResult.Paid
                : PaymentConfirmationResult.ReviewRequired;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Concurrent processing of the same provider event: database wins.
            db.ChangeTracker.Clear();
            return PaymentConfirmationResult.Duplicate;
        }
    }
}
