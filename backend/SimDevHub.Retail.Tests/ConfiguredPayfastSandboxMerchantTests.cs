using Microsoft.Extensions.Configuration;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class ConfiguredPayfastSandboxMerchantTests
{
    [Fact]
    public async Task MerchantSecretsAreIsolatedByTenantAndRequireExplicitEnablement()
    {
        var alpha = Guid.NewGuid();
        var beta = Guid.NewGuid();
        var alphaPrefix = $"Payments:PayfastSandbox:Merchants:{alpha:N}";
        var betaPrefix = $"Payments:PayfastSandbox:Merchants:{beta:N}";

        var values = new Dictionary<string, string?>
        {
            [$"{alphaPrefix}:Enabled"] = "true",
            [$"{alphaPrefix}:MerchantId"] = "10000100",
            [$"{alphaPrefix}:Passphrase"] = "alpha-secret-only",
            [$"{betaPrefix}:Enabled"] = "false",
            [$"{betaPrefix}:MerchantId"] = "20000200",
            [$"{betaPrefix}:Passphrase"] = "beta-secret-only",
            // A global setting must never substitute for a tenant configuration.
            ["Payments:PayfastSandbox:MerchantId"] = "99999999",
            ["Payments:PayfastSandbox:Passphrase"] = "unsafe-global-fallback"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var resolver = new ConfiguredPayfastSandboxMerchant(config);

        var a = await resolver.GetCredentialsAsync(alpha, CancellationToken.None);
        Assert.NotNull(a);
        Assert.Equal("10000100", a.MerchantId);
        Assert.Equal("alpha-secret-only", a.Passphrase);
        Assert.Null(await resolver.GetCredentialsAsync(beta, CancellationToken.None));
        Assert.Null(await resolver.GetCredentialsAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Null(await resolver.GetCredentialsAsync(Guid.Empty, CancellationToken.None));

        values[$"{betaPrefix}:Enabled"] = "true";
        config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        resolver = new ConfiguredPayfastSandboxMerchant(config);
        var b = await resolver.GetCredentialsAsync(beta, CancellationToken.None);
        Assert.NotNull(b);
        Assert.Equal("20000200", b.MerchantId);
        Assert.Equal("beta-secret-only", b.Passphrase);
        Assert.NotEqual(a.Passphrase, b.Passphrase);
    }

    [Theory]
    [InlineData("true", "invalid", "secret")]
    [InlineData("true", "12345678", "")]
    [InlineData("false", "12345678", "secret")]
    [InlineData(null, "12345678", "secret")]
    public async Task InvalidOrUnenabledTenantCredentialsFailClosed(
        string? enabled, string merchantId, string passphrase)
    {
        var tenant = Guid.NewGuid();
        var prefix = $"Payments:PayfastSandbox:Merchants:{tenant:N}";
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"{prefix}:Enabled"] = enabled,
                [$"{prefix}:MerchantId"] = merchantId,
                [$"{prefix}:Passphrase"] = passphrase
            }).Build();
        Assert.Null(await new ConfiguredPayfastSandboxMerchant(config)
            .GetCredentialsAsync(tenant, CancellationToken.None));
    }
}
