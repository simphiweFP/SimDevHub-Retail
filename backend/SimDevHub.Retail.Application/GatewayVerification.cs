using System.Net;

namespace SimDevHub.Retail.Application;

/// <summary>
/// Raw notification content is untrusted. A provider-specific verifier must
/// authenticate the callback and independently confirm the payment status.
/// </summary>
public sealed record GatewayNotification(
    string Provider, ReadOnlyMemory<byte> Body, IReadOnlyDictionary<string, string> Headers,
    IPAddress? SourceAddress = null);

public interface IPaymentGatewayVerifier
{
    string Provider { get; }
    Task<VerifiedPaymentEvent?> VerifyAsync(
        GatewayNotification notification, CancellationToken cancellationToken);
}

public enum GatewayNotificationStatus
{
    ProviderUnavailable,
    VerificationFailed,
    Processed
}

public sealed record GatewayNotificationResult(
    GatewayNotificationStatus Status, PaymentConfirmationResult? PaymentResult = null);

public interface IGatewayNotificationCoordinator
{
    Task<GatewayNotificationResult> HandleAsync(
        GatewayNotification notification, CancellationToken cancellationToken);
}
