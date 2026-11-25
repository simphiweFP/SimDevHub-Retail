namespace SimDevHub.Retail.Application;

public enum ReserveOrderStatus { Reserved, InvalidRequest, StoreUnavailable, ProductUnavailable, OutOfStock, CustomerUnavailable }
public sealed record ReserveOrderRequest(Guid TenantId, Guid ProductId, Guid DeviceId, Guid? CustomerUserId = null);
public sealed record ReserveOrderResult(ReserveOrderStatus Status, Guid? OrderId = null, decimal? Total = null, DateTimeOffset? ExpiresAt = null);
public interface IOrderReservationService
{
    Task<ReserveOrderResult> ReserveAsync(ReserveOrderRequest request, CancellationToken cancellationToken);
}
