using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimDevHub.Retail.Infrastructure.Migrations;

[DbContext(typeof(RetailDbContext))]
[Migration("20261124190000_VerifiedPayments")]
public sealed class VerifiedPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("payment_events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                Provider = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                ProviderEventId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payment_events", x => x.Id);
                table.ForeignKey("FK_payment_events_retail_orders_OrderId", x => x.OrderId, "retail_orders", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_payment_events_Provider_ProviderEventId", "payment_events",
            new[] { "Provider", "ProviderEventId" }, unique: true);
        migrationBuilder.CreateIndex("IX_payment_events_TenantId_OrderId", "payment_events",
            new[] { "TenantId", "OrderId" });
        migrationBuilder.CreateIndex("IX_payment_events_OrderId", "payment_events", "OrderId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("payment_events");
}
