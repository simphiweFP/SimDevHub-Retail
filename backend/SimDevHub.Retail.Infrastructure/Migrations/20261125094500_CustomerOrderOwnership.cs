using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace SimDevHub.Retail.Infrastructure.Migrations;

[DbContext(typeof(RetailDbContext))]
[Migration("20261125094500_CustomerOrderOwnership")]
public sealed class CustomerOrderOwnership : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        // Existing orders remain unowned and cannot be claimed from the API.
        m.AddColumn<Guid>("CustomerUserId", "retail_orders", type: "uuid", nullable: true);
        m.CreateIndex("IX_retail_orders_TenantId_CustomerUserId_CreatedAt",
            "retail_orders", new[] { "TenantId", "CustomerUserId", "CreatedAt" });
        m.CreateIndex("IX_retail_orders_CustomerUserId",
            "retail_orders", "CustomerUserId");
        m.AddForeignKey("FK_retail_orders_retail_users_CustomerUserId",
            "retail_orders", "CustomerUserId", "retail_users", "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropForeignKey("FK_retail_orders_retail_users_CustomerUserId", "retail_orders");
        m.DropIndex("IX_retail_orders_TenantId_CustomerUserId_CreatedAt", "retail_orders");
        m.DropIndex("IX_retail_orders_CustomerUserId", "retail_orders");
        m.DropColumn("CustomerUserId", "retail_orders");
    }
}
