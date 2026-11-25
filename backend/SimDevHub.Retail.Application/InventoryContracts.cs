namespace SimDevHub.Retail.Application;

public enum ReceiveDeviceResult { Received, InvalidImei, InvalidReference, ProductNotFound, DuplicateImei }
public sealed record ReceiveDeviceRequest(Guid TenantId, Guid ProductId, string Imei, string ReceiptReference);

public interface IInventoryReceiving
{
    Task<ReceiveDeviceResult> ReceiveAsync(ReceiveDeviceRequest request, CancellationToken cancellationToken);
}
