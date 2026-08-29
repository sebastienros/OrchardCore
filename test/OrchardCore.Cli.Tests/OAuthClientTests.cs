using System.Net;

namespace OrchardCore.Cli.Tests;

public class OAuthClientTests
{
    [Fact]
    public async Task RevokeAsync_RefreshToken_PostsPublicClientRevocationRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new TestHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = new HttpClient(handler);
        var client = new OAuthClient(httpClient, TextWriter.Null);

        await client.RevokeAsync(
            "https://example.com/connect/revoke",
            "orchardcore-cli",
            "refresh-token",
            "refresh_token",
            CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("https://example.com/connect/revoke", capturedRequest.RequestUri!.AbsoluteUri);
        Assert.Contains("client_id=orchardcore-cli", capturedBody);
        Assert.Contains("token=refresh-token", capturedBody);
        Assert.Contains("token_type_hint=refresh_token", capturedBody);
    }

    [Fact]
    public async Task GetDiscoveryAsync_DeviceOnlyDocument_DoesNotRequireAuthorizationEndpoint()
    {
        Uri? requestedUri = null;
        var handler = new TestHandler(request =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "issuer": "https://example.com/tenant",
                      "token_endpoint": "https://example.com/tenant/connect/token",
                      "device_authorization_endpoint": "https://example.com/tenant/connect/device"
                    }
                    """),
            });
        });
        using var httpClient = new HttpClient(handler);
        var client = new OAuthClient(httpClient, TextWriter.Null);

        var discovery = await client.GetDiscoveryAsync(new Uri("https://example.com/tenant"), CancellationToken.None);

        Assert.Equal("https://example.com/tenant/.well-known/openid-configuration", requestedUri!.AbsoluteUri);
        Assert.Null(discovery.AuthorizationEndpoint);
        Assert.Equal("https://example.com/tenant/connect/token", discovery.TokenEndpoint);
    }

    private sealed class TestHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public TestHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _handler(request);
    }
}
