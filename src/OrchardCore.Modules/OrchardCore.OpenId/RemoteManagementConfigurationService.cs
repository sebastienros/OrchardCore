using System.ComponentModel.DataAnnotations;
using OpenIddict.Abstractions;
using OrchardCore.Environment.Shell;
using OrchardCore.OpenId.Abstractions.Descriptors;
using OrchardCore.OpenId.Abstractions.Managers;
using OrchardCore.OpenId.Services;

namespace OrchardCore.OpenId;

public sealed class RemoteManagementConfigurationService
{
    private const string ClientId = "orchardcore-cli";
    private const string ManagementScope = "orchardcore.management";
    private const string ManagementResource = "orchardcore";

    private static readonly Uri RedirectUri = new("http://127.0.0.1/callback");
    private static readonly Uri PostLogoutRedirectUri = new("http://127.0.0.1/");

    private static readonly string[] RequiredPermissions =
    [
        OpenIddictConstants.Permissions.Endpoints.Authorization,
        OpenIddictConstants.Permissions.Endpoints.DeviceAuthorization,
        OpenIddictConstants.Permissions.Endpoints.EndSession,
        OpenIddictConstants.Permissions.Endpoints.Revocation,
        OpenIddictConstants.Permissions.Endpoints.Token,
        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
        OpenIddictConstants.Permissions.GrantTypes.DeviceCode,
        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
        OpenIddictConstants.Permissions.ResponseTypes.Code,
        OpenIddictConstants.Permissions.Prefixes.Scope + "email",
        OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
        OpenIddictConstants.Permissions.Prefixes.Scope + "profile",
        OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
        OpenIddictConstants.Permissions.Prefixes.Scope + ManagementScope,
    ];

    private readonly IOpenIdApplicationManager _applicationManager;
    private readonly IOpenIdScopeManager _scopeManager;
    private readonly IOpenIdServerService _serverService;
    private readonly IOpenIdValidationService _validationService;
    private readonly ShellSettings _shellSettings;

    public RemoteManagementConfigurationService(
        IOpenIdApplicationManager applicationManager,
        IOpenIdScopeManager scopeManager,
        IOpenIdServerService serverService,
        IOpenIdValidationService validationService,
        ShellSettings shellSettings)
    {
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
        _serverService = serverService;
        _validationService = validationService;
        _shellSettings = shellSettings;
    }

    public async Task<RemoteManagementConfigurationStatus> GetStatusAsync()
    {
        var server = await _serverService.GetSettingsAsync();
        var validation = await _validationService.GetSettingsAsync();
        var scope = await _scopeManager.FindByNameAsync(ManagementScope);
        var application = await _applicationManager.FindByClientIdAsync(ClientId);

        var scopeConfigured = false;
        if (scope is not null)
        {
            var resources = await _scopeManager.GetResourcesAsync(scope);
            scopeConfigured = resources.Contains(ManagementResource, StringComparer.Ordinal);
        }

        var applicationConfigured = false;
        if (application is not null)
        {
            var permissions = await _applicationManager.GetPermissionsAsync(application);
            var requirements = await _applicationManager.GetRequirementsAsync(application);
            var redirectUris = await _applicationManager.GetRedirectUrisAsync(application);
            var postLogoutRedirectUris = await _applicationManager.GetPostLogoutRedirectUrisAsync(application);

            applicationConfigured =
                string.Equals(await _applicationManager.GetApplicationTypeAsync(application), OpenIddictConstants.ApplicationTypes.Native, StringComparison.Ordinal) &&
                string.Equals(await _applicationManager.GetClientTypeAsync(application), OpenIddictConstants.ClientTypes.Public, StringComparison.Ordinal) &&
                RequiredPermissions.All(permissions.Contains) &&
                requirements.Contains(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange) &&
                redirectUris.Contains(RedirectUri.AbsoluteUri, StringComparer.Ordinal) &&
                postLogoutRedirectUris.Contains(PostLogoutRedirectUri.AbsoluteUri, StringComparer.Ordinal);
        }

        return new RemoteManagementConfigurationStatus
        {
            ServerEndpointsConfigured =
                server.AuthorizationEndpointPath == "/connect/authorize" &&
                server.TokenEndpointPath == "/connect/token" &&
                server.LogoutEndpointPath == "/connect/logout" &&
                server.DeviceAuthorizationEndpointPath == "/connect/device" &&
                server.EndUserVerificationEndpointPath == "/connect/verify" &&
                server.RevocationEndpointPath == "/connect/revoke",
            AuthorizationCodeFlowConfigured = server.AllowAuthorizationCodeFlow,
            ClientCredentialsFlowConfigured = server.AllowClientCredentialsFlow,
            DeviceFlowConfigured = server.AllowDeviceAuthorizationFlow,
            RefreshFlowConfigured = server.AllowRefreshTokenFlow,
            ProofKeyForCodeExchangeRequired = server.RequireProofKeyForCodeExchange,
            ValidationConfigured =
                string.Equals(validation.Tenant, _shellSettings.Name, StringComparison.Ordinal) &&
                validation.Authority is null,
            ManagementScopeConfigured = scopeConfigured,
            CliApplicationConfigured = applicationConfigured,
        };
    }

    public async Task ConfigureAsync()
    {
        var server = await _serverService.LoadSettingsAsync();
        server.AuthorizationEndpointPath = "/connect/authorize";
        server.TokenEndpointPath = "/connect/token";
        server.LogoutEndpointPath = "/connect/logout";
        server.DeviceAuthorizationEndpointPath = "/connect/device";
        server.EndUserVerificationEndpointPath = "/connect/verify";
        server.RevocationEndpointPath = "/connect/revoke";
        server.AllowAuthorizationCodeFlow = true;
        server.AllowClientCredentialsFlow = true;
        server.AllowDeviceAuthorizationFlow = true;
        server.AllowRefreshTokenFlow = true;
        server.RequireProofKeyForCodeExchange = true;
        var validation = await _validationService.LoadSettingsAsync();
        validation.Tenant = _shellSettings.Name;
        validation.Authority = null;
        validation.MetadataAddress = null;

        await ThrowIfInvalidAsync(await _serverService.ValidateSettingsAsync(server));
        await ThrowIfInvalidAsync(await _validationService.ValidateSettingsAsync(validation));

        await _serverService.UpdateSettingsAsync(server);
        await _validationService.UpdateSettingsAsync(validation);

        await ConfigureScopeAsync();
        await ConfigureApplicationAsync();
    }

    private async Task ConfigureScopeAsync()
    {
        var scope = await _scopeManager.FindByNameAsync(ManagementScope);
        var descriptor = new OpenIdScopeDescriptor();

        if (scope is not null)
        {
            await _scopeManager.PopulateAsync(scope, descriptor);
        }

        descriptor.Name = ManagementScope;
        descriptor.DisplayName = "Orchard Core remote management";
        descriptor.Description = "Allows a client to invoke Orchard Core remote management APIs.";
        descriptor.Resources.Add(ManagementResource);

        if (scope is null)
        {
            await _scopeManager.CreateAsync(descriptor);
        }
        else
        {
            await _scopeManager.UpdateAsync(scope, descriptor);
        }
    }

    private async Task ConfigureApplicationAsync()
    {
        var application = await _applicationManager.FindByClientIdAsync(ClientId);
        var descriptor = new OpenIdApplicationDescriptor();

        if (application is not null)
        {
            await _applicationManager.PopulateAsync(descriptor, application);
        }

        descriptor.ClientId = ClientId;
        descriptor.DisplayName = "Orchard Core CLI";
        descriptor.ApplicationType = OpenIddictConstants.ApplicationTypes.Native;
        descriptor.ClientType = OpenIddictConstants.ClientTypes.Public;
        descriptor.ConsentType = OpenIddictConstants.ConsentTypes.Explicit;
        descriptor.ClientSecret = null;
        descriptor.RedirectUris.Add(RedirectUri);
        descriptor.PostLogoutRedirectUris.Add(PostLogoutRedirectUri);
        descriptor.Permissions.UnionWith(RequiredPermissions);
        descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

        if (application is null)
        {
            await _applicationManager.CreateAsync(descriptor);
        }
        else
        {
            await _applicationManager.UpdateAsync(application, descriptor);
        }
    }

    private static Task ThrowIfInvalidAsync(IEnumerable<ValidationResult> results)
    {
        var errors = results
            .Where(result => result != ValidationResult.Success)
            .Select(result => result.ErrorMessage)
            .ToArray();

        return errors.Length == 0
            ? Task.CompletedTask
            : Task.FromException(new InvalidOperationException(string.Join(" ", errors)));
    }
}
