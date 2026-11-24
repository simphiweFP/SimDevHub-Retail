using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimDevHub.Retail.Infrastructure.Migrations;

[DbContext(typeof(RetailDbContext))]
[Migration("20261124120000_IdentityFoundation")]
public sealed class IdentityFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("retail_users",
            columns: table => new {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_retail_users", x => x.Id));
        migrationBuilder.CreateIndex("IX_retail_users_Email", "retail_users", "Email");

        migrationBuilder.CreateTable("external_accounts",
            columns: table => new {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Issuer = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                Subject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false)
            }, constraints: table => {
                table.PrimaryKey("PK_external_accounts", x => x.Id);
                table.ForeignKey("FK_external_accounts_retail_users_UserId", x => x.UserId, "retail_users", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_external_accounts_Issuer_Subject", "external_accounts", new[] { "Issuer", "Subject" }, unique: true);
        migrationBuilder.CreateIndex("IX_external_accounts_UserId", "external_accounts", "UserId");

        migrationBuilder.CreateTable("tenant_memberships",
            columns: table => new {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            }, constraints: table => {
                table.PrimaryKey("PK_tenant_memberships", x => x.Id);
                table.ForeignKey("FK_tenant_memberships_retail_users_UserId", x => x.UserId, "retail_users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_tenant_memberships_tenants_TenantId", x => x.TenantId, "tenants", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_tenant_memberships_TenantId_UserId", "tenant_memberships", new[] { "TenantId", "UserId" }, unique: true);
        migrationBuilder.CreateIndex("IX_tenant_memberships_UserId", "tenant_memberships", "UserId");

        migrationBuilder.CreateTable("staff_invitations",
            columns: table => new {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            }, constraints: table => {
                table.PrimaryKey("PK_staff_invitations", x => x.Id);
                table.ForeignKey("FK_staff_invitations_tenants_TenantId", x => x.TenantId, "tenants", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_staff_invitations_TokenHash", "staff_invitations", "TokenHash", unique: true);
        migrationBuilder.CreateIndex("IX_staff_invitations_TenantId_Email", "staff_invitations", new[] { "TenantId", "Email" });
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("staff_invitations");
        migrationBuilder.DropTable("tenant_memberships");
        migrationBuilder.DropTable("external_accounts");
        migrationBuilder.DropTable("retail_users");
    }
}
