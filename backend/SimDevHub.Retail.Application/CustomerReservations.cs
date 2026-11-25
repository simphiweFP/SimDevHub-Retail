namespace SimDevHub.Retail.Application;

/// <summary>
/// Issuer/subject must originate from a validated OIDC token. Store is resolved
/// in the database, never from a user-supplied TenantId or CustomerUserId.
/// </summary>
public sealed record CustomerReserveRequest(
    string Issuer, string Subject, string StoreSlug, Guid ProductId, Guid DeviceId);

public interface ICustomerReservationService
{
    Task<ReserveOrderResult> ReserveAsync(CustomerReserveRequest request, CancellationToken ct);
}

/// <summary>Customer requests one published product; the server picks the physical device.</summary>
public sealed record CustomerProductReserveRequest(string Issuer, string Subject, string StoreSlug, Guid ProductId);
public interface ICustomerProductReservationService
{
    Task<ReserveOrderResult> ReserveOneAsync(CustomerProductReserveRequest request, CancellationToken ct);
}
