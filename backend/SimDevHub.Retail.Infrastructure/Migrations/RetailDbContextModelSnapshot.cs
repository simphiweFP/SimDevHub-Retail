using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using SimDevHub.Retail.Domain;

#nullable disable

namespace SimDevHub.Retail.Infrastructure.Migrations;

[DbContext(typeof(RetailDbContext))]
public sealed class RetailDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");
        modelBuilder.Entity<RetailTenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
            entity.Property(x => x.Name).IsRequired().HasMaxLength(160);
            entity.Property(x => x.Slug).IsRequired().HasMaxLength(80);
            entity.Property(x => x.IsActive);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => x.IsActive);
        });
        modelBuilder.Entity<CatalogueItem>(entity =>
        {
            entity.ToTable("catalogue_items", table => table.HasCheckConstraint("ck_catalogue_items_price_nonnegative", "\"Price\" >= 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
            entity.Property(x => x.TenantId);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(220);
            entity.Property(x => x.Storage).IsRequired().HasMaxLength(80);
            entity.Property(x => x.Category).IsRequired().HasMaxLength(80);
            entity.Property(x => x.Price).HasPrecision(14, 2);
            entity.Property(x => x.IsPublished);
            entity.HasIndex(x => new { x.TenantId, x.IsPublished, x.Category });
            entity.HasOne(x => x.Tenant).WithMany(x => x.Products).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
