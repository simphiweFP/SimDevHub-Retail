using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;

namespace SimDevHub.Retail.Infrastructure;

public sealed class StaffAccessReader(RetailDbContext db) : IStaffAccess
{
    public Task<StaffAccessResult?> ResolveAsync(
        string issuer, string subject, string storeSlug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject) ||
            string.IsNullOrWhiteSpace(storeSlug))
            return Task.FromResult<StaffAccessResult?>(null);

        return db.Memberships.AsNoTracking()
            .Where(m => m.IsActive && m.User.IsActive && m.Tenant.IsActive &&
                        m.Tenant.Slug == storeSlug &&
                        m.User.ExternalAccounts.Any(e => e.Issuer == issuer && e.Subject == subject))
            .Select(m => new StaffAccessResult(m.TenantId, m.Tenant.Name, m.Role))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
