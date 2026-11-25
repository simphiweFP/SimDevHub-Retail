using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Domain;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>Fictional development-only tenant and products. Never invoke for production.</summary>
public static class DevelopmentSeed
{
    public static async Task SeedAsync(RetailDbContext db, CancellationToken ct = default)
    {
        const string slug = "nova-mobile";
        if (await db.Tenants.AnyAsync(x => x.Slug == slug, ct)) return;
        var tenantId = Guid.Parse("61d2ce0d-47f4-4ab1-891d-3a8e2eb456c5");
        db.Tenants.Add(new RetailTenant { Id = tenantId, Name = "Nova Mobile", Slug = slug });
        db.CatalogueItems.AddRange(
            new CatalogueItem { Id = Guid.NewGuid(), TenantId = tenantId, Name = "iPhone 16 Pro", Storage = "256GB", Category = "Phones", Price = 26999m, IsPublished = true },
            new CatalogueItem { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Samsung Galaxy S24 Ultra", Storage = "256GB", Category = "Phones", Price = 23999m, IsPublished = true },
            new CatalogueItem { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Samsung Galaxy A55", Storage = "128GB", Category = "Phones", Price = 6499m, IsPublished = true },
            new CatalogueItem { Id = Guid.NewGuid(), TenantId = tenantId, Name = "iPhone 15", Storage = "128GB", Category = "Phones", Price = 18999m, IsPublished = true }
        );
        await db.SaveChangesAsync(ct);
    }
}
