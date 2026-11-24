using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Only published catalogue records from an active store are exposed through this public reader.
/// Staff and private data will require separate authenticated, tenant-scoped interfaces.
/// </summary>
public sealed class StorefrontCatalogueReader(RetailDbContext db) : IStorefrontCatalogue
{
    public async Task<StorefrontCatalogue?> GetPublishedAsync(string storeSlug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storeSlug)) return null;

        var store = await db.Tenants.AsNoTracking()
            .Where(t => t.Slug == storeSlug && t.IsActive)
            .Select(t => new { t.Id, t.Name, t.Slug })
            .SingleOrDefaultAsync(cancellationToken);

        if (store is null) return null;

        var products = await db.CatalogueItems.AsNoTracking()
            .Where(x => x.TenantId == store.Id && x.IsPublished)
            .OrderBy(x => x.Name)
            .Select(x => new StorefrontProduct(x.Id, x.Name, x.Storage, x.Category, x.Price))
            .ToListAsync(cancellationToken);

        return new StorefrontCatalogue(store.Name, store.Slug, products);
    }
}
