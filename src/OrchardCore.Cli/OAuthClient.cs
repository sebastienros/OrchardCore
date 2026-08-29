using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace OrchardCore.Cli;

internal sealed class OAuthClient
{
    private readonly HttpClient _httpClient;
    private readonly TextWriter _stderr;

    public OAuthClient(HttpClient httpClient, TextWriter stderr)
    {
        _httpClient = httpClient;
        _stderr = stderr;
    }

    public async Task<OidcDiscoveryDocument> GetDiscoveryAsync(Uri authority, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authority);

        var discoveryUri = new Uri($"{authority.AbsoluteUri.TrimEnd('/')}/.well-known/openid-configuration");
        using var response = await _httpClient.GetAsync(discoveryUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var discovery = CliUtilities.ParseDiscoveryDocument(content);
        CliUtilities.EnsureIssuerMatches(authority.AbsoluteUri, discovery.Issuer);
        return discovery;
    }

    public async Task<StoredToken> LoginWithAuthorizationCodeAsync(TenantContextRecord context, OidcDiscoveryDocument discovery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discovery);

        var clientId = context.ClientId ?? throw new CliException("The selected context does not declare a CLI client identifier.");
        var authorizationEndpoint = discovery.AuthorizationEndpoint ??
            throw new CliException("The identity provider does not expose an authorization endpoint.");
        var scope = string.Join(' ', context.Scopes.Count > 0 ? context.Scopes : ["openid", "profile", "offline_access"]);
        var state = CreateRandomString();
        var codeVerifier = CreateRandomString();
        var challenge = CliUtilities.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier)));

        await using var listener = await LoopbackCallbackListener.StartAsync(cancellationToken);
        var authorizationUri = BuildAuthorizationUri(authorizationEndpoint, clientId, listener.RedirectUri, scope, state, challenge);
        LaunchBrowser(authorizationUri);
        await _stderr.WriteLineAsync($"Opening browser for '{context.Name}' login...");

        var (code, returnedState) = await listener.WaitForCallbackAsync(cancellationToken);
        if (!string.Equals(returnedState, state, StringComparison.Ordinal))
        {
            throw new CliException("OAuth state validation failed.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, discovery.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["redirect_uri"] = listener.RedirectUri,
                ["code_verifier"] = codeVerifier,
            }),
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        var tokenResponse = CliUtilities.ParseTokenResponse(responseContent);
        return CliUtilities.CreateStoredToken(tokenResponse, discovery);
    }

    public async Task<StoredToken> LoginWithDeviceCodeAsync(TenantContextRecord context, OidcDiscoveryDocument discovery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discovery);

        if (string.IsNullOrWhiteSpace(discovery.DeviceAuthorizationEndpoint))
        {
            throw new CliException("The identity provider does not expose a device authorization endpoint.");
        }

        var clientId = context.ClientId ?? throw new CliException("The selected context does not declare a CLI client identifier.");
        var scope = string.Join(' ', context.Scopes.Count > 0 ? context.Scopes : ["openid", "profile", "offline_access"]);

        using var deviceRequest = new HttpRequestMessage(HttpMethod.Post, discovery.DeviceAuthorizationEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["scope"] = scope,
            }),
        };

        using var deviceResponse = await _httpClient.SendAsync(deviceRequest, cancellationToken);
        var deviceContent = await deviceResponse.Content.ReadAsStringAsync(cancellationToken);
        deviceResponse.EnsureSuccessStatusCode();

        var device = CliUtilities.ParseDeviceAuthorizationResponse(deviceContent);
        await _stderr.WriteLineAsync($"Open {device.VerificationUriComplete ?? device.VerificationUri} and enter code {device.UserCode}.");

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(device.ExpiresIn);
        var interval = TimeSpan.FromSeconds(Math.Max(1, device.Interval));

        while (DateTimeOffset.UtcNow < expiresAt)
        {
            await Task.Delay(interval, cancellationToken);

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, discovery.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["client_id"] = clientId,
                    ["device_code"] = device.DeviceCode,
                }),
            };

            using var tokenResponse = await _httpClient.SendAsync(tokenRequest, cancellationToken);
            var tokenContent = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            var parsed = CliUtilities.ParseTokenResponse(tokenContent);

            if (tokenResponse.IsSuccessStatusCode)
            {
                return CliUtilities.CreateStoredToken(parsed, discovery);
            }

            switch (parsed.Error)
            {
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval += TimeSpan.FromSeconds(5);
                    continue;
                case "access_denied":
                    throw new CliException("The device authorization request was denied.");
                case "expired_token":
                    throw new CliException("The device authorization code expired before sign-in completed.");
                default:
                    throw new CliException(parsed.ErrorDescription ?? parsed.Error ?? "The device authorization flow failed.");
            }
        }

        throw new CliException("The device authorization code expired before sign-in completed.");
    }

    public async Task<StoredToken> RefreshAsync(TenantContextRecord context, OidcDiscoveryDocument discovery, StoredToken storedToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(storedToken);

        if (string.IsNullOrWhiteSpace(storedToken.RefreshToken))
        {
            throw new CliException("The stored login does not contain a refresh token. Run 'oc login' again.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, discovery.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = context.ClientId ?? throw new CliException("The selected context does not declare a CLI client identifier."),
                ["refresh_token"] = storedToken.RefreshToken,
            }),
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        var tokenResponse = CliUtilities.ParseTokenResponse(content);
        var refreshed = CliUtilities.CreateStoredToken(tokenResponse, discovery);
        refreshed.RefreshToken ??= storedToken.RefreshToken;
        return refreshed;
    }

    public async Task<StoredToken> ClientCredentialsAsync(string tokenEndpoint, string clientId, string clientSecret, string scope, string issuer, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenEndpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = scope,
            }),
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        var tokenResponse = CliUtilities.ParseTokenResponse(content);
        return new StoredToken
        {
            AccessToken = tokenResponse.AccessToken ?? throw new CliException("The token response did not contain an access token."),
            TokenType = tokenResponse.TokenType,
            Scope = tokenResponse.Scope,
            Issuer = issuer,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn > 0 ? tokenResponse.ExpiresIn : 3600),
        };
    }

    public async Task RevokeAsync(
        string revocationEndpoint,
        string clientId,
        string token,
        string tokenTypeHint,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, revocationEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["token"] = token,
                ["token_type_hint"] = tokenTypeHint,
            }),
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string BuildAuthorizationUri(string endpoint, string clientId, string redirectUri, string scope, string state, string codeChallenge)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = clientId;
        query["response_type"] = "code";
        query["redirect_uri"] = redirectUri;
        query["scope"] = scope;
        query["state"] = state;
        query["code_challenge"] = codeChallenge;
        query["code_challenge_method"] = "S256";

        return $"{endpoint}?{query}";
    }

    private static string CreateRandomString()
    {
        var buffer = RandomNumberGenerator.GetBytes(32);
        return CliUtilities.Base64UrlEncode(buffer);
    }

    private static void LaunchBrowser(string url)
    {
        ProcessStartInfo startInfo;

        if (OperatingSystem.IsMacOS())
        {
            startInfo = new ProcessStartInfo("/usr/bin/open", url)
            {
                UseShellExecute = false,
            };
        }
        else if (OperatingSystem.IsWindows())
        {
            startInfo = new ProcessStartInfo(url)
            {
                UseShellExecute = true,
            };
        }
        else
        {
            startInfo = new ProcessStartInfo("xdg-open", url)
            {
                UseShellExecute = false,
            };
        }

        _ = Process.Start(startInfo) ?? throw new CliException("Failed to launch the system browser.");
    }

    private sealed class LoopbackCallbackListener : IAsyncDisposable
    {
        private readonly TcpListener _listener;

        private LoopbackCallbackListener(TcpListener listener)
        {
            _listener = listener;
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            RedirectUri = $"http://127.0.0.1:{endpoint.Port}/callback";
        }

        public string RedirectUri { get; }

        public static Task<LoopbackCallbackListener> StartAsync(CancellationToken cancellationToken)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(1);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new LoopbackCallbackListener(listener));
        }

        public async Task<(string Code, string State)> WaitForCallbackAsync(CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));

            using var client = await _listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, leaveOpen: true);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
            {
                NewLine = "\r\n",
                AutoFlush = true,
            };

            var requestLine = await reader.ReadLineAsync(timeout.Token) ?? throw new CliException("The browser callback was empty.");
            var requestTarget = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1)
                ?? throw new CliException("The browser callback was malformed.");

            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(timeout.Token)))
            {
            }

            var targetUri = new Uri($"http://127.0.0.1{requestTarget}", UriKind.Absolute);
            var query = HttpUtility.ParseQueryString(targetUri.Query);
            var code = query["code"] ?? throw new CliException("The browser callback did not include an authorization code.");
            var state = query["state"] ?? throw new CliException("The browser callback did not include state.");

            await writer.WriteAsync("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nConnection: close\r\n\r\n<html><body><p>Login complete. You can close this window.</p></body></html>");
            return (code, state);
        }

        public ValueTask DisposeAsync()
        {
            _listener.Stop();
            return ValueTask.CompletedTask;
        }
    }
}
