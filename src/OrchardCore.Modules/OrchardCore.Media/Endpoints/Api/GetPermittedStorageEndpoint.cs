using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Media.Core.Helpers;
using OrchardCore.Media.ViewModels;
using OrchardCore.RemoteManagement;
using OrchardCore.Settings;

namespace OrchardCore.Media.Endpoints.Api;

public static class GetPermittedStorageEndpoint
{
    public static IEndpointRouteBuilder AddGetPermittedStorageEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapLegacyGet("api/media/GetPermittedStorage", HandleLegacyAsync);

        builder.MapManagementGet("api/media/constraints", HandleAsync)
            .WithName("ApiGetPermittedStorage")
            .WithSummary("Shows the media storage constraints.")
            .WithDescription("Returns the configured media storage limits, authentication mode, and allowed file extensions for remote media management clients.")
            .WithCliCommand(new CliOperationMetadata(["media", "constraints"], "show")
            {
                Capability = MediaApiEndpointConventions.CapabilityName,
                TableColumns =
                {
                    new CliTableColumnMetadata("text", "Permitted storage"),
                    new CliTableColumnMetadata("maxFileSize", "Max file size"),
                    new CliTableColumnMetadata("authenticationScheme", "Authentication"),
                },
            })
            .Produces<MediaConstraintsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return builder;
    }

    private static async Task<IResult> HandleLegacyAsync(
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IMediaFileStore mediaFileStore,
        [FromServices] IStringLocalizer<MediaApiEndpoints> localizer)
    {
        var (result, bytes, text) = await GetStorageConstraintsAsync(httpContext, authorizationService, mediaFileStore, localizer);

        return result ?? TypedResults.Ok(new PermittedStorageDto
        {
            Bytes = bytes,
            Text = text,
        });
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IMediaFileStore mediaFileStore,
        [FromServices] IStringLocalizer<MediaApiEndpoints> localizer,
        [FromServices] ISiteService siteService,
        [FromServices] IOptions<MediaOptions> options)
    {
        var (result, bytes, text) = await GetStorageConstraintsAsync(httpContext, authorizationService, mediaFileStore, localizer);

        if (result is not null)
        {
            return result;
        }

        var mediaOptions = options.Value;

        return TypedResults.Ok(new MediaConstraintsDto
        {
            Bytes = bytes,
            Text = text,
            AllowedFileExtensions = mediaOptions.AllowedFileExtensions.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            MaxFileSize = mediaOptions.MaxFileSize,
            MaxUploadChunkSize = mediaOptions.MaxUploadChunkSize,
            AuthenticationScheme = siteService.GetSettings<MediaApiSettings>().AuthenticationScheme.ToString(),
        });
    }

    private static async Task<(IResult Result, long? Bytes, string Text)> GetStorageConstraintsAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IMediaFileStore mediaFileStore,
        IStringLocalizer<MediaApiEndpoints> localizer)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMedia)
            || !await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)string.Empty))
        {
            return (httpContext.ApiForbidProblem(), null, null);
        }

        var bytes = await mediaFileStore.GetPermittedStorageAsync();
        var text = bytes == null ? localizer["Unspecified"] : FileSizeHelpers.FormatAsBytes(bytes.Value);

        return (null, bytes, text);
    }
}
