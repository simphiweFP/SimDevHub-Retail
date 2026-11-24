using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimDevHub.Retail.Infrastructure.Migrations;

[DbContext(typeof(RetailDbContext))]
[Migration("20261124150000_SerialisedInventory")]
public sealed class SerialisedInventory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("serialised_devices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                Imei = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_serialised_devices", x => x.Id);
                table.ForeignKey("FK_serialised_devices_tenants_TenantId", x => x.TenantId, "tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_serialised_devices_catalogue_items_ProductId", x => x.ProductId, "catalogue_items", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("ck_device_imei_15_digits", "\"Imei\" ~ '^[0-9]{15}$'");
            });
        migrationBuilder.CreateIndex("IX_serialised_devices_TenantId_Imei", "serialised_devices", new[] { "TenantId", "Imei" }, unique: true);
        migrationBuilder.CreateIndex("IX_serialised_devices_ProductId", "serialised_devices", "ProductId");

        migrationBuilder.CreateTable("inventory_movements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                MovementType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_inventory_movements", x => x.Id);
                table.ForeignKey("FK_inventory_movements_serialised_devices_DeviceId", x => x.DeviceId, "serialised_devices", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_inventory_movements_DeviceId_OccurredAt", "inventory_movements", new[] { "DeviceId", "OccurredAt" });
        migrationBuilder.CreateIndex("IX_inventory_movements_TenantId", "inventory_movements", "TenantId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("inventory_movements");
        migrationBuilder.DropTable("serialised_devices");
    }
}
