using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Sandbox Payfast ITN verifier. NEVER register until the source validator,
/// merchant settings and server confirmation implementation have been reviewed.
/// No callback route or payment state transition is created by this class.
/// </summary>
public sealed class PayfastSandboxItnVerifier(
    RetailDbContext db,
    IPayfastSourceValidator sourceValidator,
    IPayfastServerConfirmation serverConfirmation,
    IPayfastSandboxMerchant merchant) : IPaymentGatewayVerifier
{
    public string Provider => "PayfastSandbox";

    public async Task<VerifiedPaymentEvent?> VerifyAsync(
        GatewayNotification notification, CancellationToken ct)
    {
        if (notification.Provider != Provider || notification.Body.IsEmpty ||
            notification.Body.Length > 32 * 1024)
            return null;

        // Raw ITN must be form-encoded; duplicate keys or malformed fields fail closed.
        var text = Encoding.UTF8.GetString(notification.Body.Span);
        var values = ParseForm(text);
        if (values is null || !values.TryGetValue("signature", out var submitted) ||
            submitted.Length != 32 || !submitted.All(Uri.IsHexDigit))
            return null;

        if (!values.TryGetValue("m_payment_id", out var paymentId) ||
            !Guid.TryParseExact(paymentId, "N", out var intentId) ||
            !values.TryGetValue("merchant_id", out var merchantId) ||
            !values.TryGetValue("payment_status", out var status) || status != "COMPLETE" ||
            !values.TryGetValue("pf_payment_id", out var providerId) ||
            providerId.Length is < 1 or > 160 ||
            !values.TryGetValue("amount_gross", out var gross) ||
            !decimal.TryParse(gross, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0 || amount != decimal.Round(amount, 2))
            return null;

        // The untrusted intent reference only selects a candidate tenant. It
        // does not prove authority. Every credential and amount check below is
        // against that persisted tenant/intent rather than a global merchant.
        var intent = await db.PaymentIntents.AsNoTracking()
            .Where(x => x.Id == intentId && x.Provider == Provider &&
                        x.Status == "AwaitingVerification" && x.Currency == "ZAR" &&
                        x.Order.TenantId == x.TenantId && x.Order.Tenant.IsActive)
            .Select(x => new { x.Id, x.TenantId, x.OrderId, x.Amount })
            .SingleOrDefaultAsync(ct);
        if (intent is null || intent.Amount != amount)
            return null;

        var configuration = await merchant.GetCredentialsAsync(intent.TenantId, ct);
        if (configuration is null ||
            !string.Equals(merchantId, configuration.MerchantId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(configuration.Passphrase))
            return null;

        // Build signature in original POST field order. The provider's signature
        // is calculated from all nonempty fields other than the signature itself.
        var fields = text.Split('&', StringSplitOptions.None)
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0] != "signature" && parts[1].Length > 0)
            .Select(parts => WebUtility.UrlDecode(parts[0]) + "=" +
                             WebUtility.UrlEncode(WebUtility.UrlDecode(parts[1])));
        var payload = string.Join("&", fields) + "&passphrase=" +
                      WebUtility.UrlEncode(configuration.Passphrase.Trim());
        var expected = MD5.HashData(Encoding.UTF8.GetBytes(payload));
        var supplied = Convert.FromHexString(submitted);
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
            return null;

        // Source and server-validation checks are both mandatory. They are
        // separate contracts to prevent signature-only acceptance.
        if (!await sourceValidator.IsTrustedSourceAsync(notification, ct) ||
            !await serverConfirmation.ValidateAsync(notification.Body, ct))
            return null;

        return new VerifiedPaymentEvent(
            intent.TenantId, intent.OrderId, intent.Id, Provider, providerId, amount, "ZAR");
    }

    private static Dictionary<string, string>? ParseForm(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in text.Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2 || parts[0].Length == 0) return null;
            var key = WebUtility.UrlDecode(parts[0]);
            var value = WebUtility.UrlDecode(parts[1]);
            if (key.Length > 80 || value.Length > 2048 || !result.TryAdd(key, value))
                return null;
        }
        return result;
    }
}

public sealed record PayfastMerchantSettings(string MerchantId, string Passphrase);

public interface IPayfastSandboxMerchant
{
    Task<PayfastMerchantSettings?> GetCredentialsAsync(Guid tenantId, CancellationToken ct);
}

/// <summary>
/// The implementation must establish the remote source from the HTTP connection,
/// never spoofable user-supplied Forwarded/X-Forwarded-For headers.
/// </summary>
public interface IPayfastSourceValidator
{
    Task<bool> IsTrustedSourceAsync(GatewayNotification notification, CancellationToken ct);
}

/// <summary>Server-to-server response from Payfast's official sandbox validation endpoint.</summary>
public interface IPayfastServerConfirmation
{
    Task<bool> ValidateAsync(ReadOnlyMemory<byte> formBody, CancellationToken ct);
}
