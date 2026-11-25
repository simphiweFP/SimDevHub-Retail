using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Internal-only reservation operation. Affected-row conditional update + transaction
/// prevent two orders reserving the same serialised device.
/// No checkout API is exposed until customer identity, tax, delivery pricing and payments are ready.
/// </summary>
public sealed class OrderReservationService(RetailDbContext db, TimeProvider clock) : IOrderReservationService
{
    public async Task<ReserveOrderResult> ReserveAsync(ReserveOrderRequest request, CancellationToken ct)
    {
        if (request.TenantId == Guid.Empty || request.ProductId == Guid.Empty || request.DeviceId == Guid.Empty)
            return new(ReserveOrderStatus.InvalidRequest);

        if (request.CustomerUserId == Guid.Empty)
            return new(ReserveOrderStatus.CustomerUnavailable);
        if (request.CustomerUserId is Guid customerId &&
            !await db.Users.AsNoTracking().AnyAsync(x => x.Id == customerId && x.IsActive, ct))
            return new(ReserveOrderStatus.CustomerUnavailable);

        var product = await db.CatalogueItems.AsNoTracking()
            .Where(p => p.Id == request.ProductId && p.TenantId == request.TenantId &&
                        p.Tenant.IsActive && p.IsPublished && p.Price >= 0)
            .Select(p => new { p.Price })
            .SingleOrDefaultAsync(ct);
        if (product is null) return new(ReserveOrderStatus.ProductUnavailable);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.SerialisedDevices
            .Where(x => x.Id == request.DeviceId && x.TenantId == request.TenantId &&
                        x.ProductId == request.ProductId && x.Status == "Available")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Reserved"), ct);
        if (changed != 1) return new(ReserveOrderStatus.OutOfStock);

        var now = clock.GetUtcNow();
        var order = new RetailOrder
        {
            Id = Guid.NewGuid(), TenantId = request.TenantId, ProductId = request.ProductId,
            DeviceId = request.DeviceId, CustomerUserId = request.CustomerUserId,
            Status = "AwaitingPayment",
            UnitPrice = product.Price, CreatedAt = now, ReservedUntil = now.AddMinutes(15)
        };
        db.RetailOrders.Add(order);
        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(), TenantId = request.TenantId, DeviceId = request.DeviceId,
            MovementType = "Reserved", Reference = order.Id.ToString("N"), OccurredAt = now
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(ReserveOrderStatus.Reserved, order.Id, order.UnitPrice, order.ReservedUntil);
    }
}
