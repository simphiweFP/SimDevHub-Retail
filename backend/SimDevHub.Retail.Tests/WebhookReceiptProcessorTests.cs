using System.Text;
using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class WebhookReceiptProcessorTests
{
    [Fact]
    public async Task PersistBeforeProcessingAndDeduplicateDeliveredNotification()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();
        var coordinator = new FakeCoordinator
        {
            Result = new GatewayNotificationResult(
                GatewayNotificationStatus.Processed, PaymentConfirmationResult.Paid)
        };
        var receipt = new WebhookReceiptProcessor(db, coordinator, TimeProvider.System);
        var n = new GatewayNotification("TestGateway",
            Encoding.UTF8.GetBytes("event-" + Guid.NewGuid()),
            new Dictionary<string, string>());
        var first = await receipt.ReceiveAsync(n, CancellationToken.None);
        Assert.Equal(WebhookReceiptStatus.Processed, first.Status);
        Assert.NotNull(first.ReceiptId);
        Assert.Equal(1, coordinator.Calls);
        var saved = await db.GatewayWebhookReceipts.AsNoTracking()
            .SingleAsync(x => x.Id == first.ReceiptId);
        Assert.Equal("Completed", saved.Status);
        Assert.Equal(1, saved.AttemptCount);
        Assert.Equal(64, saved.BodySha256.Length);
        Assert.NotNull(saved.CompletedAt);
        var again = await receipt.ReceiveAsync(n, CancellationToken.None);
        Assert.Equal(WebhookReceiptStatus.AlreadyProcessed, again.Status);
        Assert.Equal(first.ReceiptId, again.ReceiptId);
        Assert.Equal(1, coordinator.Calls);
        Assert.Equal(1, await db.GatewayWebhookReceipts.CountAsync(x => x.Provider == "TestGateway"
            && x.BodySha256 == saved.BodySha256));
    }

    [Fact]
    public async Task RetryOnlyWithRedelivery_AndNeverTreatUnavailableProviderAsPaid()
    {
        var connection = Environment.GetEnvironmentVariable("RETAIL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var options = new DbContextOptionsBuilder<RetailDbContext>().UseNpgsql(connection).Options;
        await using var db = new RetailDbContext(options);
        await db.Database.MigrateAsync();

        var coordinator = new FakeCoordinator
        {
            Result = new GatewayNotificationResult(GatewayNotificationStatus.ProviderUnavailable)
        };
        var service = new WebhookReceiptProcessor(db, coordinator, TimeProvider.System);
        var notification = new GatewayNotification("NotRegistered",
            Encoding.UTF8.GetBytes("retry-" + Guid.NewGuid()),
            new Dictionary<string, string>());
        var first = await service.ReceiveAsync(notification, CancellationToken.None);
        Assert.Equal(WebhookReceiptStatus.RetryPending, first.Status);
        Assert.Equal("RetryPending", (await db.GatewayWebhookReceipts.AsNoTracking()
            .SingleAsync(x => x.Id == first.ReceiptId)).Status);

        coordinator.Result = new GatewayNotificationResult(
            GatewayNotificationStatus.Processed, PaymentConfirmationResult.ReviewRequired);
        var second = await service.ReceiveAsync(notification, CancellationToken.None);
        Assert.Equal(WebhookReceiptStatus.Processed, second.Status);
        Assert.Equal(first.ReceiptId, second.ReceiptId);
        Assert.Equal(2, coordinator.Calls);
        Assert.Equal(2, (await db.GatewayWebhookReceipts.AsNoTracking()
            .SingleAsync(x => x.Id == second.ReceiptId)).AttemptCount);
        Assert.Equal(WebhookReceiptStatus.Rejected,
            (await service.ReceiveAsync(notification with { Body = ReadOnlyMemory<byte>.Empty },
                CancellationToken.None)).Status);
    }

    private sealed class FakeCoordinator : IGatewayNotificationCoordinator
    {
        public int Calls { get; private set; }
        public required GatewayNotificationResult Result { get; set; }
        public Task<GatewayNotificationResult> HandleAsync(GatewayNotification notification, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }
}
