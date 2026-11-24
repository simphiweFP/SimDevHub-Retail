using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;
namespace SimDevHub.Retail.Infrastructure;
public sealed class DevelopmentProductReader : IProductReader
{
  public Task<IReadOnlyList<Product>> GetPublishedAsync(Guid tenantId, CancellationToken cancellationToken)
  {
    IReadOnlyList<Product> products = new[]
    {
      new Product(Guid.Parse("ab513331-a373-4ea4-85fa-6ffea131a651"),"iPhone 16 Pro","256GB",26999m,"Phones"),
      new Product(Guid.Parse("c572b2ac-c85d-47ab-9432-e9f5e1d9a240"),"Samsung Galaxy A55","128GB",6499m,"Phones")
    };
    return Task.FromResult(products);
  }
}
