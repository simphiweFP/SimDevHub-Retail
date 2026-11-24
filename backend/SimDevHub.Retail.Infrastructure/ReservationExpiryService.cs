using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Internal expiry worker operation; call via a scheduled background worker later.
/// The order claim and device release are in one transaction.
/// </summary>
public sealed class ReservationExpiryService(RetailDbContext db, TimeProvider clock) : IReservationExpiry
{
    public async Task<int> ReleaseExpiredAsync(int batchSize, CancellationToken ct)
    {
        if (batchSize is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var now = clock.GetUtcNow();
        var orders = await db.RetailOrders.AsNoTracking()
            .Where(x => x.Status == "AwaitingPayment" && x.ReservedUntil <= now)
            .OrderBy(x => x.ReservedUntil)
            .Select(x => new { x.Id, x.TenantId, x.DeviceId })
            .Take(batchSize)
            .ToListAsync(ct);
        var released = 0;
        foreach (var order in orders)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // A successful transition is a one-winner claim across competing workers.
            var claimed = await db.RetailOrders
                .Where(x => x.Id == order.Id && x.Status == "AwaitingPayment" && x.ReservedUntil <= now)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Expired"), ct);
            if (claimed != 1) continue;

            var deviceChanged = await db.SerialisedDevices
                .Where(x => x.Id == order.DeviceId && x.TenantId == order.TenantId && x.Status == "Reserved")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Available"), ct);
            if (deviceChanged != 1)
                throw new InvalidOperationException("Reservation/device state mismatch; transaction aborted.");

            db.InventoryMovements.Add(new InventoryMovement
            {
                Id = Guid.NewGuid(), TenantId = order.TenantId, DeviceId = order.DeviceId,
                MovementType = "ReservationExpired", Reference = order.Id.ToString("N"), OccurredAt = now
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            released++;
        }
        return released;
    }
}
