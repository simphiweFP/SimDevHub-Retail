namespace SimDevHub.Retail.Application;

/// <summary>
/// Call only with issuer/subject/email_verified from a validated OIDC token,
/// never values supplied by a request body. Email is descriptive, not identity.
/// </summary>
public sealed record VerifiedCustomerIdentity(string Issuer, string Subject, string VerifiedEmail);
public enum CustomerOnboardingStatus { Created, Existing, Invalid, Disabled, Conflict }
public sealed record CustomerOnboardingResult(CustomerOnboardingStatus Status, Guid? UserId = null);

public interface ICustomerOnboarding
{
    Task<CustomerOnboardingResult> RegisterAsync(VerifiedCustomerIdentity identity, CancellationToken ct);
}
