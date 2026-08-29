using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;
using OrchardCore.RemoteManagement;
using OrchardCore.Settings;

namespace OrchardCore.HomeRoute.Endpoints;

internal static class HomeRouteManagementEndpoints
{
    private const string Route = "api/home-route/content/{contentItemId}";

    public static IEndpointRouteBuilder AddHomeRouteManagementEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPut(Route, SetHomeContentAsync)
            .WithName("ApiSetHomeContent")
            .WithTags("Home Route")
            .WithSummary("Sets the home content item.")
            .WithDescription("Sets a published content item as the tenant home route.")
            .WithCliCommand(new CliOperationMetadata(["settings"], "set-home-content")
            {
                Capability = "home-route",
                Arguments = { new CliArgumentMetadata("contentItemId", 0) },
            })
            .DisableAntiforgery()
            .RequireAuthorization(static policy => policy
                .AddAuthenticationSchemes(OrchardCoreConstants.AuthenticationSchemes.Api)
                .AddRequirements(new OrchardCore.Security.PermissionRequirement(RemoteManagementPermissions.AccessRemoteManagement))
                .RequireAuthenticatedUser())
            .Produces<HomeRouteResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return builder;
    }

    internal static async Task<IResult> SetHomeContentAsync(
        HttpContext httpContext,
        string contentItemId,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IContentManager contentManager,
        [FromServices] ISiteService siteService)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, Permissions.SetHomeRoute))
        {
            return httpContext.ApiForbidProblem();
        }

        var contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.Published);
        if (contentItem is null)
        {
            return TypedResults.Problem(
                title: "Not found",
                detail: $"Published content item not found: '{contentItemId}'.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var site = await siteService.LoadSiteSettingsAsync();
        var existingContentItemId = site.HomeRoute?["ContentItemId"]?.ToString();
        if (!string.Equals(existingContentItemId, contentItem.ContentItemId, StringComparison.Ordinal) ||
            !string.Equals(site.HomeRoute?["Area"]?.ToString(), "OrchardCore.Contents", StringComparison.Ordinal) ||
            !string.Equals(site.HomeRoute?["Controller"]?.ToString(), "Item", StringComparison.Ordinal) ||
            !string.Equals(site.HomeRoute?["Action"]?.ToString(), "Display", StringComparison.Ordinal))
        {
            site.HomeRoute = new RouteValueDictionary
            {
                ["Area"] = "OrchardCore.Contents",
                ["Controller"] = "Item",
                ["Action"] = "Display",
                ["ContentItemId"] = contentItem.ContentItemId,
            };
            await siteService.UpdateSiteSettingsAsync(site);
        }

        return TypedResults.Ok(new HomeRouteResponse
        {
            ContentItemId = contentItem.ContentItemId,
            ContentType = contentItem.ContentType,
            DisplayText = contentItem.DisplayText,
        });
    }

    internal sealed class HomeRouteResponse
    {
        public string ContentItemId { get; init; }
        public string ContentType { get; init; }
        public string DisplayText { get; init; }
    }
}
