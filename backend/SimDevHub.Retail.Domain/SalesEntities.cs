namespace SimDevHub.Retail.Domain;

/// <summary>Initial single-device order; further increments add customers, multiple lines, taxes and fulfilment.</summary>
public sealed class RetailOrder
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid DeviceId { get; set; }
    public required string Status { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ReservedUntil { get; set; }
    public RetailTenant Tenant { get; set; } = null!;
    public CatalogueItem Product { get; set; } = null!;
    public SerialisedDevice Device { get; set; } = null!;
}
