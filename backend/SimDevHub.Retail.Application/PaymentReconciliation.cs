namespace SimDevHub.Retail.Application;

public sealed record PaymentExceptionSummary(
    Guid EventId, Guid OrderId, string Provider, string ProviderEventId,
    decimal Amount, string Currency, DateTimeOffset RecordedAt);

public interface IPaymentReconciliationReader
{
    Task<IReadOnlyList<PaymentExceptionSummary>> GetUnresolvedAsync(
        Guid tenantId, int take, CancellationToken cancellationToken);
}
