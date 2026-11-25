using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimDevHub.Retail.Infrastructure.Migrations;

[DbContext(typeof(RetailDbContext))]
[Migration("20261124180000_OrderReservation")]
public sealed class OrderReservation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("retail_orders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                UnitPrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReservedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_retail_orders", x => x.Id);
                table.ForeignKey("FK_retail_orders_tenants_TenantId", x => x.TenantId, "tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_retail_orders_catalogue_items_ProductId", x => x.ProductId, "catalogue_items", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_retail_orders_serialised_devices_DeviceId", x => x.DeviceId, "serialised_devices", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("ck_orders_nonnegative_price", "\"UnitPrice\" >= 0");
            });
        migrationBuilder.CreateIndex("IX_retail_orders_TenantId_Status", "retail_orders", new[] { "TenantId", "Status" });
        migrationBuilder.CreateIndex("IX_retail_orders_ProductId", "retail_orders", "ProductId");
        migrationBuilder.CreateIndex("IX_retail_orders_DeviceId", "retail_orders", "DeviceId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("retail_orders");
}
