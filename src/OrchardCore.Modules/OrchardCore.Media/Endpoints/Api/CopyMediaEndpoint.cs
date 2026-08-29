using System.IO;
using System.Linq;
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

public static class CopyMediaEndpoint
{
    public static IEndpointRouteBuilder AddCopyMediaEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapLegacyPost("api/media/CopyMedia", HandleLegacyAsync)
            .AddEndpointFilter<MediaApiAntiforgeryEndpointFilter>();

        builder.MapManagementPost("api/media/files:copy", HandleAsync)
            .WithName("ApiCopyMedia")
            .WithSummary("Copies a media file.")
            .WithDescription("Copies a media file to a new media path after validating both folder permissions and the destination file extension.")
            .WithCliCommand(new CliOperationMetadata(["media", "files"], "copy")
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
            .Accepts<CopyMediaRequest>("application/json")
            .Produces<CopyMediaResultDto>(StatusCodes.Status200OK)
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
        [FromServices] IStringLocalizer<MediaApiEndpoints> localizer,
        string oldPath,
        string newPath)
        => HandleLegacyResultAsync(httpContext, authorizationService, mediaFileStore, contentTypeProvider, fileVersionProvider, options, localizer, oldPath, newPath);

    private static Task<IResult> HandleAsync(
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IMediaFileStore mediaFileStore,
        [FromServices] IContentTypeProvider contentTypeProvider,
        [FromServices] IFileVersionProvider fileVersionProvider,
        [FromServices] IOptions<MediaOptions> options,
        [FromServices] IStringLocalizer<MediaApiEndpoints> localizer,
        [FromBody] CopyMediaRequest request)
        => HandleManagementAsync(httpContext, authorizationService, mediaFileStore, contentTypeProvider, fileVersionProvider, options, localizer, request.OldPath, request.NewPath);

    private static async Task<IResult> HandleLegacyResultAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IMediaFileStore mediaFileStore,
        IContentTypeProvider contentTypeProvider,
        IFileVersionProvider fileVersionProvider,
        IOptions<MediaOptions> options,
        IStringLocalizer<MediaApiEndpoints> localizer,
        string oldPath,
        string newPath)
    {
        var (result, copiedFile) = await CopyAsync(httpContext, authorizationService, mediaFileStore, options, localizer, oldPath, newPath, allowCompleted: false);

        return result ?? TypedResults.Ok(MediaEndpointHelpers.CreateFileResult(copiedFile, httpContext, contentTypeProvider, fileVersionProvider, mediaFileStore));
    }

    private static async Task<IResult> HandleManagementAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IMediaFileStore mediaFileStore,
        IContentTypeProvider contentTypeProvider,
        IFileVersionProvider fileVersionProvider,
        IOptions<MediaOptions> options,
        IStringLocalizer<MediaApiEndpoints> localizer,
        string oldPath,
        string newPath)
    {
        var (result, copiedFile) = await CopyAsync(httpContext, authorizationService, mediaFileStore, options, localizer, oldPath, newPath, allowCompleted: true);

        return result ?? TypedResults.Ok(new CopyMediaResultDto
        {
            OldPath = oldPath,
            NewPath = newPath,
            File = MediaEndpointHelpers.CreateFileResult(copiedFile, httpContext, contentTypeProvider, fileVersionProvider, mediaFileStore),
        });
    }

    private static async Task<(IResult Result, IFileStoreEntry File)> CopyAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IMediaFileStore mediaFileStore,
        IOptions<MediaOptions> options,
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

        var sourceFile = await mediaFileStore.GetFileInfoAsync(oldPath);
        if (sourceFile == null)
        {
            return (httpContext.ApiNotFoundProblem(), null);
        }

        var newExtension = Path.GetExtension(newPath);

        if (!options.Value.AllowedFileExtensions.Contains(newExtension, System.StringComparer.OrdinalIgnoreCase))
        {
            return (httpContext.ApiValidationProblem(detail: localizer["This file extension is not allowed: {0}", newExtension]), null);
        }

        var targetFile = await mediaFileStore.GetFileInfoAsync(newPath);
        if (targetFile != null)
        {
            if (allowCompleted && await MediaEndpointHelpers.FilesAreEqualAsync(mediaFileStore, sourceFile, targetFile, httpContext.RequestAborted))
            {
                return (null, targetFile);
            }

            return (httpContext.ApiValidationProblem(detail: localizer["Cannot copy media because a file already exists with the same name"]), null);
        }

        if (!await mediaFileStore.TryCopyFileAsync(oldPath, newPath))
        {
            targetFile = await mediaFileStore.GetFileInfoAsync(newPath);
            if (allowCompleted &&
                targetFile != null &&
                await MediaEndpointHelpers.FilesAreEqualAsync(mediaFileStore, sourceFile, targetFile, httpContext.RequestAborted))
            {
                return (null, targetFile);
            }

            return (httpContext.ApiValidationProblem(detail: localizer["Cannot copy media because a file already exists with different content"]), null);
        }

        var copiedFile = await mediaFileStore.GetFileInfoAsync(newPath);

        return (null, copiedFile);
    }
}
