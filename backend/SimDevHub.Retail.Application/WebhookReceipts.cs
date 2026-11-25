namespace SimDevHub.Retail.Application;
public enum WebhookReceiptStatus { Processed, AlreadyProcessed, Busy, RetryPending, Rejected }
public sealed record WebhookReceiptResult(Guid? ReceiptId, WebhookReceiptStatus Status, PaymentConfirmationResult? Payment = null);
public interface IWebhookReceiptProcessor
{
    Task<WebhookReceiptResult> ReceiveAsync(GatewayNotification notification, CancellationToken ct);
}
