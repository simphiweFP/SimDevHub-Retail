using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class PayfastSandboxCheckoutBuilderTests
{
    private static PayfastSandboxCheckoutBuilder.Credentials Credentials =>
        new("10000100", "46f0cd694581a", "local-test-only");

    private static PayfastSandboxCheckoutBuilder.Request Request =>
        new("abcd1234", 6499.00m, "Order abcd1234",
            new Uri("https://demo.example.test/paid"),
            new Uri("https://demo.example.test/cancel"),
            new Uri("https://demo.example.test/webhook"));

    [Fact]
    public void CreatesOnlySandboxPostForm_WithDeterministicProtocolSignature()
    {
        var result = PayfastSandboxCheckoutBuilder.Build(Credentials, Request);
        Assert.Equal("https://sandbox.payfast.co.za/eng/process", result.Action.AbsoluteUri);
        Assert.Equal("6499.00", result.Fields["amount"]);
        Assert.Equal("abcd1234", result.Fields["m_payment_id"]);
        // Independently calculated with form URL encoding and the Payfast field order.
        Assert.Equal("33139768a6b7b395fe1754001f77fef7", result.Fields["signature"]);
        Assert.False(result.Fields.ContainsKey("passphrase"));
        Assert.Equal(9, result.Fields.Count);
    }

    [Fact]
    public void ChangeToAmountOrReferenceChangesSignature()
    {
        var first = PayfastSandboxCheckoutBuilder.Build(Credentials, Request);
        var changedAmount = PayfastSandboxCheckoutBuilder.Build(Credentials, Request with { Amount = 6500m });
        var changedReference = PayfastSandboxCheckoutBuilder.Build(Credentials,
            Request with { MerchantPaymentReference = "abcd1235" });
        Assert.NotEqual(first.Fields["signature"], changedAmount.Fields["signature"]);
        Assert.NotEqual(first.Fields["signature"], changedReference.Fields["signature"]);
    }

    [Fact]
    public void InvalidAmountsAndUnsafeCallbacksAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PayfastSandboxCheckoutBuilder.Build(Credentials, Request with { Amount = 10.001m }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PayfastSandboxCheckoutBuilder.Build(Credentials, Request with { Amount = 0m }));
        Assert.Throws<ArgumentException>(() =>
            PayfastSandboxCheckoutBuilder.Build(Credentials,
                Request with { NotifyUrl = new Uri("http://demo.example.test/webhook") }));
        Assert.Throws<ArgumentException>(() =>
            PayfastSandboxCheckoutBuilder.Build(Credentials,
                Request with { NotifyUrl = new Uri("https://localhost:443/webhook") }));
    }
}
