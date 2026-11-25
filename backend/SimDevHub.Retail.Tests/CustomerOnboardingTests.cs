using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class CustomerOnboardingTests
{
    [Fact]
    public async Task RepeatedLoginIsIdempotentAndEmailNeverMergesDifferentIdentity()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var service = new CustomerOnboardingService(db);
        var id = Guid.NewGuid().ToString("N");
        var email = "same-" + id + "@example.test";
        var first = new VerifiedCustomerIdentity(
            "https://login.example.test", "customer-" + id, email);
        var created = await service.RegisterAsync(first, CancellationToken.None);
        Assert.Equal(CustomerOnboardingStatus.Created, created.Status);
        Assert.NotNull(created.UserId);
        var again = await service.RegisterAsync(first, CancellationToken.None);
        Assert.Equal(CustomerOnboardingStatus.Existing, again.Status);
        Assert.Equal(created.UserId, again.UserId);

        var otherIssuer = await service.RegisterAsync(first with
        {
            Issuer = "https://other-issuer.example.test"
        }, CancellationToken.None);
        Assert.Equal(CustomerOnboardingStatus.Created, otherIssuer.Status);
        Assert.NotEqual(created.UserId, otherIssuer.UserId);

        var otherSubject = await service.RegisterAsync(first with
        {
            Subject = "different-" + id
        }, CancellationToken.None);
        Assert.Equal(CustomerOnboardingStatus.Created, otherSubject.Status);
        Assert.NotEqual(created.UserId, otherSubject.UserId);
        Assert.NotEqual(otherIssuer.UserId, otherSubject.UserId);

        Assert.Equal(3, await db.Users.CountAsync(x => x.Email == email));
        Assert.Equal(3, await db.ExternalAccounts.CountAsync(
            x => x.Subject == "customer-" + id || x.Subject == "different-" + id));
        Assert.Equal(CustomerOnboardingStatus.Conflict,
            (await service.RegisterAsync(first with {
                VerifiedEmail = "changed-" + id + "@example.test"
            }, CancellationToken.None)).Status);

        await db.Users.Where(x => x.Id == created.UserId)
            .ExecuteUpdateAsync(x => x.SetProperty(u => u.IsActive, false));
        Assert.Equal(CustomerOnboardingStatus.Conflict,
            (await service.RegisterAsync(first, CancellationToken.None)).Status);
        Assert.Equal(0, await db.Memberships.CountAsync(x =>
            x.UserId == otherIssuer.UserId || x.UserId == otherSubject.UserId));
    }

    [Theory]
    [InlineData("http://issuer.test", "abc", "a@b.test")]
    [InlineData("https://issuer.test", "", "a@b.test")]
    [InlineData("https://issuer.test", "abc", "not-an-email")]
    [InlineData("https://issuer.test", "abc", " ")]
    public async Task InvalidVerifiedIdentityCannotCreateUser(
        string issuer, string subject, string email)
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();
        var service = new CustomerOnboardingService(db);
        var result = await service.RegisterAsync(
            new VerifiedCustomerIdentity(issuer, subject, email), CancellationToken.None);
        Assert.Equal(CustomerOnboardingStatus.Invalid, result.Status);
        Assert.Null(result.UserId);
    }

    [Fact]
    public async Task ParallelSameIdentityCannotCreateTwoAccounts()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using (var setup = new RetailDbContext(options))
            await setup.Database.MigrateAsync();
        var key = Guid.NewGuid().ToString("N");
        var identity = new VerifiedCustomerIdentity(
            "https://login.example.test", "race-" + key, "race-" + key + "@example.test");

        async Task<CustomerOnboardingResult> Attempt()
        {
            await using var db = new RetailDbContext(options);
            return await new CustomerOnboardingService(db).RegisterAsync(
                identity, CancellationToken.None);
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(results.Where(x => x.Status == CustomerOnboardingStatus.Created));
        Assert.Single(results.Where(x => x.Status == CustomerOnboardingStatus.Existing));
        Assert.Equal(results[0].UserId, results[1].UserId);
        await using var verify = new RetailDbContext(options);
        Assert.Equal(1, await verify.ExternalAccounts.CountAsync(x =>
            x.Issuer == identity.Issuer && x.Subject == identity.Subject));
    }
}
