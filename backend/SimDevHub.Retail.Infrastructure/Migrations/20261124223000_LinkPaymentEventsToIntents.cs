using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace SimDevHub.Retail.Infrastructure.Migrations;

[DbContext(typeof(RetailDbContext))]
[Migration("20261124223000_LinkPaymentEventsToIntents")]
public sealed class LinkPaymentEventsToIntents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Nullable because historical payment events predate payment intents.
        // New events require a valid intent at the application boundary.
        migrationBuilder.AddColumn<Guid>(
            name: "PaymentIntentId", table: "payment_events", type: "uuid", nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_payment_events_PaymentIntentId",
            table: "payment_events", column: "PaymentIntentId");
        migrationBuilder.AddForeignKey(
            name: "FK_payment_events_payment_intents_PaymentIntentId",
            table: "payment_events", column: "PaymentIntentId",
            principalTable: "payment_intents", principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_payment_events_payment_intents_PaymentIntentId", "payment_events");
        migrationBuilder.DropIndex("IX_payment_events_PaymentIntentId", "payment_events");
        migrationBuilder.DropColumn("PaymentIntentId", "payment_events");
    }
}
