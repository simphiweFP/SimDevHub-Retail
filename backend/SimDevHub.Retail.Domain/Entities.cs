namespace SimDevHub.Retail.Domain;
public sealed record Product(Guid Id, string Name, string Storage, decimal Price, string Category);
public sealed record Tenant(Guid Id, string Name, string Slug);
public sealed record Branch(Guid Id, Guid TenantId, string Name);
public sealed record InventoryItem(Guid Id, Guid TenantId, Guid ProductId, string? Imei, string Status);
