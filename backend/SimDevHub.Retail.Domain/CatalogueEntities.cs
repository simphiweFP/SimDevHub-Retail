namespace SimDevHub.Retail.Domain;

public sealed class RetailTenant
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<CatalogueItem> Products { get; set; } = new List<CatalogueItem>();
}

public sealed class CatalogueItem
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public required string Storage { get; set; }
    public required string Category { get; set; }
    public decimal Price { get; set; }
    public bool IsPublished { get; set; }
    public RetailTenant Tenant { get; set; } = null!;
}
