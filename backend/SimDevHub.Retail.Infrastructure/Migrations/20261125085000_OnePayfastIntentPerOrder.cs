using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace SimDevHub.Retail.Infrastructure.Migrations;

[DbContext(typeof(RetailDbContext))]
[Migration("20261125085000_OnePayfastIntentPerOrder")]
public sealed class OnePayfastIntentPerOrder : Migration
{
    protected override void Up(MigrationBuilder m) =>
        m.CreateIndex(
            name: "IX_payment_intents_OnePayfastSandboxPerOrder",
            table: "payment_intents",
            column: "OrderId",
            unique: true,
            filter: "\"Provider\" = 'PayfastSandbox'");

    protected override void Down(MigrationBuilder m) =>
        m.DropIndex("IX_payment_intents_OnePayfastSandboxPerOrder", "payment_intents");
}
