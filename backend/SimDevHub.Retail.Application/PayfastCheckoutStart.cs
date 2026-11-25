namespace SimDevHub.Retail.Application;

/// <summary>
/// Internal-only service. TenantId and OrderId must be obtained by a trusted
/// authenticated order workflow. Never expose this contract as anonymous HTTP.
/// </summary>
public sealed record StartPayfastSandboxCheckout(Guid TenantId, Guid OrderId, string IdempotencyKey);
public enum PayfastCheckoutStartStatus
{
    Ready, Replayed, Invalid, Unavailable, OrderUnavailable, Expired, Conflict
}
public sealed record PayfastCheckoutStartResult(
    PayfastCheckoutStartStatus Status, Guid? IntentId = null,
    string? FormAction = null, IReadOnlyDictionary<string, string>? FormFields = null);

public interface IPayfastSandboxCheckoutStart
{
    Task<PayfastCheckoutStartResult> StartAsync(
        StartPayfastSandboxCheckout request, CancellationToken ct);
}
