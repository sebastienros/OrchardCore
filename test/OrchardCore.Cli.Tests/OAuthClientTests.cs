using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace OrchardCore.Cli.Tests;

public class OAuthClientTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 79)]
    [InlineData(true, 0)]
    [InlineData(true, 79)]
    public async Task DeviceLogin_QrUsesVerificationUrl_AndKeepsText(bool completeUrl, int qrWidth)
    {
        const string baseUrl = "https://example.test/tenant/connect/verify";
        var expectedUrl = completeUrl ? baseUrl + "?user_code=1234-5678" : baseUrl;
        var requests = 0;
        using var http = new HttpClient(new TestHandler(_ =>
        {
            requests++;
            var content = requests == 1
                ? "{\"device_code\":\"private-device-code\",\"user_code\":\"1234-5678\",\"verification_uri\":\"" + baseUrl +
                  "\",\"interval\":1,\"expires_in\":60" + (completeUrl ? ",\"verification_uri_complete\":\"" + expectedUrl + "\"" : "") + "}"
                : "{\"access_token\":\"test-access\",\"expires_in\":3600}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) });
        }));
        using var output = new StringWriter();
        var oauth = new OAuthClient(http, output);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var token = await oauth.LoginWithDeviceCodeAsync(new TenantContextRecord { ClientId = "orchardcore-cli" },
            new OidcDiscoveryDocument
            {
                Issuer = "https://example.test/tenant/",
                DeviceAuthorizationEndpoint = "https://example.test/tenant/connect/device",
                TokenEndpoint = "https://example.test/tenant/connect/token",
            }, timeout.Token, qrWidth);

        Assert.Equal("test-access", token.AccessToken);
        var text = output.ToString();
        Assert.Contains($"Open {expectedUrl} and enter code 1234-5678.", text);
        Assert.DoesNotContain("private-device-code", text);
        if (qrWidth == 0)
        {
            Assert.DoesNotContain('\u001b', text);
        }
        else
        {
            Assert.Equal(expectedUrl, TerminalQrCodeTests.Decode(text[text.IndexOf('\u001b')..]));
        }
    }

    [Theory]
    [InlineData("https://foreign.test/connect/verify")]
    [InlineData("http://example.test/connect/verify")]
    public async Task DeviceLogin_UntrustedVerificationUrl_IsNotDisplayed(string verificationUrl)
    {
        using var http = new HttpClient(new TestHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"device_code\":\"secret\",\"user_code\":\"1234\",\"verification_uri\":\"" + verificationUrl + "\"}"),
        })));
        using var output = new StringWriter();
        var oauth = new OAuthClient(http, output);

        await Assert.ThrowsAsync<CliException>(() => oauth.LoginWithDeviceCodeAsync(
            new TenantContextRecord { ClientId = "orchardcore-cli" },
            new OidcDiscoveryDocument
            {
                Issuer = "https://example.test/",
                DeviceAuthorizationEndpoint = "https://example.test/connect/device",
            }, CancellationToken.None, 79));
        Assert.Empty(output.ToString());
    }

    [Theory]
    [InlineData("success")]
    [InlineData("state")]
    [InlineData("issuer")]
    [InlineData("denied")]
    [InlineData("path")]
    [InlineData("oversized")]
    public async Task BrowserLogin_Callback_ValidatesBeforeTokenExchange(string scenario)
    {
        string? tokenBody = null;
        using var http = new HttpClient(new TestHandler(async request =>
        {
            tokenBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"test-access","expires_in":3600}"""),
            };
        }));
        var launched = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oauth = new OAuthClient(http, TextWriter.Null, url => launched.SetResult(url));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var login = oauth.LoginWithAuthorizationCodeAsync(new TenantContextRecord
        {
            Name = "test",
            TenantUrl = "https://example.test/tenant/",
            ClientId = "orchardcore-cli",
        }, new OidcDiscoveryDocument
        {
            Issuer = "https://example.test/tenant/",
            AuthorizationEndpoint = "https://example.test/tenant/connect/authorize",
            TokenEndpoint = "https://example.test/tenant/connect/token",
        }, timeout.Token);
        var parameters = HttpUtility.ParseQueryString(new Uri(await launched.Task.WaitAsync(timeout.Token)).Query);
        var redirect = new Uri(parameters["redirect_uri"]!);
        Assert.Equal("127.0.0.1", redirect.Host);
        Assert.Equal("S256", parameters["code_challenge_method"]);
        var state = scenario == "state" ? "wrong" : parameters["state"];
        var path = scenario == "path" ? "/unexpected" : "/callback";
        var query = scenario == "denied" ? "error=access_denied" : "code=test-code";
        query += "&state=" + state;
        if (scenario == "issuer")
        {
            query += "&iss=https%3A%2F%2Fforeign.test";
        }
        if (scenario == "oversized")
        {
            query += "&padding=" + new string('a', 9000);
        }
        using var socket = new TcpClient();
        await socket.ConnectAsync(IPAddress.Loopback, redirect.Port, timeout.Token);
        await using var stream = socket.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"GET {path}?{query} HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n"), timeout.Token);
        if (scenario != "success")
        {
            await Assert.ThrowsAsync<CliException>(() => login);
            Assert.Null(tokenBody);
            if (scenario == "denied")
            {
                using var deniedReader = new StreamReader(stream);
                var response = await deniedReader.ReadToEndAsync(timeout.Token);
                Assert.Contains("HTTP/1.1 200 OK", response);
                Assert.Contains("Authorization not completed", response);
                Assert.Contains("Cache-Control: no-store", response);
            }
            return;
        }
        var token = await login;
        Assert.Equal("test-access", token.AccessToken);
        var exchange = HttpUtility.ParseQueryString(tokenBody!);
        Assert.Equal("test-code", exchange["code"]);
        Assert.Equal(redirect.AbsoluteUri, exchange["redirect_uri"]);
        Assert.Equal(parameters["code_challenge"], CliUtilities.Base64UrlEncode(
            SHA256.HashData(Encoding.UTF8.GetBytes(exchange["code_verifier"]!))));
        using var reader = new StreamReader(stream);
        Assert.Contains("HTTP/1.1 200 OK", await reader.ReadToEndAsync(timeout.Token));
    }

    [Fact]
    public void ClientCredentialsScope_ExcludesHumanScopes()
    {
        var context = new TenantContextRecord
        {
            Name = "test",
            TenantUrl = "https://example.test/",
            Scopes = ["openid", "profile", "email", "roles", "offline_access", "orchardcore.management"],
        };
        Assert.Equal("orchardcore.management", CliApplication.BuildClientCredentialsScope(context));
    }

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
