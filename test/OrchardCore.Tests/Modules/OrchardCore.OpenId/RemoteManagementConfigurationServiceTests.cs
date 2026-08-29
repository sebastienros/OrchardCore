using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using OrchardCore.Environment.Shell;
using OrchardCore.OpenId;
using OrchardCore.OpenId.Abstractions.Descriptors;
using OrchardCore.OpenId.Abstractions.Managers;
using OrchardCore.OpenId.Services;
using OrchardCore.OpenId.Settings;

namespace OrchardCore.Tests.Modules.OrchardCore.OpenId;

public class RemoteManagementConfigurationServiceTests
{
    [Fact]
    public async Task GetStatusAsync_MissingConfiguration_ReturnsNotReady()
    {
        var service = CreateService();

        var status = await service.GetStatusAsync();

        Assert.False(status.IsReady);
        Assert.False(status.ManagementScopeConfigured);
        Assert.False(status.CliApplicationConfigured);
    }

    [Fact]
    public async Task ConfigureAsync_MissingConfiguration_CreatesRequiredConfiguration()
    {
        var serverSettings = new OpenIdServerSettings
        {
            UserinfoEndpointPath = "/custom/userinfo",
            AllowPasswordFlow = true,
        };
        var validationSettings = new OpenIdValidationSettings
        {
            Authority = new Uri("https://identity.example.com"),
            MetadataAddress = new Uri("https://identity.example.com/metadata"),
            Audience = "preserved-audience",
        };
        OpenIdScopeDescriptor createdScope = null;
        OpenIdApplicationDescriptor createdApplication = null;

        var serverService = new Mock<IOpenIdServerService>();
        serverService.Setup(service => service.LoadSettingsAsync()).ReturnsAsync(serverSettings);
        serverService.Setup(service => service.ValidateSettingsAsync(serverSettings))
            .ReturnsAsync(ImmutableArray<ValidationResult>.Empty);

        var validationService = new Mock<IOpenIdValidationService>();
        validationService.Setup(service => service.LoadSettingsAsync()).ReturnsAsync(validationSettings);
        validationService.Setup(service => service.ValidateSettingsAsync(validationSettings))
            .ReturnsAsync(ImmutableArray<ValidationResult>.Empty);

        var scopeManager = new Mock<IOpenIdScopeManager>();
        scopeManager
            .Setup(manager => manager.CreateAsync(It.IsAny<OpenIdScopeDescriptor>(), It.IsAny<CancellationToken>()))
            .Callback<OpenIddictScopeDescriptor, CancellationToken>((descriptor, _) => createdScope = (OpenIdScopeDescriptor)descriptor);

        var applicationManager = new Mock<IOpenIdApplicationManager>();
        applicationManager
            .Setup(manager => manager.CreateAsync(It.IsAny<OpenIdApplicationDescriptor>(), It.IsAny<CancellationToken>()))
            .Callback<OpenIddictApplicationDescriptor, CancellationToken>((descriptor, _) => createdApplication = (OpenIdApplicationDescriptor)descriptor);

        var service = CreateService(
            applicationManager,
            scopeManager,
            serverService,
            validationService);

        await service.ConfigureAsync();

        Assert.Equal("/custom/userinfo", serverSettings.UserinfoEndpointPath);
        Assert.True(serverSettings.AllowPasswordFlow);
        Assert.True(serverSettings.AllowAuthorizationCodeFlow);
        Assert.True(serverSettings.AllowClientCredentialsFlow);
        Assert.True(serverSettings.AllowDeviceAuthorizationFlow);
        Assert.True(serverSettings.AllowRefreshTokenFlow);
        Assert.True(serverSettings.RequireProofKeyForCodeExchange);
        Assert.Equal("TestTenant", validationSettings.Tenant);
        Assert.Null(validationSettings.Authority);
        Assert.Null(validationSettings.MetadataAddress);
        Assert.Equal("preserved-audience", validationSettings.Audience);
        Assert.Contains("orchardcore", createdScope.Resources);
        Assert.Equal("orchardcore-cli", createdApplication.ClientId);
        Assert.Contains(new Uri("http://127.0.0.1/callback"), createdApplication.RedirectUris);
        Assert.Contains(
            OpenIddictConstants.Permissions.Prefixes.Scope + "orchardcore.management",
            createdApplication.Permissions);
    }

    [Fact]
    public async Task ConfigureAsync_ExistingConfiguration_PreservesUnrelatedValues()
    {
        var existingScope = new object();
        var existingApplication = new object();
        OpenIdScopeDescriptor updatedScope = null;
        OpenIdApplicationDescriptor updatedApplication = null;

        var serverService = new Mock<IOpenIdServerService>();
        serverService.Setup(service => service.LoadSettingsAsync()).ReturnsAsync(new OpenIdServerSettings());
        serverService.Setup(service => service.ValidateSettingsAsync(It.IsAny<OpenIdServerSettings>()))
            .ReturnsAsync(ImmutableArray<ValidationResult>.Empty);

        var validationService = new Mock<IOpenIdValidationService>();
        validationService.Setup(service => service.LoadSettingsAsync()).ReturnsAsync(new OpenIdValidationSettings());
        validationService.Setup(service => service.ValidateSettingsAsync(It.IsAny<OpenIdValidationSettings>()))
            .ReturnsAsync(ImmutableArray<ValidationResult>.Empty);

        var scopeManager = new Mock<IOpenIdScopeManager>();
        scopeManager.Setup(manager => manager.FindByNameAsync("orchardcore.management", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingScope);
        scopeManager
            .Setup(manager => manager.PopulateAsync(existingScope, It.IsAny<OpenIdScopeDescriptor>(), It.IsAny<CancellationToken>()))
            .Callback<object, OpenIddictScopeDescriptor, CancellationToken>((_, descriptor, _) => descriptor.Resources.Add("other-resource"));
        scopeManager
            .Setup(manager => manager.UpdateAsync(existingScope, It.IsAny<OpenIdScopeDescriptor>(), It.IsAny<CancellationToken>()))
            .Callback<object, OpenIddictScopeDescriptor, CancellationToken>((_, descriptor, _) => updatedScope = (OpenIdScopeDescriptor)descriptor);

        var applicationManager = new Mock<IOpenIdApplicationManager>();
        applicationManager.Setup(manager => manager.FindByClientIdAsync("orchardcore-cli", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingApplication);
        applicationManager
            .Setup(manager => manager.PopulateAsync(It.IsAny<OpenIdApplicationDescriptor>(), existingApplication, It.IsAny<CancellationToken>()))
            .Callback<OpenIddictApplicationDescriptor, object, CancellationToken>((descriptor, _, _) =>
            {
                descriptor.RedirectUris.Add(new Uri("https://example.com/existing-callback"));
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "existing-scope");
                ((OpenIdApplicationDescriptor)descriptor).Roles.Add("ExistingRole");
            });
        applicationManager
            .Setup(manager => manager.UpdateAsync(existingApplication, It.IsAny<OpenIdApplicationDescriptor>(), It.IsAny<CancellationToken>()))
            .Callback<object, OpenIddictApplicationDescriptor, CancellationToken>((_, descriptor, _) => updatedApplication = (OpenIdApplicationDescriptor)descriptor);

        var service = CreateService(
            applicationManager,
            scopeManager,
            serverService,
            validationService);

        await service.ConfigureAsync();

        Assert.Contains("other-resource", updatedScope.Resources);
        Assert.Contains("orchardcore", updatedScope.Resources);
        Assert.Contains(new Uri("https://example.com/existing-callback"), updatedApplication.RedirectUris);
        Assert.Contains(OpenIddictConstants.Permissions.Prefixes.Scope + "existing-scope", updatedApplication.Permissions);
        Assert.Contains("ExistingRole", updatedApplication.Roles);
    }

    private static RemoteManagementConfigurationService CreateService(
        Mock<IOpenIdApplicationManager> applicationManager = null,
        Mock<IOpenIdScopeManager> scopeManager = null,
        Mock<IOpenIdServerService> serverService = null,
        Mock<IOpenIdValidationService> validationService = null)
    {
        applicationManager ??= new Mock<IOpenIdApplicationManager>();
        scopeManager ??= new Mock<IOpenIdScopeManager>();
        serverService ??= new Mock<IOpenIdServerService>();
        validationService ??= new Mock<IOpenIdValidationService>();

        serverService
            .Setup(service => service.GetSettingsAsync())
            .ReturnsAsync(new OpenIdServerSettings());
        validationService
            .Setup(service => service.GetSettingsAsync())
            .ReturnsAsync(new OpenIdValidationSettings());

        return new RemoteManagementConfigurationService(
            applicationManager.Object,
            scopeManager.Object,
            serverService.Object,
            validationService.Object,
            new ShellSettings { Name = "TestTenant" });
    }
}
