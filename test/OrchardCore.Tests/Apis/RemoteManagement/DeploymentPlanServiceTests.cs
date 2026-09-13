using System.Text.Json.Nodes;
using OrchardCore.Deployment;
using OrchardCore.Deployment.Recipes;
using OrchardCore.Recipes.Models;
using OrchardCore.Deployment.Steps;
using OrchardCore.Environment.Shell;
using OrchardCore.Tests.Apis.Context;

namespace OrchardCore.Tests.Apis.RemoteManagement;

public class DeploymentPlanServiceTests
{
    [Theory]
    [InlineData("{\"Plans\":{}}")]
    [InlineData("{\"Plans\":[null]}")]
    [InlineData("{\"Plans\":[{\"Name\":\" \"}]}")]
    [InlineData("{\"Plans\":[{\"Name\":\"Keep\",\"Steps\":[{\"Type\":\"MissingFactory\",\"Step\":{}}]}]}")]
    [InlineData("{\"Plans\":[{\"Name\":\"Keep\",\"Steps\":[{\"Type\":\"CustomFileDeploymentStep\",\"Step\":{\"FileName\":\"../escape.txt\"}}]}]}")]
    [InlineData("{\"Plans\":[{\"Name\":\"Keep\"},{\"Name\":\"keep\"}]}")]
    [InlineData("{\"Plans\":[{\"Name\":\"Keep\",\"Steps\":[{\"Type\":\"RecipeFileDeploymentStep\",\"Step\":{\"Id\":\"same\"}},{\"Type\":\"RecipeFileDeploymentStep\",\"Step\":{\"Id\":\"SAME\"}}]}]}")]
    public async Task Recipe_InvalidBatch_ReportsErrorsAndPreservesPersistedPlan(string json)
    {
        using var context = await CreateContextAsync();
        await context.UsingTenantScopeAsync(scope =>
            scope.ServiceProvider.GetRequiredService<IDeploymentPlanService>().CreateOrUpdateDeploymentPlansAsync(
                [new DeploymentPlan { Name = "Keep", DeploymentSteps = [new RecipeFileDeploymentStep { Id = "original" }] }]));
        await context.UsingTenantScopeAsync(async scope =>
        {
            var recipe = new RecipeExecutionContext { Name = "deployment", Step = JsonNode.Parse(json).AsObject() };
            await ActivatorUtilities.CreateInstance<DeploymentPlansRecipeStep>(scope.ServiceProvider).ExecuteAsync(recipe);
            Assert.NotEmpty(recipe.Errors);
        });
        await context.UsingTenantScopeAsync(async scope =>
        {
            var plan = Assert.Single(await scope.ServiceProvider.GetRequiredService<IDeploymentPlanService>().GetDeploymentPlansAsync("Keep"));
            Assert.Equal("original", Assert.Single(plan.DeploymentSteps).Id);
        });
    }

    [Fact]
    public async Task Recipe_ValidReplacement_PersistsConfigurationAndCanonicalFactoryName()
    {
        using var context = await CreateContextAsync();
        await context.UsingTenantScopeAsync(async scope =>
        {
            var recipe = new RecipeExecutionContext
            {
                Name = "deployment",
                Step = JsonNode.Parse("""
                    {"Plans":[{"Name":"Recipe plan","Steps":[{"Type":"CustomFileDeploymentStep",
                      "Step":{"Id":"file","Name":"incorrect","FileName":"folder/readme.txt","FileContent":"sample"}}]}]}
                    """).AsObject(),
            };
            await ActivatorUtilities.CreateInstance<DeploymentPlansRecipeStep>(scope.ServiceProvider).ExecuteAsync(recipe);
            Assert.Empty(recipe.Errors);
        });
        await context.UsingTenantScopeAsync(async scope =>
        {
            var plan = Assert.Single(await scope.ServiceProvider.GetRequiredService<IDeploymentPlanService>().GetDeploymentPlansAsync("Recipe plan"));
            var step = Assert.IsType<CustomFileDeploymentStep>(Assert.Single(plan.DeploymentSteps));
            Assert.Equal("file", step.Id);
            Assert.Equal(new CustomFileDeploymentStep().Name, step.Name);
            Assert.Equal("folder/readme.txt", step.FileName);
            Assert.Equal("sample", step.FileContent);
        });
    }

    [Fact]
    public async Task Replace_InvalidLaterPlan_RejectsBatchBeforeChangingExistingPlan()
    {
        using var context = await CreateContextAsync();
        await context.UsingTenantScopeAsync(async scope =>
        {
            var service = scope.ServiceProvider.GetRequiredService<IDeploymentPlanService>();
            await service.CreateOrUpdateDeploymentPlansAsync([new DeploymentPlan
            {
                Name = "Keep", DeploymentSteps = [new RecipeFileDeploymentStep { Id = "original" }],
            }]);
            var existing = Assert.Single(await service.GetDeploymentPlansAsync("Keep"));
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateOrUpdateDeploymentPlansAsync(
            [
                new DeploymentPlan { Name = "Keep", DeploymentSteps = [new RecipeFileDeploymentStep { Id = "replacement" }] },
                new DeploymentPlan { Name = " " },
            ]));
            Assert.Equal("original", Assert.Single(existing.DeploymentSteps).Id);
        });
    }

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
