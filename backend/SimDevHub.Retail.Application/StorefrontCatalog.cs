namespace SimDevHub.Retail.Application;

public sealed record StorefrontProduct(Guid Id, string Name, string Storage, string Category, decimal Price);
public sealed record StorefrontCatalogue(string StoreName, string StoreSlug, IReadOnlyList<StorefrontProduct> Products);

public interface IStorefrontCatalogue
{
    Task<StorefrontCatalogue?> GetPublishedAsync(string storeSlug, CancellationToken cancellationToken);
}
