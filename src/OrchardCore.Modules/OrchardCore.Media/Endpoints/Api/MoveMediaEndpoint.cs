using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.FileStorage;
using OrchardCore.Media.ViewModels;
using OrchardCore.RemoteManagement;

namespace OrchardCore.Media.Endpoints.Api;

public static class MoveMediaEndpoint
{
    public static IEndpointRouteBuilder AddMoveMediaEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapLegacyPost("api/media/MoveMedia", HandleLegacyAsync)
            .AddEndpointFilter<MediaApiAntiforgeryEndpointFilter>();

        builder.MapManagementPost("api/media/files:move", HandleAsync)
            .WithName("ApiMoveMedia")
            .WithSummary("Moves a media file.")
            .WithDescription("Moves a media file to a new path after validating both folder permissions and the destination file extension.")
            .WithCliCommand(new CliOperationMetadata(["media", "files"], "move")
            {
                Capability = MediaApiEndpointConventions.CapabilityName,
                InputMode = CliInputMode.Options,
                TableColumns =
                {
                    new CliTableColumnMetadata("file.name", "Name"),
                    new CliTableColumnMetadata("file.filePath", "Path"),
                    new CliTableColumnMetadata("oldPath", "Source"),
                },
            })
            .Accepts<MoveMediaRequest>("application/json")
            .Produces<MoveMediaResultDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return builder;
    }

    private static Task<IResult> HandleLegacyAsync(
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IMediaFileStore mediaFileStore,
        [FromServices] IContentTypeProvider contentTypeProvider,
        [FromServices] IFileVersionProvider fileVersionProvider,
        [FromServices] IOptions<MediaOptions> options,
        [FromServices] IServiceProvider serviceProvider,
        [FromServices] IStringLocalizer<MediaApiEndpoints> localizer,
        string oldPath,
        string newPath)
        => HandleLegacyResultAsync(httpContext, authorizationService, mediaFileStore, contentTypeProvider, fileVersionProvider, options, serviceProvider, localizer, oldPath, newPath);

    private static Task<IResult> HandleAsync(
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IMediaFileStore mediaFileStore,
        [FromServices] IContentTypeProvider contentTypeProvider,
        [FromServices] IFileVersionProvider fileVersionProvider,
        [FromServices] IOptions<MediaOptions> options,
        [FromServices] IServiceProvider serviceProvider,
        [FromServices] IStringLocalizer<MediaApiEndpoints> localizer,
        [FromBody] MoveMediaRequest request)
        => HandleManagementAsync(httpContext, authorizationService, mediaFileStore, contentTypeProvider, fileVersionProvider, options, serviceProvider, localizer, request.OldPath, request.NewPath);

    private static async Task<IResult> HandleLegacyResultAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IMediaFileStore mediaFileStore,
        IContentTypeProvider contentTypeProvider,
        IFileVersionProvider fileVersionProvider,
        IOptions<MediaOptions> options,
        IServiceProvider serviceProvider,
        IStringLocalizer<MediaApiEndpoints> localizer,
        string oldPath,
        string newPath)
    {
        var (result, movedFile) = await MoveAsync(httpContext, authorizationService, mediaFileStore, options, serviceProvider, localizer, oldPath, newPath, allowCompleted: false);

        return result ?? TypedResults.Ok(MediaEndpointHelpers.CreateFileResult(movedFile, httpContext, contentTypeProvider, fileVersionProvider, mediaFileStore));
    }

    private static async Task<IResult> HandleManagementAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IMediaFileStore mediaFileStore,
        IContentTypeProvider contentTypeProvider,
        IFileVersionProvider fileVersionProvider,
        IOptions<MediaOptions> options,
        IServiceProvider serviceProvider,
        IStringLocalizer<MediaApiEndpoints> localizer,
        string oldPath,
        string newPath)
    {
        var (result, movedFile) = await MoveAsync(httpContext, authorizationService, mediaFileStore, options, serviceProvider, localizer, oldPath, newPath, allowCompleted: true);

        return result ?? TypedResults.Ok(new MoveMediaResultDto
        {
            OldPath = oldPath,
            NewPath = newPath,
            File = MediaEndpointHelpers.CreateFileResult(movedFile, httpContext, contentTypeProvider, fileVersionProvider, mediaFileStore),
        });
    }

    private static async Task<(IResult Result, IFileStoreEntry File)> MoveAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IMediaFileStore mediaFileStore,
        IOptions<MediaOptions> options,
        IServiceProvider serviceProvider,
        IStringLocalizer<MediaApiEndpoints> localizer,
        string oldPath,
        string newPath,
        bool allowCompleted)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMedia)
            || !await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)oldPath)
            || !await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)newPath))
        {
            return (httpContext.ApiForbidProblem(), null);
        }

        if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
        {
            return (httpContext.ApiNotFoundProblem(), null);
        }

        var newExtension = Path.GetExtension(newPath);

        if (!options.Value.AllowedFileExtensions.Contains(newExtension, StringComparer.OrdinalIgnoreCase))
        {
            return (httpContext.ApiValidationProblem(detail: localizer["This file extension is not allowed: {0}", newExtension]), null);
        }

        var sourceFile = await mediaFileStore.GetFileInfoAsync(oldPath);
        var targetFile = await mediaFileStore.GetFileInfoAsync(newPath);

        if (sourceFile == null)
        {
            return allowCompleted && targetFile != null
                ? (null, targetFile)
                : (httpContext.ApiNotFoundProblem(), null);
        }

        if (targetFile != null)
        {
            if (allowCompleted &&
                await MediaEndpointHelpers.TryCompleteMoveAsync(mediaFileStore, sourceFile, targetFile, serviceProvider, httpContext.RequestAborted))
            {
                await MediaEndpointHelpers.PreCacheRemoteMediaAsync(targetFile, serviceProvider, mediaFileStore, httpContext);
                return (null, targetFile);
            }

            return (httpContext.ApiValidationProblem(detail: localizer["Cannot move media because a file already exists with the same name"]), null);
        }

        try
        {
            if (!await mediaFileStore.TryMoveFileAsync(oldPath, newPath))
            {
                sourceFile = await mediaFileStore.GetFileInfoAsync(oldPath);
                targetFile = await mediaFileStore.GetFileInfoAsync(newPath);
                if (!allowCompleted ||
                    !await MediaEndpointHelpers.TryCompleteMoveAsync(mediaFileStore, sourceFile, targetFile, serviceProvider, httpContext.RequestAborted))
                {
                    return (httpContext.ApiValidationProblem(detail: localizer["Cannot move media because the target contains different content"]), null);
                }
            }
        }
        catch (FileStoreException) when (allowCompleted)
        {
            sourceFile = await mediaFileStore.GetFileInfoAsync(oldPath);
            targetFile = await mediaFileStore.GetFileInfoAsync(newPath);
            if (!await MediaEndpointHelpers.TryCompleteMoveAsync(mediaFileStore, sourceFile, targetFile, serviceProvider, httpContext.RequestAborted))
            {
                return (httpContext.ApiValidationProblem(detail: localizer["Cannot move media because the target contains different content"]), null);
            }
        }

        var movedFile = await mediaFileStore.GetFileInfoAsync(newPath);
        await MediaEndpointHelpers.PreCacheRemoteMediaAsync(movedFile, serviceProvider, mediaFileStore, httpContext);

        return (null, movedFile);
    }
}
