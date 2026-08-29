using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.FileStorage;
using OrchardCore.Media.ViewModels;
using OrchardCore.RemoteManagement;

namespace OrchardCore.Media.Endpoints.Api;

public static class MoveMediaListEndpoint
{
    public static IEndpointRouteBuilder AddMoveMediaListEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapLegacyPost("api/media/MoveMediaList", HandleLegacyAsync)
            .AddEndpointFilter<MediaApiAntiforgeryEndpointFilter>();

        builder.MapManagementPost("api/media/files:move-batch", HandleAsync)
            .WithName("ApiMoveMediaList")
            .WithSummary("Moves multiple media files.")
            .WithDescription("Moves a batch of media files from one folder to another after verifying both folder permissions.")
            .WithCliCommand(new CliOperationMetadata(["media", "files"], "move-batch")
            {
                Capability = MediaApiEndpointConventions.CapabilityName,
                InputMode = CliInputMode.Json,
            })
            .Accepts<MoveMediaBatchRequest>("application/json")
            .Produces<MoveMediaBatchResultDto>(StatusCodes.Status200OK)
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
        [FromServices] IStringLocalizer<MediaApiEndpoints> localizer,
        [FromBody] MoveMedias model)
        => HandleLegacyResultAsync(httpContext, authorizationService, mediaFileStore, localizer, model.mediaNames, model.sourceFolder, model.targetFolder);

    private static Task<IResult> HandleAsync(
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IMediaFileStore mediaFileStore,
        [FromServices] IStringLocalizer<MediaApiEndpoints> localizer,
        [FromBody] MoveMediaBatchRequest request)
        => HandleManagementAsync(httpContext, authorizationService, mediaFileStore, localizer, request.MediaNames, request.SourceFolder, request.TargetFolder);

    private static async Task<IResult> HandleLegacyResultAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IMediaFileStore mediaFileStore,
        IStringLocalizer<MediaApiEndpoints> localizer,
        string[] mediaNames,
        string sourceFolder,
        string targetFolder)
    {
        var (result, normalizedSourceFolder, normalizedTargetFolder) = await MoveAsync(httpContext, authorizationService, mediaFileStore, localizer, mediaNames, sourceFolder, targetFolder, allowCompleted: false);

        return result ?? TypedResults.Ok();
    }

    private static async Task<IResult> HandleManagementAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IMediaFileStore mediaFileStore,
        IStringLocalizer<MediaApiEndpoints> localizer,
        string[] mediaNames,
        string sourceFolder,
        string targetFolder)
    {
        var (result, normalizedSourceFolder, normalizedTargetFolder) = await MoveAsync(httpContext, authorizationService, mediaFileStore, localizer, mediaNames, sourceFolder, targetFolder, allowCompleted: true);

        return result ?? TypedResults.Ok(new MoveMediaBatchResultDto
        {
            MediaNames = mediaNames,
            SourceFolder = normalizedSourceFolder,
            TargetFolder = normalizedTargetFolder,
        });
    }

    private static async Task<(IResult Result, string SourceFolder, string TargetFolder)> MoveAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IMediaFileStore mediaFileStore,
        IStringLocalizer<MediaApiEndpoints> localizer,
        string[] mediaNames,
        string sourceFolder,
        string targetFolder,
        bool allowCompleted)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMedia)
            || !await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)sourceFolder)
            || !await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)targetFolder))
        {
            return (httpContext.ApiForbidProblem(), null, null);
        }

        if (mediaNames == null || mediaNames.Length < 1
            || string.IsNullOrEmpty(sourceFolder)
            || string.IsNullOrEmpty(targetFolder))
        {
            return (httpContext.ApiNotFoundProblem(), null, null);
        }

        sourceFolder = sourceFolder == "root" ? string.Empty : sourceFolder;
        targetFolder = targetFolder == "root" ? string.Empty : targetFolder;

        if (mediaNames.Any(name => !MediaEndpointHelpers.IsBaseName(name)))
        {
            return (httpContext.ApiValidationProblem(detail: localizer["Media names must not contain directory paths."]), null, null);
        }

        var paths = mediaNames
            .Select(name => (
                Source: mediaFileStore.Combine(sourceFolder, name),
                Target: mediaFileStore.Combine(targetFolder, name)))
            .ToArray();

        if (paths.Any(path => string.Equals(
            mediaFileStore.NormalizePath(path.Source),
            mediaFileStore.NormalizePath(path.Target),
            StringComparison.OrdinalIgnoreCase)))
        {
            return (httpContext.ApiValidationProblem(detail: localizer["Source and destination paths must be different."]), null, null);
        }

        foreach (var path in paths)
        {
            if (!await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)path.Source)
                || !await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)path.Target))
            {
                return (httpContext.ApiForbidProblem(), null, null);
            }
        }

        var filesOnError = new List<string>();

        foreach (var path in paths)
        {
            var sourcePath = path.Source;
            var targetPath = path.Target;

            if (allowCompleted)
            {
                var sourceFile = await mediaFileStore.GetFileInfoAsync(sourcePath);
                var targetFile = await mediaFileStore.GetFileInfoAsync(targetPath);

                if (sourceFile == null && targetFile != null)
                {
                    continue;
                }

                if (sourceFile == null)
                {
                    filesOnError.Add(sourcePath);
                    continue;
                }
            }

            try
            {
                if (!await mediaFileStore.TryMoveFileAsync(sourcePath, targetPath))
                {
                    var sourceFile = await mediaFileStore.GetFileInfoAsync(sourcePath);
                    var targetFile = await mediaFileStore.GetFileInfoAsync(targetPath);
                    if (!allowCompleted || sourceFile is not null || targetFile is null)
                    {
                        filesOnError.Add(sourcePath);
                    }
                }
            }
            catch (FileStoreException)
            {
                var sourceFile = await mediaFileStore.GetFileInfoAsync(sourcePath);
                var targetFile = await mediaFileStore.GetFileInfoAsync(targetPath);
                if (!allowCompleted || sourceFile is not null || targetFile is null)
                {
                    filesOnError.Add(sourcePath);
                }
            }
        }

        if (filesOnError.Count > 0)
        {
            return (httpContext.ApiValidationProblem(detail: localizer["Error when moving files. Maybe they already exist on the target folder? Files on error: {0}", string.Join(",", filesOnError)]), null, null);
        }

        return (null, sourceFolder, targetFolder);
    }
}
