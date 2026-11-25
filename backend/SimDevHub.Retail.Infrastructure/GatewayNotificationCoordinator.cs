using SimDevHub.Retail.Application;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// No provider verifiers are registered in the application yet: all notifications
/// fail closed. Gateway verifiers must validate signatures, timestamps, merchant
/// identity and payment status with their provider before returning an event.
/// </summary>
public sealed class GatewayNotificationCoordinator(
    IEnumerable<IPaymentGatewayVerifier> verifiers,
    IVerifiedPaymentProcessor processor) : IGatewayNotificationCoordinator
{
    public async Task<GatewayNotificationResult> HandleAsync(
        GatewayNotification notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(notification.Provider) ||
            notification.Provider.Length > 60 ||
            notification.Body.IsEmpty || notification.Body.Length > 64 * 1024)
            return new(GatewayNotificationStatus.VerificationFailed);

        // Reject missing or ambiguous provider registration; no fallback verifier.
        var available = verifiers.Where(x =>
            string.Equals(x.Provider, notification.Provider, StringComparison.Ordinal))
            .Take(2).ToArray();
        if (available.Length != 1)
            return new(GatewayNotificationStatus.ProviderUnavailable);

        // A verifier must return a fully authenticated event obtained from the
        // payment gateway, never a browser-posted "paid" field.
        var verified = await available[0].VerifyAsync(notification, cancellationToken);
        if (verified is null || verified.Provider != notification.Provider ||
            verified.PaymentIntentId == Guid.Empty || verified.TenantId == Guid.Empty ||
            verified.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(verified.ProviderEventId))
            return new(GatewayNotificationStatus.VerificationFailed);

        var status = await processor.ConfirmAsync(verified, cancellationToken);
        return new(GatewayNotificationStatus.Processed, status);
    }
}
