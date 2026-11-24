using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimDevHub.Retail.Infrastructure.Migrations;

[DbContext(typeof(RetailDbContext))]
[Migration("20261124103000_InitialRetailCatalogue")]
public sealed class InitialRetailCatalogue : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tenants",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_tenants", x => x.Id));

        migrationBuilder.CreateTable(
            name: "catalogue_items",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                Storage = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Price = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                IsPublished = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_catalogue_items", x => x.Id);
                table.ForeignKey("FK_catalogue_items_tenants_TenantId", x => x.TenantId, "tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("ck_catalogue_items_price_nonnegative", "\"Price\" >= 0");
            });

        migrationBuilder.CreateIndex(name: "IX_tenants_Slug", table: "tenants", column: "Slug", unique: true);
        migrationBuilder.CreateIndex(name: "IX_tenants_IsActive", table: "tenants", column: "IsActive");
        migrationBuilder.CreateIndex(name: "IX_catalogue_items_TenantId_IsPublished_Category", table: "catalogue_items", columns: new[] { "TenantId", "IsPublished", "Category" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("catalogue_items");
        migrationBuilder.DropTable("tenants");
    }
}
