using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Domain;

namespace SimDevHub.Retail.Infrastructure;

public sealed class RetailDbContext(DbContextOptions<RetailDbContext> options) : DbContext(options)
{
    public DbSet<RetailTenant> Tenants => Set<RetailTenant>();
    public DbSet<CatalogueItem> CatalogueItems => Set<CatalogueItem>();
    public DbSet<RetailUser> Users => Set<RetailUser>();
    public DbSet<ExternalAccount> ExternalAccounts => Set<ExternalAccount>();
    public DbSet<TenantMembership> Memberships => Set<TenantMembership>();
    public DbSet<StaffInvitation> StaffInvitations => Set<StaffInvitation>();

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
        product.ToTable("catalogue_items", t => t.HasCheckConstraint("ck_catalogue_items_price_nonnegative", "\"Price\" >= 0"));
        product.HasKey(x => x.Id);
        product.Property(x => x.Name).HasMaxLength(220).IsRequired();
        product.Property(x => x.Storage).HasMaxLength(80).IsRequired();
        product.Property(x => x.Category).HasMaxLength(80).IsRequired();
        product.Property(x => x.Price).HasPrecision(14, 2);
        product.HasIndex(x => new { x.TenantId, x.IsPublished, x.Category });
        product.HasOne(x => x.Tenant).WithMany(x => x.Products).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);

        var user = modelBuilder.Entity<RetailUser>();
        user.ToTable("retail_users");
        user.HasKey(x => x.Id);
        user.Property(x => x.Email).HasMaxLength(320).IsRequired();
        user.HasIndex(x => x.Email);
        var external = modelBuilder.Entity<ExternalAccount>();
        external.ToTable("external_accounts");
        external.HasKey(x => x.Id);
        external.Property(x => x.Issuer).HasMaxLength(320).IsRequired();
        external.Property(x => x.Subject).HasMaxLength(320).IsRequired();
        external.HasIndex(x => new { x.Issuer, x.Subject }).IsUnique();
        external.HasOne(x => x.User).WithMany(x => x.ExternalAccounts).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        var membership = modelBuilder.Entity<TenantMembership>();
        membership.ToTable("tenant_memberships");
        membership.HasKey(x => x.Id);
        membership.Property(x => x.Role).HasMaxLength(50).IsRequired();
        membership.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        membership.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        membership.HasOne(x => x.User).WithMany(x => x.Memberships).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        var invitation = modelBuilder.Entity<StaffInvitation>();
        invitation.ToTable("staff_invitations");
        invitation.HasKey(x => x.Id);
        invitation.Property(x => x.Email).HasMaxLength(320).IsRequired();
        invitation.Property(x => x.Role).HasMaxLength(50).IsRequired();
        invitation.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        invitation.HasIndex(x => x.TokenHash).IsUnique();
        invitation.HasIndex(x => new { x.TenantId, x.Email });
        invitation.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
