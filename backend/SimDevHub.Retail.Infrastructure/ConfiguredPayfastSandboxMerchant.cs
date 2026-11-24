using Microsoft.Extensions.Configuration;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Resolves an explicitly enabled Payfast SANDBOX merchant for one persisted
/// retailer. Never searches by merchant ID or falls back to global settings.
/// Configuration secrets must be supplied through a secret manager or
/// environment variables, not source-controlled JSON.
/// </summary>
public sealed class ConfiguredPayfastSandboxMerchant(IConfiguration configuration)
    : IPayfastSandboxMerchant
{
    public Task<PayfastMerchantSettings?> GetCredentialsAsync(
        Guid tenantId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (tenantId == Guid.Empty)
            return Task.FromResult<PayfastMerchantSettings?>(null);

        // Guid N is stable, ASCII-only and cannot manipulate configuration paths.
        var section = configuration.GetSection(
            $"Payments:PayfastSandbox:Merchants:{tenantId:N}");

        if (!string.Equals(section["Enabled"], "true",
                StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<PayfastMerchantSettings?>(null);

        var merchantId = section["MerchantId"];
        var passphrase = section["Passphrase"];
        if (merchantId is null || merchantId.Length != 8 ||
            !merchantId.All(char.IsAsciiDigit) ||
            string.IsNullOrWhiteSpace(passphrase) ||
            passphrase.Length > 100)
            return Task.FromResult<PayfastMerchantSettings?>(null);

        // Preserve the configured passphrase exactly; it must match the
        // passphrase that Payfast uses for payment form / ITN signing.
        return Task.FromResult<PayfastMerchantSettings?>(
            new PayfastMerchantSettings(merchantId, passphrase));
    }
}
