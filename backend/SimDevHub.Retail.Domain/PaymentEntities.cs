namespace SimDevHub.Retail.Domain;

/// <summary>Provider messages received by a trusted adapter. Unique provider/event IDs prevent replay.</summary>
public sealed class PaymentEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public required string Provider { get; set; }
    public required string ProviderEventId { get; set; }
    public required string Outcome { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public RetailOrder Order { get; set; } = null!;
}
