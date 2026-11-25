namespace SimDevHub.Retail.Domain;

/// <summary>One tracked physical device, not an aggregate quantity.</summary>
public sealed class SerialisedDevice
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public required string Imei { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public RetailTenant Tenant { get; set; } = null!;
    public CatalogueItem Product { get; set; } = null!;
    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}

/// <summary>Movement history is append-only through application services.</summary>
public sealed class InventoryMovement
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DeviceId { get; set; }
    public required string MovementType { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public required string Reference { get; set; }
    public SerialisedDevice Device { get; set; } = null!;
}
