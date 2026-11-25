namespace SimDevHub.Retail.Application;

/// <summary>
/// Order ownership is a stable internal user ID, not email or an OAuth claim.
/// Unowned historical orders are never exposed in customer order history.
/// </summary>
public sealed record CustomerOrderSummary(
    Guid OrderId, string Status, decimal Amount, string Currency,
    DateTimeOffset CreatedAt, DateTimeOffset ReservedUntil);

public interface ICustomerOrderAccess
{
    Task<IReadOnlyList<CustomerOrderSummary>> GetOwnedOrdersAsync(
        string issuer, string subject, string storeSlug, int take, CancellationToken ct);

    Task<bool> OwnsOrderAsync(
        string issuer, string subject, Guid tenantId, Guid orderId, CancellationToken ct);
}
