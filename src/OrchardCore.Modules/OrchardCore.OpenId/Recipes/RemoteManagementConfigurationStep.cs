using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace OrchardCore.OpenId.Recipes;

/// <summary>
/// Configures the OpenID Connect server, validation, scope, and public client used for remote management.
/// </summary>
public sealed class RemoteManagementConfigurationStep : NamedRecipeStepHandler
{
    private readonly RemoteManagementConfigurationService _configurationService;

    public RemoteManagementConfigurationStep(RemoteManagementConfigurationService configurationService)
        : base("RemoteManagementConfiguration")
    {
        _configurationService = configurationService;
    }

    protected override Task HandleAsync(RecipeExecutionContext context) =>
        _configurationService.ConfigureAsync();
}
