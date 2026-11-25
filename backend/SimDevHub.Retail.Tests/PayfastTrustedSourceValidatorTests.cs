using System.Net;
using Microsoft.Extensions.Configuration;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class PayfastTrustedSourceValidatorTests
{
    private static PayfastTrustedSourceValidator Validator(string configValue)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Payments:PayfastSandbox:TrustedSourceCidrs"] = configValue
        }).Build();
        return new PayfastTrustedSourceValidator(config);
    }

    private static GatewayNotification Source(string? ip) =>
        new("PayfastSandbox", new byte[] { 1 }, new Dictionary<string, string>(),
            ip is null ? null : IPAddress.Parse(ip));

    [Fact]
    public async Task AcceptsOnlyConfiguredPeerAddressAndNeverHeaderClaims()
    {
        var v = Validator("197.97.145.144/28,144.126.193.139");
        Assert.True(v.IsConfigured);
        Assert.True(await v.IsTrustedSourceAsync(Source("197.97.145.150"), CancellationToken.None));
        Assert.True(await v.IsTrustedSourceAsync(Source("144.126.193.139"), CancellationToken.None));
        Assert.False(await v.IsTrustedSourceAsync(Source("197.97.145.160"), CancellationToken.None));
        Assert.False(await v.IsTrustedSourceAsync(Source("41.74.179.210"), CancellationToken.None));
        Assert.False(await v.IsTrustedSourceAsync(Source(null), CancellationToken.None));

        var forged = Source("203.0.113.9") with
        {
            Headers = new Dictionary<string, string>
            {
                ["X-Forwarded-For"] = "197.97.145.150", ["Referer"] = "https://sandbox.payfast.co.za/"
            }
        };
        Assert.False(await v.IsTrustedSourceAsync(forged, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0.0.0.0/0")]
    [InlineData("197.97.0.0/16")]
    [InlineData("127.0.0.1")]
    [InlineData("invalid")]
    [InlineData("197.97.145.144/28,invalid")]
    [InlineData("2001:db8::/1")]
    public async Task InvalidOrOverBroadAllowlistDisablesAllAccess(string cidrs)
    {
        var validator = Validator(cidrs);
        Assert.False(validator.IsConfigured);
        Assert.False(await validator.IsTrustedSourceAsync(
            Source("197.97.145.150"), CancellationToken.None));
    }
}
