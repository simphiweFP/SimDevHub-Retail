namespace SimDevHub.Retail.Application;

/// <summary>
/// Caller must supply an identity whose issuer, subject and verified email came
/// from a successfully validated identity-provider token.
/// </summary>
public sealed record VerifiedStaffIdentity(string Issuer, string Subject, string VerifiedEmail);

public sealed record CreatedStaffInvitation(Guid Id, string Token, DateTimeOffset ExpiresAt);
public enum InvitationAcceptStatus { Accepted, Invalid, AlreadyMember }

public interface IStaffInvitations
{
    Task<CreatedStaffInvitation?> CreateAsync(
        Guid tenantId, string email, string role, TimeSpan lifetime, CancellationToken cancellationToken);

    Task<InvitationAcceptStatus> AcceptAsync(
        string token, VerifiedStaffIdentity identity, CancellationToken cancellationToken);
}
