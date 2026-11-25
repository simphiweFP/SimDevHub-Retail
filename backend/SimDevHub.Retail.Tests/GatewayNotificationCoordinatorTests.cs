using System.Security.Cryptography;
using System.Text;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class GatewayNotificationCoordinatorTests
{
    [Fact]
    public async Task MissingGatewayFailsClosedWithoutCallingProcessor()
    {
        var processor = new SpyProcessor();
        var coordinator = new GatewayNotificationCoordinator(Array.Empty<IPaymentGatewayVerifier>(), processor);
        var received = await coordinator.HandleAsync(
            new GatewayNotification("TestSandbox", Encoding.UTF8.GetBytes("event"), new Dictionary<string, string>()),
            CancellationToken.None);
        Assert.Equal(GatewayNotificationStatus.ProviderUnavailable, received.Status);
        Assert.Equal(0, processor.Calls);
    }

    [Fact]
    public async Task TamperedNotificationIsRejected_ValidSignedFixtureIsForwardedOnce()
    {
        var processor = new SpyProcessor();
        var verifier = new TestOnlyHmacVerifier();
        var coordinator = new GatewayNotificationCoordinator(new IPaymentGatewayVerifier[] { verifier }, processor);
        var payload = Encoding.UTF8.GetBytes("fixture-approved-payment");
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("local-test-secret-not-production"), payload));
        var headers = new Dictionary<string, string> { ["X-Test-Signature"] = signature };
        var good = new GatewayNotification("TestSandbox", payload, headers);
        var bad = new GatewayNotification("TestSandbox", Encoding.UTF8.GetBytes("tampered"), headers);
        Assert.Equal(GatewayNotificationStatus.VerificationFailed,
            (await coordinator.HandleAsync(bad, CancellationToken.None)).Status);
        Assert.Equal(0, processor.Calls);

        var accepted = await coordinator.HandleAsync(good, CancellationToken.None);
        Assert.Equal(GatewayNotificationStatus.Processed, accepted.Status);
        Assert.Equal(PaymentConfirmationResult.Paid, accepted.PaymentResult);
        Assert.Equal(1, processor.Calls);
        Assert.NotNull(processor.LastEvent);
        Assert.Equal("TestSandbox", processor.LastEvent.Provider);
    }

    [Fact]
    public async Task DuplicateVerifiersAndProviderMismatchFailClosed()
    {
        var processor = new SpyProcessor();
        var verifier = new TestOnlyHmacVerifier();
        var duplicate = new GatewayNotificationCoordinator(
            new IPaymentGatewayVerifier[] { verifier, verifier }, processor);
        var notification = new GatewayNotification(
            "TestSandbox", Encoding.UTF8.GetBytes("fixture-approved-payment"),
            new Dictionary<string, string>());
        Assert.Equal(GatewayNotificationStatus.ProviderUnavailable,
            (await duplicate.HandleAsync(notification, CancellationToken.None)).Status);
        Assert.Equal(0, processor.Calls);
    }

    private sealed class SpyProcessor : IVerifiedPaymentProcessor
    {
        public int Calls { get; private set; }
        public VerifiedPaymentEvent? LastEvent { get; private set; }
        public Task<PaymentConfirmationResult> ConfirmAsync(
            VerifiedPaymentEvent payment, CancellationToken cancellationToken)
        {
            Calls++;
            LastEvent = payment;
            return Task.FromResult(PaymentConfirmationResult.Paid);
        }
    }

    // This fake is compiled only into the test assembly. Real gateways must
    // implement their own provider-specific callback and status verification.
    private sealed class TestOnlyHmacVerifier : IPaymentGatewayVerifier
    {
        public string Provider => "TestSandbox";

        public Task<VerifiedPaymentEvent?> VerifyAsync(
            GatewayNotification notification, CancellationToken ct)
        {
            if (!notification.Headers.TryGetValue("X-Test-Signature", out var signature) ||
                !signature.All(Uri.IsHexDigit))
                return Task.FromResult<VerifiedPaymentEvent?>(null);
            var signatureBytes = Convert.FromHexString(signature);
            var expected = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes("local-test-secret-not-production"),
                notification.Body.Span);
            if (!CryptographicOperations.FixedTimeEquals(signatureBytes, expected))
                return Task.FromResult<VerifiedPaymentEvent?>(null);
            // A fixed fixture substitutes for the independent provider lookup.
            // Never register this verifier in a real API.
            var payment = new VerifiedPaymentEvent(
                Guid.Parse("a91ff253-2d18-4b16-8ab5-6b71a19a33da"),
                Guid.Parse("b21cc2e2-2f64-4e5e-b9ef-8851489e3221"),
                Guid.Parse("c31dd3f1-faf4-4a1a-ad53-b9f37ac91211"),
                Provider, "test-event-001", 123m, "ZAR");
            return Task.FromResult<VerifiedPaymentEvent?>(payment);
        }
    }
}
