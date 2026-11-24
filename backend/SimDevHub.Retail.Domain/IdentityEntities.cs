namespace SimDevHub.Retail.Domain;

/// <summary>Stable internal account identity. Email is not an authorisation key.</summary>
public sealed class RetailUser
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ExternalAccount> ExternalAccounts { get; set; } = new List<ExternalAccount>();
    public ICollection<TenantMembership> Memberships { get; set; } = new List<TenantMembership>();
}

public sealed class ExternalAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Issuer { get; set; }
    public required string Subject { get; set; }
    public RetailUser User { get; set; } = null!;
}

public sealed class TenantMembership
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public required string Role { get; set; }
    public bool IsActive { get; set; } = true;
    public RetailUser User { get; set; } = null!;
    public RetailTenant Tenant { get; set; } = null!;
}

public sealed class StaffInvitation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Email { get; set; }
    public required string Role { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public RetailTenant Tenant { get; set; } = null!;
}
