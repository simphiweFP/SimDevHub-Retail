using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class StaffInvitationServiceTests
{
    [Fact]
    public async Task VerifiedIdentity_CanRedeemTokenOnce_ForItsIntendedTenant()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseSqlite(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var alpha = new RetailTenant { Id = Guid.NewGuid(), Name = "Alpha", Slug = "alpha" };
        var beta = new RetailTenant { Id = Guid.NewGuid(), Name = "Beta", Slug = "beta" };
        db.Tenants.AddRange(alpha, beta);
        await db.SaveChangesAsync();

        var service = new StaffInvitationService(db, TimeProvider.System);
        var invitation = await service.CreateAsync(alpha.Id, "Member@Example.test", "Cashier", TimeSpan.FromHours(2), CancellationToken.None);
        Assert.NotNull(invitation);
        Assert.Equal(64, invitation.Token.Length);

        var identity = new VerifiedStaffIdentity("https://login.example.test", "subject-001", "member@example.test");
        Assert.Equal(InvitationAcceptStatus.Invalid, await service.AcceptAsync(
            invitation.Token, identity with { VerifiedEmail = "other@example.test" }, CancellationToken.None));
        Assert.Equal(InvitationAcceptStatus.Accepted, await service.AcceptAsync(invitation.Token, identity, CancellationToken.None));
        Assert.Equal(InvitationAcceptStatus.Invalid, await service.AcceptAsync(invitation.Token, identity, CancellationToken.None));
        Assert.Single(await db.Memberships.Where(x => x.TenantId == alpha.Id).ToListAsync());
        Assert.Empty(await db.Memberships.Where(x => x.TenantId == beta.Id).ToListAsync());
        Assert.NotEqual(invitation.Token, (await db.StaffInvitations.SingleAsync()).TokenHash);
        Assert.Equal("Cashier", (await db.Memberships.SingleAsync()).Role);

        Assert.Null(await service.CreateAsync(alpha.Id, "member@example.test", "Owner", TimeSpan.FromHours(2), CancellationToken.None));
        var expired = await service.CreateAsync(alpha.Id, "member@example.test", "Manager", TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.NotNull(expired);
        // Revoke by expiring the invitation (simulates clock advancing).
        var pending = await db.StaffInvitations.SingleAsync(x => x.Id == expired.Id);
        pending.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();
        Assert.Equal(InvitationAcceptStatus.Invalid, await service.AcceptAsync(expired.Token, identity, CancellationToken.None));
    }
}
