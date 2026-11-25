using System.Net.Http.Headers;
using System.Text;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Fixed-host Payfast sandbox confirmation. This never accepts a caller-supplied
/// URL and it verifies the TLS certificate through the default HTTP handler.
/// It does not replace source validation or webhook authentication.
/// </summary>
public sealed class PayfastSandboxServerConfirmation(HttpClient client)
    : IPayfastServerConfirmation
{
    private static readonly Uri ValidationUri =
        new("https://sandbox.payfast.co.za/eng/query/validate");

    public async Task<bool> ValidateAsync(ReadOnlyMemory<byte> formBody, CancellationToken ct)
    {
        if (formBody.IsEmpty || formBody.Length > 32 * 1024) return false;
        using var request = new HttpRequestMessage(HttpMethod.Post, ValidationUri);
        request.Content = new ByteArrayContent(formBody.ToArray());
        request.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) return false;
        var confirmation = await response.Content.ReadAsStringAsync(ct);
        // Payfast's validation response is a short VALID/INVALID token.
        return confirmation.Trim() == "VALID";
    }
}
