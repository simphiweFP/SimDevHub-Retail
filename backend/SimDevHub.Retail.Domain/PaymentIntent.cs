namespace SimDevHub.Retail.Domain;

/// <summary>
/// A server-priced request to start a payment, not evidence that money was received.
/// No provider credentials, redirect URL or card data are persisted here.
/// </summary>
public sealed class PaymentIntent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string Provider { get; set; }
    public required string Status { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public RetailOrder Order { get; set; } = null!;
}
