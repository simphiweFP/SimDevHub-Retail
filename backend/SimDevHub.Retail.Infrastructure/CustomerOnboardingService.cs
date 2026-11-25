using Microsoft.EntityFrameworkCore;
using Npgsql;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Stable identity is (issuer, subject). Never merge users based on email:
/// another identity with the same verified email is a separate account.
/// Enforces database uniqueness for races across app instances.
/// </summary>
public sealed class CustomerOnboardingService(RetailDbContext db) : ICustomerOnboarding
{
    public async Task<CustomerOnboardingResult> RegisterAsync(
        VerifiedCustomerIdentity identity, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(identity.Issuer) || identity.Issuer.Length > 320 ||
            !Uri.TryCreate(identity.Issuer, UriKind.Absolute, out var issuerUri) ||
            issuerUri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(identity.Subject) || identity.Subject.Length > 320 ||
            string.IsNullOrWhiteSpace(identity.VerifiedEmail))
            return new(CustomerOnboardingStatus.Invalid);

        var email = identity.VerifiedEmail.Trim().ToLowerInvariant();
        if (email.Length is < 3 or > 320 ||
            email.Any(char.IsWhiteSpace) ||
            email.IndexOf('@') is <= 0 ||
            email.LastIndexOf('@') == email.Length - 1)
            return new(CustomerOnboardingStatus.Invalid);

        var existing = await db.ExternalAccounts.AsNoTracking()
            .Where(x => x.Issuer == identity.Issuer && x.Subject == identity.Subject)
            .Select(x => new { x.UserId, x.User.Email, x.User.IsActive })
            .SingleOrDefaultAsync(ct);
        if (existing is not null)
            return existing.IsActive &&
                string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase)
                ? new(CustomerOnboardingStatus.Existing, existing.UserId)
                : new(CustomerOnboardingStatus.Conflict);

        // Separate user even if the email matches another record; no implicit
        // cross-provider identity linking or privilege inheritance.
        var user = new RetailUser { Id = Guid.NewGuid(), Email = email, IsActive = true };
        db.Users.Add(user);
        db.ExternalAccounts.Add(new ExternalAccount
        {
            Id = Guid.NewGuid(), UserId = user.Id,
            Issuer = identity.Issuer, Subject = identity.Subject
        });

        try
        {
            await db.SaveChangesAsync(ct);
            return new(CustomerOnboardingStatus.Created, user.Id);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another instance won the same issuer/subject race.
            // Never claim this attempt created a user or merge by email.
            db.ChangeTracker.Clear();
            var winner = await db.ExternalAccounts.AsNoTracking()
                .Where(x => x.Issuer == identity.Issuer && x.Subject == identity.Subject)
                .Select(x => new { x.UserId, x.User.Email, x.User.IsActive })
                .SingleOrDefaultAsync(ct);
            return winner is not null && winner.IsActive &&
                string.Equals(winner.Email, email, StringComparison.OrdinalIgnoreCase)
                ? new(CustomerOnboardingStatus.Existing, winner.UserId)
                : new(CustomerOnboardingStatus.Conflict);
        }
    }
}
