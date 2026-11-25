namespace SimDevHub.Retail.Application;

public sealed record StaffAccessResult(Guid TenantId, string StoreName, string Role);

/// <summary>Authorisation is derived from validated provider issuer/subject and persisted membership.</summary>
public interface IStaffAccess
{
    Task<StaffAccessResult?> ResolveAsync(string issuer, string subject, string storeSlug, CancellationToken cancellationToken);
}
