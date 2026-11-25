using System.Text;
using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class WebhookReceiptConcurrencyTests
{
    [Fact]
    public async Task ParallelIdenticalDeliveries_OnlyOneRunsGatewayVerification()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using (var setup = new RetailDbContext(options))
            await setup.Database.MigrateAsync();

        var gate = new BlockingCoordinator();
        var notification = new GatewayNotification("TestGateway",
            Encoding.UTF8.GetBytes("parallel-receipt-" + Guid.NewGuid()),
            new Dictionary<string, string>());

        async Task<WebhookReceiptResult> Deliver()
        {
            await using var db = new RetailDbContext(options);
            return await new WebhookReceiptProcessor(db, gate, TimeProvider.System)
                .ReceiveAsync(notification, CancellationToken.None);
        }

        var first = Deliver();
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        // While first delivery is processing, a second connection must NOT
        // execute gateway validation or allocate another order.
        var concurrent = await Deliver().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(WebhookReceiptStatus.Busy, concurrent.Status);
        Assert.Equal(1, gate.Calls);

        gate.Release.TrySetResult();
        var completed = await first.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(WebhookReceiptStatus.Processed, completed.Status);
        Assert.Equal(concurrent.ReceiptId, completed.ReceiptId);

        var repeated = await Deliver();
        Assert.Equal(WebhookReceiptStatus.AlreadyProcessed, repeated.Status);
        Assert.Equal(1, gate.Calls);
        await using var verify = new RetailDbContext(options);
        Assert.Equal(1, await verify.GatewayWebhookReceipts.CountAsync(
            x => x.Id == completed.ReceiptId && x.Status == "Completed" && x.AttemptCount == 1));
    }

    [Fact]
    public async Task StaleLeaseCanBeReclaimedButRecentClaimCannot()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var notification = new GatewayNotification("TestGateway",
            Encoding.UTF8.GetBytes("stale-lease-" + Guid.NewGuid()),
            new Dictionary<string, string>());
        var gateway = new ImmediateCoordinator();
        var service = new WebhookReceiptProcessor(db, gateway, TimeProvider.System);
        var inserted = await service.ReceiveAsync(notification, CancellationToken.None);
        Assert.Equal(WebhookReceiptStatus.Processed, inserted.Status);

        // Simulate a previous process having crashed after claiming the receipt.
        await db.GatewayWebhookReceipts.Where(x => x.Id == inserted.ReceiptId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(r => r.Status, "Processing")
                .SetProperty(r => r.LastClaimedAt, DateTimeOffset.UtcNow));

        Assert.Equal(WebhookReceiptStatus.Busy,
            (await service.ReceiveAsync(notification, CancellationToken.None)).Status);

        await db.GatewayWebhookReceipts.Where(x => x.Id == inserted.ReceiptId)
            .ExecuteUpdateAsync(x => x
                .SetProperty(r => r.LastClaimedAt, DateTimeOffset.UtcNow.AddMinutes(-10)));
        Assert.Equal(WebhookReceiptStatus.Processed,
            (await service.ReceiveAsync(notification, CancellationToken.None)).Status);

        var state = await db.GatewayWebhookReceipts.AsNoTracking()
            .SingleAsync(x => x.Id == inserted.ReceiptId);
        Assert.Equal(2, state.AttemptCount);
        Assert.Equal("Completed", state.Status);
    }

    [Fact]
    public async Task StaleWorkerCannotOverwriteNewerClaimOrReportSuccess()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using (var setup = new RetailDbContext(options))
            await setup.Database.MigrateAsync();

        var notification = new GatewayNotification("TestGateway",
            Encoding.UTF8.GetBytes("stale-worker-" + Guid.NewGuid()),
            new Dictionary<string, string>());
        var slowGateway = new BlockingCoordinator();
        async Task<WebhookReceiptResult> WithGateway(IGatewayNotificationCoordinator gateway)
        {
            await using var db = new RetailDbContext(options);
            return await new WebhookReceiptProcessor(db, gateway, TimeProvider.System)
                .ReceiveAsync(notification, CancellationToken.None);
        }

        var oldWorker = WithGateway(slowGateway);
        await slowGateway.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await using (var db = new RetailDbContext(options))
        {
            // Model the five-minute lease having elapsed while the first
            // provider verifier is still blocked.
            await db.GatewayWebhookReceipts.Where(x => x.Provider == "TestGateway" &&
                    x.Status == "Processing")
                .Where(x => x.BodySha256 ==
                    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(notification.Body.Span)))
                .ExecuteUpdateAsync(x => x.SetProperty(
                    r => r.LastClaimedAt, DateTimeOffset.UtcNow.AddMinutes(-10)));
        }

        var newWorker = await WithGateway(new ImmediateCoordinator())
            .WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(WebhookReceiptStatus.Processed, newWorker.Status);
        slowGateway.Release.TrySetResult();
        var staleResult = await oldWorker.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(WebhookReceiptStatus.Busy, staleResult.Status);
        Assert.Equal(newWorker.ReceiptId, staleResult.ReceiptId);

        await using var verify = new RetailDbContext(options);
        var receipt = await verify.GatewayWebhookReceipts.AsNoTracking()
            .SingleAsync(x => x.Id == newWorker.ReceiptId);
        Assert.Equal("Completed", receipt.Status);
        Assert.Equal(2, receipt.AttemptCount);
    }

    private sealed class BlockingCoordinator : IGatewayNotificationCoordinator
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<GatewayNotificationResult> HandleAsync(
            GatewayNotification notification, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(ct);
            return new(GatewayNotificationStatus.Processed, PaymentConfirmationResult.Paid);
        }
    }

    private sealed class ImmediateCoordinator : IGatewayNotificationCoordinator
    {
        public Task<GatewayNotificationResult> HandleAsync(
            GatewayNotification notification, CancellationToken ct) =>
            Task.FromResult(new GatewayNotificationResult(
                GatewayNotificationStatus.Processed, PaymentConfirmationResult.Paid));
    }
}
