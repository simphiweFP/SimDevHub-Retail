using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SimDevHub.Retail.Domain;

#nullable disable

namespace SimDevHub.Retail.Infrastructure.Migrations;

[DbContext(typeof(RetailDbContext))]
public sealed class RetailDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");
        modelBuilder.Entity<RetailTenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).IsRequired().HasMaxLength(160);
            e.Property(x => x.Slug).IsRequired().HasMaxLength(80);
            e.Property(x => x.IsActive);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.IsActive);
        });
        modelBuilder.Entity<CatalogueItem>(e =>
        {
            e.ToTable("catalogue_items", t => t.HasCheckConstraint("ck_catalogue_items_price_nonnegative", "\"Price\" >= 0"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.TenantId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(220);
            e.Property(x => x.Storage).IsRequired().HasMaxLength(80);
            e.Property(x => x.Category).IsRequired().HasMaxLength(80);
            e.Property(x => x.Price).HasPrecision(14, 2);
            e.Property(x => x.IsPublished);
            e.HasIndex(x => new { x.TenantId, x.IsPublished, x.Category });
            e.HasOne(x => x.Tenant).WithMany(x => x.Products).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RetailUser>(e =>
        {
            e.ToTable("retail_users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Email).IsRequired().HasMaxLength(320);
            e.Property(x => x.IsActive);
            e.HasIndex(x => x.Email);
        });
        modelBuilder.Entity<ExternalAccount>(e =>
        {
            e.ToTable("external_accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.UserId);
            e.Property(x => x.Issuer).IsRequired().HasMaxLength(320);
            e.Property(x => x.Subject).IsRequired().HasMaxLength(320);
            e.HasIndex(x => new { x.Issuer, x.Subject }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User).WithMany(x => x.ExternalAccounts).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TenantMembership>(e =>
        {
            e.ToTable("tenant_memberships");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.TenantId);
            e.Property(x => x.UserId);
            e.Property(x => x.Role).IsRequired().HasMaxLength(50);
            e.Property(x => x.IsActive);
            e.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User).WithMany(x => x.Memberships).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StaffInvitation>(e =>
        {
            e.ToTable("staff_invitations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.TenantId);
            e.Property(x => x.Email).IsRequired().HasMaxLength(320);
            e.Property(x => x.Role).IsRequired().HasMaxLength(50);
            e.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
            e.Property(x => x.ExpiresAt);
            e.Property(x => x.AcceptedAt);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Email });
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
