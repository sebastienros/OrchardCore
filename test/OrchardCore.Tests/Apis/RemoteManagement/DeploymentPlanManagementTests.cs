using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Deployment;
using OrchardCore.Deployment.Controllers;
using OrchardCore.Deployment.Endpoints.Management;
using OrchardCore.Deployment.Steps;
using OrchardCore.Deployment.ViewModels;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell;
using OrchardCore.Navigation;
using OrchardCore.Localization;
using OrchardCore.RemoteManagement;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Tests.Apis.Context;

namespace OrchardCore.Tests.Apis.RemoteManagement;

public class DeploymentPlanManagementTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task AdminAndApi_EditSamePlan_PreserveStepsAndRejectConflicts()
    {
        using var site = await CreateContextAsync();
        long id = 0;
        await site.UsingTenantScopeAsync(async scope =>
        {
            var plans = scope.ServiceProvider.GetRequiredService<IDeploymentPlanService>();
            var admin = Admin(plans);
            Assert.IsType<RedirectToActionResult>(await admin.Create(new CreateDeploymentPlanViewModel { Name = "Alpha" }));
            var plan = await plans.FindByNameAsync("Alpha");
            id = plan.Id;
            plan.DeploymentSteps.Add(new CustomFileDeploymentStep { Id = "file", FileName = "example.txt", FileContent = "private-step-content" });
            plan.DeploymentSteps.Add(new RecipeFileDeploymentStep { Id = "recipe" });
            await plans.CreateOrUpdateDeploymentPlansAsync([plan]);

            var retry = Write(await DeploymentPlanEndpoints.CreateAsync(Http(), Authorized(), plans, new() { Name = "Alpha" }));
            Assert.False(retry.Changed);
            Assert.Equal(id, retry.Plan.Id);
            Assert.Equal(2, retry.Plan.StepCount);
            Assert.DoesNotContain("private-step-content", JsonSerializer.Serialize(retry));

            Assert.True(Write(await DeploymentPlanEndpoints.UpdateAsync(Http(), Authorized(), plans, id, new() { Name = "Beta" })).Changed);
            Assert.False(Write(await DeploymentPlanEndpoints.UpdateAsync(Http(), Authorized(), plans, id, new() { Name = "Beta" })).Changed);
            Assert.IsType<RedirectToActionResult>(await Admin(plans).Edit(new EditDeploymentPlanViewModel { Id = id, Name = "Gamma" }));
            Assert.Equal(new[] { "file", "recipe" }, plan.DeploymentSteps.Select(step => step.Id));
            Assert.Equal("private-step-content", Assert.IsType<CustomFileDeploymentStep>(plan.DeploymentSteps[0]).FileContent);

            await plans.CreateAsync("Taken");
            Assert.Equal(409, Status(await DeploymentPlanEndpoints.UpdateAsync(Http(), Authorized(), plans, id, new() { Name = "Taken" })));
            Assert.Equal("Gamma", plan.Name);
            var rejected = Admin(plans);
            Assert.IsType<ViewResult>(await rejected.Edit(new EditDeploymentPlanViewModel { Id = id, Name = "Taken" }));
            Assert.False(rejected.ModelState.IsValid);
            Assert.Equal("Gamma", plan.Name);
            Assert.Equal(new[] { "file", "recipe" }, plan.DeploymentSteps.Select(step => step.Id));

            var page = Assert.IsType<Ok<DeploymentPlanListResponse>>(await DeploymentPlanEndpoints.ListAsync(Http(), Authorized(), plans,
                new() { Search = "Gamma", Take = 1 })).Value;
            Assert.Equal(1, page.TotalCount);
            Assert.Equal(id, Assert.Single(page.Items).Id);
        });
        await site.UsingTenantScopeAsync(async scope =>
        {
            var plans = scope.ServiceProvider.GetRequiredService<IDeploymentPlanService>();
            var plan = await plans.GetAsync(id);
            Assert.Equal("Gamma", plan.Name);
            Assert.Equal(new[] { "file", "recipe" }, plan.DeploymentSteps.Select(step => step.Id));
            Assert.True(Write(await DeploymentPlanEndpoints.DeleteAsync(Http(), Authorized(), plans, id)).Changed);
            Assert.False(Write(await DeploymentPlanEndpoints.DeleteAsync(Http(), Authorized(), plans, id)).Changed);
            Assert.Equal(404, Status(await DeploymentPlanEndpoints.GetAsync(Http(), Authorized(), plans, id)));
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Endpoints_RequireBothPermissions_BeforeAccessingPlans(bool remoteOnly)
    {
        var auth = Authorize(remoteOnly ? RemoteManagementPermissions.AccessRemoteManagement : DeploymentPermissions.ManageDeploymentPlan);
        var plans = new Mock<IDeploymentPlanService>(MockBehavior.Strict);
        Assert.Equal(403, Status(await DeploymentPlanEndpoints.ListAsync(Http(), auth, plans.Object, new())));
        Assert.Equal(403, Status(await DeploymentPlanEndpoints.GetAsync(Http(), auth, plans.Object, 1)));
        Assert.Equal(403, Status(await DeploymentPlanEndpoints.CreateAsync(Http(), auth, plans.Object, new() { Name = "New" })));
        Assert.Equal(403, Status(await DeploymentPlanEndpoints.UpdateAsync(Http(), auth, plans.Object, 1, new() { Name = "New" })));
        Assert.Equal(403, Status(await DeploymentPlanEndpoints.DeleteAsync(Http(), auth, plans.Object, 1)));
        plans.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvalidRequests_DoNotReachPlanStore()
    {
        var plans = new Mock<IDeploymentPlanService>(MockBehavior.Strict);
        Assert.Equal(400, Status(await DeploymentPlanEndpoints.ListAsync(Http(), Authorized(), plans.Object, new() { Take = 201 })));
        Assert.Equal(400, Status(await DeploymentPlanEndpoints.ListAsync(Http(), Authorized(), plans.Object, new() { Skip = -1 })));
        Assert.Equal(400, Status(await DeploymentPlanEndpoints.GetAsync(Http(), Authorized(), plans.Object, 0)));
        Assert.Equal(400, Status(await DeploymentPlanEndpoints.CreateAsync(Http(), Authorized(), plans.Object, new() { Name = " " })));
        Assert.Equal(400, Status(await DeploymentPlanEndpoints.UpdateAsync(Http(), Authorized(), plans.Object, 1, new() { Name = " " })));
        Assert.Equal(400, Status(await DeploymentPlanEndpoints.DeleteAsync(Http(), Authorized(), plans.Object, -1)));
        plans.VerifyNoOtherCalls();
    }

    private DeploymentPlanController Admin(IDeploymentPlanService plans) => new(
        Authorize(DeploymentPermissions.ManageDeploymentPlan), null, [], plans, Options.Create(new PagerOptions()), null,
        new StringLocalizer<DeploymentPlanController>(new NullStringLocalizerFactory()), Mock.Of<IHtmlLocalizer<DeploymentPlanController>>(),
        Mock.Of<INotifier>(), null)
    {
        ControllerContext = new ControllerContext { HttpContext = Http() },
        Url = Mock.Of<IUrlHelper>(),
        TempData = Mock.Of<ITempDataDictionary>(),
    };

    private DefaultHttpContext Http() => new() { RequestServices = _services };
    private static DeploymentPlanWriteResponse Write(IResult result) => Assert.IsType<Ok<DeploymentPlanWriteResponse>>(result).Value;
    private static int? Status(IResult result) => Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode;
    private static IAuthorizationService Authorized() => Authorize(RemoteManagementPermissions.AccessRemoteManagement, DeploymentPermissions.ManageDeploymentPlan);
    private static IAuthorizationService Authorize(params Permission[] permissions)
    {
        var auth = new Mock<IAuthorizationService>();
        auth.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => permissions.Any(permission => permission.Name == requirement.Permission.Name))
                    ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        return auth.Object;
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
