using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Generates the Payfast sandbox hosted-payment POST fields only.
/// This does not verify payments, change intent status or charge customers.
/// The merchant credentials must be retrieved from secure configuration in a
/// future authenticated, tenant-scoped checkout workflow.
/// </summary>
public static class PayfastSandboxCheckoutBuilder
{
    public static readonly Uri SandboxUrl = new("https://sandbox.payfast.co.za/eng/process");

    public sealed record Credentials(string MerchantId, string MerchantKey, string Passphrase);

    public sealed record Request(
        string MerchantPaymentReference,
        decimal Amount,
        string ItemName,
        Uri ReturnUrl,
        Uri CancelUrl,
        Uri NotifyUrl);

    public sealed record Form(Uri Action, IReadOnlyDictionary<string, string> Fields);

    /// <summary>
    /// Payfast's payment-form signature uses the documented insertion order,
    /// application/x-www-form-urlencoded encoding and lowercase MD5 digest.
    /// MD5 here is a Payfast protocol requirement, not a generic password hash.
    /// </summary>
    public static Form Build(Credentials credentials, Request request)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(credentials.MerchantId) ||
            credentials.MerchantId.Length != 8 || !credentials.MerchantId.All(char.IsAsciiDigit) ||
            string.IsNullOrWhiteSpace(credentials.MerchantKey) ||
            credentials.MerchantKey.Length > 80 ||
            string.IsNullOrWhiteSpace(credentials.Passphrase) ||
            credentials.Passphrase.Length > 100)
            throw new ArgumentException("Configured sandbox merchant details are required.");

        if (request.Amount <= 0 || request.Amount > 100000000m ||
            request.Amount != decimal.Round(request.Amount, 2))
            throw new ArgumentOutOfRangeException(nameof(request), "A positive two-decimal ZAR amount is required.");
        if (string.IsNullOrWhiteSpace(request.MerchantPaymentReference) ||
            request.MerchantPaymentReference.Length > 64 ||
            !request.MerchantPaymentReference.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ||
            string.IsNullOrWhiteSpace(request.ItemName) || request.ItemName.Length > 100)
            throw new ArgumentException("Invalid checkout reference or item description.");

        RequireHttps(request.ReturnUrl);
        RequireHttps(request.CancelUrl);
        RequireHttps(request.NotifyUrl);
        if (request.NotifyUrl.IsLoopback)
            throw new ArgumentException("Payfast sandbox needs an externally reachable HTTPS ITN URL.");

        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["merchant_id"] = credentials.MerchantId,
            ["merchant_key"] = credentials.MerchantKey,
            ["return_url"] = request.ReturnUrl.AbsoluteUri,
            ["cancel_url"] = request.CancelUrl.AbsoluteUri,
            ["notify_url"] = request.NotifyUrl.AbsoluteUri,
            ["m_payment_id"] = request.MerchantPaymentReference,
            ["amount"] = request.Amount.ToString("F2", CultureInfo.InvariantCulture),
            ["item_name"] = request.ItemName.Trim()
        };

        // Only nonempty fields in their form-insertion order enter the digest.
        // The passphrase MUST NOT be included in submitted form fields.
        var signable = fields.Select(pair => pair.Key + "=" + WebUtility.UrlEncode(pair.Value))
            .Append("passphrase=" + WebUtility.UrlEncode(credentials.Passphrase.Trim()));
        var digest = MD5.HashData(Encoding.UTF8.GetBytes(string.Join("&", signable)));
        fields.Add("signature", Convert.ToHexString(digest).ToLowerInvariant());
        return new Form(SandboxUrl, fields);
    }

    private static void RequireHttps(Uri url)
    {
        if (url is null || !url.IsAbsoluteUri ||
            !string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(url.UserInfo) || url.Fragment.Length > 0)
            throw new ArgumentException("Return, cancel and notification URLs must be absolute HTTPS URLs.");
    }
}
