using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Internal-only receipt processor: persists hash first, claims with an atomic
/// compare-and-update and fences stale workers. Never stores raw payer data.
/// Automatic retries require secure payload storage, which is NOT implemented.
/// </summary>
public sealed class WebhookReceiptProcessor(
    RetailDbContext db, IGatewayNotificationCoordinator coordinator, TimeProvider clock)
    : IWebhookReceiptProcessor
{
    private const int MaxAttempts = 8;
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);

    public async Task<WebhookReceiptResult> ReceiveAsync(GatewayNotification notification, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(notification.Provider) ||
            notification.Provider.Length > 60 ||
            notification.Body.IsEmpty || notification.Body.Length > 32 * 1024)
            return new(null, WebhookReceiptStatus.Rejected);

        var fingerprint = Convert.ToHexString(SHA256.HashData(notification.Body.Span));
        var record = await db.GatewayWebhookReceipts.AsNoTracking().SingleOrDefaultAsync(
            x => x.Provider == notification.Provider && x.BodySha256 == fingerprint, ct);

        if (record is null)
        {
            record = new GatewayWebhookReceipt
            {
                Id = Guid.NewGuid(), Provider = notification.Provider,
                BodySha256 = fingerprint, Status = "Pending",
                ReceivedAt = clock.GetUtcNow()
            };
            db.GatewayWebhookReceipts.Add(record);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                db.ChangeTracker.Clear();
                record = await db.GatewayWebhookReceipts.AsNoTracking().SingleAsync(
                    x => x.Provider == notification.Provider && x.BodySha256 == fingerprint, ct);
            }
        }

        if (record.Status == "Completed")
            return new(record.Id, WebhookReceiptStatus.AlreadyProcessed);
        if (record.Status == "Rejected" || record.AttemptCount >= MaxAttempts)
            return new(record.Id, WebhookReceiptStatus.Rejected);

        var now = clock.GetUtcNow();
        var staleAt = now.Subtract(ProcessingLease);
        var claimed = await db.GatewayWebhookReceipts
            .Where(x => x.Id == record.Id && x.AttemptCount < MaxAttempts &&
                (x.Status == "Pending" || x.Status == "RetryPending" ||
                 (x.Status == "Processing" && x.LastClaimedAt < staleAt)))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "Processing")
                .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                .SetProperty(x => x.LastClaimedAt, now), ct);
        if (claimed != 1) return new(record.Id, WebhookReceiptStatus.Busy);

        var attempt = await db.GatewayWebhookReceipts.AsNoTracking()
            .Where(x => x.Id == record.Id).Select(x => x.AttemptCount).SingleAsync(ct);

        try
        {
            var result = await coordinator.HandleAsync(notification, ct);
            var processed = result.Status == GatewayNotificationStatus.Processed &&
                (result.PaymentResult == PaymentConfirmationResult.Paid ||
                 result.PaymentResult == PaymentConfirmationResult.Duplicate ||
                 result.PaymentResult == PaymentConfirmationResult.ReviewRequired);
            var rejected = result.Status == GatewayNotificationStatus.Processed &&
                (result.PaymentResult == PaymentConfirmationResult.Invalid ||
                 result.PaymentResult == PaymentConfirmationResult.OrderNotFound);
            var next = processed ? "Completed" :
                rejected || attempt >= MaxAttempts ? "Rejected" : "RetryPending";
            await db.GatewayWebhookReceipts.Where(x => x.Id == record.Id &&
                    x.Status == "Processing" && x.AttemptCount == attempt)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, next)
                    .SetProperty(x => x.CompletedAt, processed ? (DateTimeOffset?)now : null), ct);
            return new(record.Id,
                processed ? WebhookReceiptStatus.Processed :
                next == "Rejected" ? WebhookReceiptStatus.Rejected : WebhookReceiptStatus.RetryPending,
                result.PaymentResult);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            await db.GatewayWebhookReceipts.Where(x => x.Id == record.Id &&
                    x.Status == "Processing" && x.AttemptCount == attempt)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status,
                    attempt >= MaxAttempts ? "Rejected" : "RetryPending"), ct);
            return new(record.Id, attempt >= MaxAttempts ?
                WebhookReceiptStatus.Rejected : WebhookReceiptStatus.RetryPending);
        }
    }
}
