using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;
using OrchardCore.HomeRoute.Endpoints;
using OrchardCore.Settings;

namespace OrchardCore.Tests.Modules.OrchardCore.HomeRoute;

public class HomeRouteManagementEndpointsTests
{
    [Fact]
    public async Task SetHomeContentAsync_PublishedContent_SetsContentDisplayRoute()
    {
        var contentItem = new ContentItem
        {
            ContentItemId = "home-id",
            ContentType = "LandingPage",
            DisplayText = "Home",
            Published = true,
        };
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(manager => manager.GetAsync("home-id", VersionOptions.Published))
            .ReturnsAsync(contentItem);
        var site = new Mock<ISite>();
        site.SetupProperty(settings => settings.HomeRoute, []);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.LoadSiteSettingsAsync()).ReturnsAsync(site.Object);

        var result = await HomeRouteManagementEndpoints.SetHomeContentAsync(
            new DefaultHttpContext(),
            "home-id",
            CreateAuthorizationService().Object,
            contentManager.Object,
            siteService.Object);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("OrchardCore.Contents", site.Object.HomeRoute["Area"]);
        Assert.Equal("Item", site.Object.HomeRoute["Controller"]);
        Assert.Equal("Display", site.Object.HomeRoute["Action"]);
        Assert.Equal("home-id", site.Object.HomeRoute["ContentItemId"]);
        siteService.Verify(service => service.UpdateSiteSettingsAsync(site.Object), Times.Once);
    }

    [Fact]
    public async Task SetHomeContentAsync_CurrentContentRoute_DoesNotWriteAgain()
    {
        var contentItem = new ContentItem { ContentItemId = "home-id", Published = true };
        var contentManager = Mock.Of<IContentManager>(manager =>
            manager.GetAsync("home-id", VersionOptions.Published) == Task.FromResult(contentItem));
        var homeRoute = new RouteValueDictionary
        {
            ["Area"] = "OrchardCore.Contents",
            ["Controller"] = "Item",
            ["Action"] = "Display",
            ["ContentItemId"] = "home-id",
        };
        var site = Mock.Of<ISite>(settings => settings.HomeRoute == homeRoute);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.LoadSiteSettingsAsync()).ReturnsAsync(site);

        var result = await HomeRouteManagementEndpoints.SetHomeContentAsync(
            new DefaultHttpContext(),
            "home-id",
            CreateAuthorizationService().Object,
            contentManager,
            siteService.Object);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        siteService.Verify(service => service.UpdateSiteSettingsAsync(It.IsAny<ISite>()), Times.Never);
    }

    private static Mock<IAuthorizationService> CreateAuthorizationService()
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        return authorizationService;
    }
}
