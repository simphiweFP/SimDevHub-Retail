using SimDevHub.Retail.Domain;
namespace SimDevHub.Retail.Application;
public interface IProductReader
{
  Task<IReadOnlyList<Product>> GetPublishedAsync(Guid tenantId, CancellationToken cancellationToken);
}
