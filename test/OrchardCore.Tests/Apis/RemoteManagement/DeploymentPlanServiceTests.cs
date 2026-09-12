using OrchardCore.Deployment;
using OrchardCore.Deployment.Steps;
using OrchardCore.Environment.Shell;
using OrchardCore.Tests.Apis.Context;

namespace OrchardCore.Tests.Apis.RemoteManagement;

public class DeploymentPlanServiceTests
{
    [Fact]
    public async Task Create_AfterCachedRead_IsVisibleToExistingExportCallers()
    {
        using var context = await CreateContextAsync();
        await context.UsingTenantScopeAsync(async scope =>
        {
            var service = scope.ServiceProvider.GetRequiredService<IDeploymentPlanService>();
            await service.GetAllDeploymentPlanNamesAsync();
            await service.CreateOrUpdateDeploymentPlansAsync([new DeploymentPlan { Name = "New export" }]);

            Assert.Contains("New export", await service.GetAllDeploymentPlanNamesAsync());
            Assert.Single(await service.GetDeploymentPlansAsync("New export"));
        });
    }

    [Fact]
    public async Task Replace_WithPreviouslyReadPlan_PreservesItsSteps()
    {
        using var context = await CreateContextAsync();
        await context.UsingTenantScopeAsync(scope =>
            scope.ServiceProvider.GetRequiredService<IDeploymentPlanService>()
                .CreateOrUpdateDeploymentPlansAsync([new DeploymentPlan
                {
                    Name = "Existing export",
                    DeploymentSteps = [new RecipeFileDeploymentStep { Id = "recipe-file" }],
                }]));

        await context.UsingTenantScopeAsync(async scope =>
        {
            var service = scope.ServiceProvider.GetRequiredService<IDeploymentPlanService>();
            var plan = Assert.Single(await service.GetDeploymentPlansAsync("Existing export"));
            await service.CreateOrUpdateDeploymentPlansAsync([plan]);
            Assert.Equal("recipe-file", Assert.Single(plan.DeploymentSteps).Id);
        });

        await context.UsingTenantScopeAsync(async scope =>
        {
            var service = scope.ServiceProvider.GetRequiredService<IDeploymentPlanService>();
            var plan = Assert.Single(await service.GetDeploymentPlansAsync("Existing export"));
            Assert.Equal("recipe-file", Assert.Single(plan.DeploymentSteps).Id);
        });
    }

    private static async Task<SiteContext> CreateContextAsync()
    {
        var context = new SiteContext();
        await context.InitializeAsync();
        await context.UsingTenantScopeAsync(async scope =>
        {
            var manager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();
            var feature = (await manager.GetAvailableFeaturesAsync()).Single(feature => feature.Id == "OrchardCore.Deployment");
            await manager.EnableFeaturesAsync([feature], force: true);
        });
        return context;
    }
}
