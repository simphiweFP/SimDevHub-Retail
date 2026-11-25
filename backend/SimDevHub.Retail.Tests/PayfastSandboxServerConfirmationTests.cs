using System.Net;
using System.Text;
using SimDevHub.Retail.Infrastructure;
using Xunit;

namespace SimDevHub.Retail.Tests;

public sealed class PayfastSandboxServerConfirmationTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, "VALID", true)]
    [InlineData(HttpStatusCode.OK, "INVALID", false)]
    [InlineData(HttpStatusCode.BadRequest, "VALID", false)]
    [InlineData(HttpStatusCode.OK, "VALID-but-not-really", false)]
    public async Task OnlyExactSuccessfulSandboxResponsePasses(
        HttpStatusCode status, string body, bool expected)
    {
        var handler = new CapturingHandler(status, body);
        using var client = new HttpClient(handler);
        var confirmation = new PayfastSandboxServerConfirmation(client);
        var payload = Encoding.UTF8.GetBytes("m_payment_id=example&payment_status=COMPLETE");
        var ok = await confirmation.ValidateAsync(payload, CancellationToken.None);

        Assert.Equal(expected, ok);
        Assert.Equal("https://sandbox.payfast.co.za/eng/query/validate", handler.LastUri?.AbsoluteUri);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("application/x-www-form-urlencoded", handler.LastContentType);
        Assert.Equal(Encoding.UTF8.GetString(payload), handler.LastBody);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task EmptyOrOversizedNotificationsNeverReachNetwork()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, "VALID");
        using var client = new HttpClient(handler);
        var confirmation = new PayfastSandboxServerConfirmation(client);
        Assert.False(await confirmation.ValidateAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None));
        Assert.False(await confirmation.ValidateAsync(new byte[32 * 1024 + 1], CancellationToken.None));
        Assert.Equal(0, handler.Calls);
    }

    private sealed class CapturingHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Uri? LastUri { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastContentType { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUri = request.RequestUri;
            LastMethod = request.Method;
            LastContentType = request.Content?.Headers.ContentType?.MediaType;
            LastBody = request.Content is null
                ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody)
            };
        }
    }
}
