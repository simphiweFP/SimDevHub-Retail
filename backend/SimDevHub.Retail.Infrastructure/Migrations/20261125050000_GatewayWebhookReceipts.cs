using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace SimDevHub.Retail.Infrastructure.Migrations;
[DbContext(typeof(RetailDbContext))]
[Migration("20261125050000_GatewayWebhookReceipts")]
public sealed class GatewayWebhookReceipts : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("gateway_webhook_receipts",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                Provider = t.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                BodySha256 = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Status = t.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                AttemptCount = t.Column<int>(type: "integer", nullable: false),
                ReceivedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastClaimedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CompletedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: t => t.PrimaryKey("PK_gateway_webhook_receipts", x => x.Id));
        m.CreateIndex("IX_gateway_webhook_receipts_Provider_BodySha256",
            "gateway_webhook_receipts", new[] { "Provider", "BodySha256" }, unique: true);
        m.CreateIndex("IX_gateway_webhook_receipts_Status_LastClaimedAt",
            "gateway_webhook_receipts", new[] { "Status", "LastClaimedAt" });
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("gateway_webhook_receipts");
}
