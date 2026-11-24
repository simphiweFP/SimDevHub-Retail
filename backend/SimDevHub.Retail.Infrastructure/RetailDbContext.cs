using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Domain;

namespace SimDevHub.Retail.Infrastructure;

public sealed class RetailDbContext(DbContextOptions<RetailDbContext> options) : DbContext(options)
{
    public DbSet<RetailTenant> Tenants => Set<RetailTenant>();
    public DbSet<CatalogueItem> CatalogueItems => Set<CatalogueItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tenant = modelBuilder.Entity<RetailTenant>();
        tenant.ToTable("tenants");
        tenant.HasKey(x => x.Id);
        tenant.Property(x => x.Name).HasMaxLength(160).IsRequired();
        tenant.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        tenant.HasIndex(x => x.Slug).IsUnique();
        tenant.HasIndex(x => x.IsActive);

        var product = modelBuilder.Entity<CatalogueItem>();
        product.ToTable("catalogue_items");
        product.HasKey(x => x.Id);
        product.Property(x => x.Name).HasMaxLength(220).IsRequired();
        product.Property(x => x.Storage).HasMaxLength(80).IsRequired();
        product.Property(x => x.Category).HasMaxLength(80).IsRequired();
        product.Property(x => x.Price).HasPrecision(14, 2);
        product.ToTable(t => t.HasCheckConstraint("ck_catalogue_items_price_nonnegative", "\"Price\" >= 0"));
        product.HasIndex(x => new { x.TenantId, x.IsPublished, x.Category });
        product.HasOne(x => x.Tenant).WithMany(x => x.Products).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
