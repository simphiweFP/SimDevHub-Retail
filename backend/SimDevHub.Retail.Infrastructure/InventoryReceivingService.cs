using Microsoft.EntityFrameworkCore;
using Npgsql;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Internal receiving service. Do not expose until staff permissions, role
/// scope, document verification and auditing have been wired to an API.
/// </summary>
public sealed class InventoryReceivingService(RetailDbContext db, TimeProvider clock) : IInventoryReceiving
{
    public async Task<ReceiveDeviceResult> ReceiveAsync(ReceiveDeviceRequest request, CancellationToken ct)
    {
        var imei = request.Imei.Trim();
        var reference = request.ReceiptReference.Trim();
        if (imei.Length != 15 || !imei.All(char.IsAsciiDigit))
            return ReceiveDeviceResult.InvalidImei;
        if (reference.Length is < 2 or > 100)
            return ReceiveDeviceResult.InvalidReference;
        if (!await db.CatalogueItems.AnyAsync(p => p.Id == request.ProductId &&
                p.TenantId == request.TenantId && p.Tenant.IsActive, ct))
            return ReceiveDeviceResult.ProductNotFound;

        // The DB unique index prevents duplicates even under concurrent requests.
        if (await db.SerialisedDevices.AnyAsync(x => x.TenantId == request.TenantId && x.Imei == imei, ct))
            return ReceiveDeviceResult.DuplicateImei;

        var id = Guid.NewGuid();
        db.SerialisedDevices.Add(new SerialisedDevice
        {
            Id = id, TenantId = request.TenantId, ProductId = request.ProductId,
            Imei = imei, Status = "Available", ReceivedAt = clock.GetUtcNow()
        });
        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(), TenantId = request.TenantId, DeviceId = id,
            MovementType = "Received", Reference = reference, OccurredAt = clock.GetUtcNow()
        });

        try
        {
            await db.SaveChangesAsync(ct);
            return ReceiveDeviceResult.Received;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // This is the expected race outcome for a duplicate IMEI.
            db.ChangeTracker.Clear();
            return ReceiveDeviceResult.DuplicateImei;
        }
    }
}
