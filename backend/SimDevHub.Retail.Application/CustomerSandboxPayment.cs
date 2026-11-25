namespace SimDevHub.Retail.Application;
public sealed record CustomerSandboxPaymentRequest(
    string Issuer, string Subject, string StoreSlug, Guid OrderId, string IdempotencyKey);
public interface ICustomerSandboxPayment
{
    Task<PayfastCheckoutStartResult> StartAsync(CustomerSandboxPaymentRequest request, CancellationToken ct);
}
