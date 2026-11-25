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

        modelBuilder.Entity<SerialisedDevice>(e =>
        {
            e.ToTable("serialised_devices", t => t.HasCheckConstraint("ck_device_imei_15_digits", "\"Imei\" ~ '^[0-9]{15}$'"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.TenantId);
            e.Property(x => x.ProductId);
            e.Property(x => x.Imei).IsRequired().HasMaxLength(15);
            e.Property(x => x.Status).IsRequired().HasMaxLength(24);
            e.Property(x => x.ReceivedAt);
            e.HasIndex(x => new { x.TenantId, x.Imei }).IsUnique();
            e.HasIndex(x => x.ProductId);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<InventoryMovement>(e =>
        {
            e.ToTable("inventory_movements");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.TenantId);
            e.Property(x => x.DeviceId);
            e.Property(x => x.MovementType).IsRequired().HasMaxLength(30);
            e.Property(x => x.OccurredAt);
            e.Property(x => x.Reference).IsRequired().HasMaxLength(100);
            e.HasIndex(x => new { x.DeviceId, x.OccurredAt });
            e.HasIndex(x => x.TenantId);
            e.HasOne(x => x.Device).WithMany(x => x.Movements).HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RetailOrder>(e =>
        {
            e.ToTable("retail_orders", t => t.HasCheckConstraint("ck_orders_nonnegative_price", "\"UnitPrice\" >= 0"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.TenantId);
            e.Property(x => x.ProductId);
            e.Property(x => x.DeviceId);
            e.Property(x => x.CustomerUserId);
            e.Property(x => x.Status).IsRequired().HasMaxLength(30);
            e.Property(x => x.UnitPrice).HasPrecision(14, 2);
            e.Property(x => x.CreatedAt);
            e.Property(x => x.ReservedUntil);
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasIndex(x => x.CustomerUserId);
            e.HasIndex(x => new { x.TenantId, x.CustomerUserId, x.CreatedAt });
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ProductId);
            e.HasIndex(x => x.DeviceId);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PaymentEvent>(e =>
        {
            e.ToTable("payment_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.TenantId);
            e.Property(x => x.OrderId);
            e.Property(x => x.PaymentIntentId);
            e.Property(x => x.Provider).IsRequired().HasMaxLength(60);
            e.Property(x => x.ProviderEventId).IsRequired().HasMaxLength(160);
            e.Property(x => x.Outcome).IsRequired().HasMaxLength(30);
            e.Property(x => x.Amount).HasPrecision(14, 2);
            e.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            e.Property(x => x.RecordedAt);
            e.HasIndex(x => new { x.Provider, x.ProviderEventId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.OrderId });
            e.HasIndex(x => x.OrderId);
            e.HasIndex(x => x.PaymentIntentId);
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PaymentIntent).WithMany().HasForeignKey(x => x.PaymentIntentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PaymentIntent>(e =>
        {
            e.ToTable("payment_intents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.TenantId);
            e.Property(x => x.OrderId);
            e.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(100);
            e.Property(x => x.Provider).IsRequired().HasMaxLength(60);
            e.Property(x => x.Status).IsRequired().HasMaxLength(30);
            e.Property(x => x.Amount).HasPrecision(14, 2);
            e.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            e.Property(x => x.CreatedAt);
            e.HasIndex(x => new { x.TenantId, x.OrderId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => x.OrderId, "IX_payment_intents_OnePayfastSandboxPerOrder")
                .IsUnique().HasFilter("\"Provider\" = 'PayfastSandbox'");
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<GatewayWebhookReceipt>(e =>
        {
            e.ToTable("gateway_webhook_receipts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Provider).IsRequired().HasMaxLength(60);
            e.Property(x => x.BodySha256).IsRequired().HasMaxLength(64);
            e.Property(x => x.Status).IsRequired().HasMaxLength(24);
            e.Property(x => x.AttemptCount);
            e.Property(x => x.ReceivedAt);
            e.Property(x => x.LastClaimedAt);
            e.Property(x => x.CompletedAt);
            e.HasIndex(x => new { x.Provider, x.BodySha256 }).IsUnique();
            e.HasIndex(x => new { x.Status, x.LastClaimedAt });
        });
    }
}
