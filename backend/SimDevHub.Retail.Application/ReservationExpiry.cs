namespace SimDevHub.Retail.Application;

public interface IReservationExpiry
{
    Task<int> ReleaseExpiredAsync(int batchSize, CancellationToken cancellationToken);
}
