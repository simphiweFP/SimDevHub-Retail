namespace SimDevHub.Retail.Application;

public sealed record CreatePaymentIntentRequest(Guid TenantId, Guid OrderId, string IdempotencyKey, string Provider);
public enum CreatePaymentIntentStatus { Created, Replayed, Invalid, OrderUnavailable, Expired, ProviderUnavailable }
public sealed record PaymentIntentResult(
    CreatePaymentIntentStatus Status, Guid? IntentId = null, decimal? Amount = null, string? Currency = null);

public interface IPaymentIntentService
{
    Task<PaymentIntentResult> CreateAsync(CreatePaymentIntentRequest request, CancellationToken ct);
}

/// <summary>
/// Gateway integrations must independently validate provider credentials, hosted-checkout
/// response, signed callbacks, transaction status and merchant ownership.
/// This interface is deliberately not an implementation of a live payment gateway.
/// </summary>
public interface IPaymentGateway
{
    string Provider { get; }
    bool IsConfigured { get; }
}
