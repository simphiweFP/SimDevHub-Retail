using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Staff invitations are generated for trusted internal workflows only; the
/// one-time raw token must be delivered using a protected notification channel.
/// Never log invitation tokens or use them as URL query parameters.
/// </summary>
public sealed class StaffInvitationService(RetailDbContext db, TimeProvider clock) : IStaffInvitations
{
    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.Ordinal) { "Manager", "Cashier", "StockController", "PurchasingOfficer", "FinanceOfficer", "DeliveryDriver" };

    public async Task<CreatedStaffInvitation?> CreateAsync(
        Guid tenantId, string email, string role, TimeSpan lifetime, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 320 || !normalized.Contains('@') ||
            !AllowedRoles.Contains(role) || lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromDays(7))
            return null;
        if (!await db.Tenants.AnyAsync(x => x.Id == tenantId && x.IsActive, ct))
            return null;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expires = clock.GetUtcNow().Add(lifetime);
        var record = new StaffInvitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = normalized,
            Role = role,
            TokenHash = Hash(token),
            ExpiresAt = expires
        };
        db.StaffInvitations.Add(record);
        await db.SaveChangesAsync(ct);
        return new CreatedStaffInvitation(record.Id, token, expires);
    }

    public async Task<InvitationAcceptStatus> AcceptAsync(
        string token, VerifiedStaffIdentity identity, CancellationToken ct)
    {
        // No email-only activation, no unverified social account, and no anonymous redemption.
        if (token.Length != 64 || !token.All(Uri.IsHexDigit) ||
            string.IsNullOrWhiteSpace(identity.Issuer) ||
            string.IsNullOrWhiteSpace(identity.Subject) ||
            string.IsNullOrWhiteSpace(identity.VerifiedEmail))
            return InvitationAcceptStatus.Invalid;

        var verifiedEmail = identity.VerifiedEmail.Trim().ToLowerInvariant();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var hash = Hash(token);
        var invite = await db.StaffInvitations.AsNoTracking()
            .Include(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (invite is null || invite.AcceptedAt is not null ||
            invite.ExpiresAt <= clock.GetUtcNow() || !invite.Tenant.IsActive ||
            !AllowedRoles.Contains(invite.Role) ||
            !string.Equals(invite.Email, verifiedEmail, StringComparison.Ordinal))
            return InvitationAcceptStatus.Invalid;

        var existingIdentity = await db.ExternalAccounts
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Issuer == identity.Issuer && x.Subject == identity.Subject, ct);
        if (existingIdentity is not null &&
            (!existingIdentity.User.IsActive ||
             !string.Equals(existingIdentity.User.Email, verifiedEmail, StringComparison.OrdinalIgnoreCase)))
            return InvitationAcceptStatus.Invalid;

        RetailUser user;
        if (existingIdentity is not null)
        {
            user = existingIdentity.User;
        }
        else
        {
            // Deliberately do not link by matching email: a different provider
            // identity must go through an explicit account-linking flow later.
            user = new RetailUser { Id = Guid.NewGuid(), Email = verifiedEmail };
            db.Users.Add(user);
            db.ExternalAccounts.Add(new ExternalAccount
            {
                Id = Guid.NewGuid(), UserId = user.Id,
                Issuer = identity.Issuer, Subject = identity.Subject
            });
        }

        var alreadyMember = existingIdentity is not null &&
            await db.Memberships.AnyAsync(x => x.TenantId == invite.TenantId && x.UserId == user.Id, ct);
        if (alreadyMember) return InvitationAcceptStatus.AlreadyMember;

        // A conditional DB update makes invitation redemption one-time across
        // separate application instances. Membership changes share its transaction.
        var claimed = await db.StaffInvitations.Where(x =>
                x.Id == invite.Id && x.AcceptedAt == null && x.ExpiresAt > clock.GetUtcNow())
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AcceptedAt, clock.GetUtcNow()), ct);
        if (claimed != 1) return InvitationAcceptStatus.Invalid;

        db.Memberships.Add(new TenantMembership
        {
            Id = Guid.NewGuid(), TenantId = invite.TenantId,
            UserId = user.Id, Role = invite.Role, IsActive = true
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return InvitationAcceptStatus.Accepted;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToUpperInvariant())));
}
