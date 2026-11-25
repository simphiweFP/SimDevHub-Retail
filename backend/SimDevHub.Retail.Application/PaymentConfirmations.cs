namespace SimDevHub.Retail.Application;

public sealed record VerifiedPaymentEvent(
    Guid TenantId, Guid OrderId, Guid PaymentIntentId, string Provider, string ProviderEventId,
    decimal Amount, string Currency);

public enum PaymentConfirmationResult
{
    Paid,
    Duplicate,
    ReviewRequired,
    OrderNotFound,
    Invalid
}

/// <summary>
/// Only a provider adapter that has independently verified a signed event and
/// transaction status may invoke this contract. Not an HTTP endpoint.
/// </summary>
public interface IVerifiedPaymentProcessor
{
    Task<PaymentConfirmationResult> ConfirmAsync(VerifiedPaymentEvent payment, CancellationToken cancellationToken);
}
