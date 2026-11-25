using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class StaffAccessReaderTests
{
    [Fact]
    public async Task Membership_IsBoundToValidatedSubjectTenantAndActiveStatus()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseSqlite(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var alpha = new RetailTenant { Id = Guid.NewGuid(), Name = "Alpha", Slug = "alpha" };
        var beta = new RetailTenant { Id = Guid.NewGuid(), Name = "Beta", Slug = "beta" };
        var user = new RetailUser { Id = Guid.NewGuid(), Email = "person@example.test" };
        db.AddRange(alpha, beta, user);
        db.ExternalAccounts.Add(new ExternalAccount { Id = Guid.NewGuid(), UserId = user.Id, Issuer = "https://issuer.example.test", Subject = "subject-123" });
        db.Memberships.Add(new TenantMembership { Id = Guid.NewGuid(), TenantId = alpha.Id, UserId = user.Id, Role = "Owner" });
        await db.SaveChangesAsync();
        var reader = new StaffAccessReader(db);

        Assert.Equal("Owner", (await reader.ResolveAsync("https://issuer.example.test", "subject-123", "alpha", CancellationToken.None))?.Role);
        Assert.Null(await reader.ResolveAsync("https://issuer.example.test", "subject-123", "beta", CancellationToken.None));
        Assert.Null(await reader.ResolveAsync("https://issuer.example.test", "wrong", "alpha", CancellationToken.None));
        Assert.Null(await reader.ResolveAsync("https://different-issuer.example.test", "subject-123", "alpha", CancellationToken.None));
        user.IsActive = false;
        await db.SaveChangesAsync();
        Assert.Null(await reader.ResolveAsync("https://issuer.example.test", "subject-123", "alpha", CancellationToken.None));
    }
}
