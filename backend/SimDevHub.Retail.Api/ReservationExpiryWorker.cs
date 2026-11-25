using SimDevHub.Retail.Application;

namespace SimDevHub.Retail.Api;

/// <summary>
/// Opt-in hosted sweeper. Each cycle creates a fresh DI scope / DbContext;
/// PostgreSQL conditional transitions keep multiple application instances safe.
/// The first sweep runs at startup and subsequent sweeps use a bounded interval.
/// </summary>
public sealed class ReservationExpiryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationExpiryWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = configuration.GetValue<int>("Retail:ReservationExpiry:IntervalSeconds", 30);
        var batch = configuration.GetValue<int>("Retail:ReservationExpiry:BatchSize", 100);
        // Configuration mistakes fail closed at registration/startup too.
        if (seconds is < 15 or > 300 || batch is < 1 or > 200)
            throw new InvalidOperationException("Reservation expiry configuration is out of range.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var expiry = scope.ServiceProvider.GetRequiredService<IReservationExpiry>();
                var count = await expiry.ReleaseExpiredAsync(batch, stoppingToken);
                if (count != 0)
                    logger.LogInformation("Reservation expiry released {Count} abandoned orders.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never stop sweeping on a transient database outage, and never
                // log customer identifiers or payment webhook payloads.
                logger.LogError(ex, "Reservation expiry sweep failed; will retry next interval.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
