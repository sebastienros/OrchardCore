using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using OrchardCore.FileStorage;
using OrchardCore.Media.ViewModels;
using OrchardCore.RemoteManagement;

namespace OrchardCore.Media.Endpoints.Api;

public static class GetAllMediaItemsEndpoint
{
    public static IEndpointRouteBuilder AddGetAllMediaItemsEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapLegacyGet("api/media/GetAllMediaItems", HandleAsync);

        builder.MapManagementGet("api/media/items", HandleAsync)
            .WithName("ApiGetAllMediaItems")
            .WithSummary("Lists all accessible media items.")
            .WithDescription("Returns every accessible media file and folder under the media root, optionally filtered by file extension.")
            .WithCliCommand(new CliOperationMetadata(["media", "items"], "list")
            {
                Capability = MediaApiEndpointConventions.CapabilityName,
                TableColumns =
                {
                    new CliTableColumnMetadata("items[].name", "Name"),
                    new CliTableColumnMetadata("items[].filePath", "Path"),
                    new CliTableColumnMetadata("items[].size", "Size"),
                    new CliTableColumnMetadata("items[].mime", "Content type"),
                },
            })
            .Produces<MediaListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IMediaFileStore mediaFileStore,
        [FromServices] IContentTypeProvider contentTypeProvider,
        [FromServices] IFileVersionProvider fileVersionProvider,
        [FromServices] IOptions<MediaOptions> options,
        [FromServices] IUserAssetFolderNameProvider userAssetFolderNameProvider,
        [AsParameters] GetAllMediaItemsRequest request)
    {
        var extensions = request.Extensions;
        var isManagementEndpoint = httpContext.GetEndpoint()?.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName == "ApiGetAllMediaItems";
        var skip = request.Skip ?? 0;
        var take = request.Take ?? MediaEndpointHelpers.DefaultTake;

        if (isManagementEndpoint && MediaEndpointHelpers.ValidatePaging(skip, take) is { } pagingError)
        {
            return pagingError;
        }

        if (!await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMedia)
            || !await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageMediaFolder, (object)string.Empty))
        {
            return httpContext.ApiForbidProblem();
        }

        var mediaOptions = options.Value;

        // create default folders if not exist
        if (await authorizationService.AuthorizeAsync(httpContext.User, MediaPermissions.ManageOwnMedia)
            && await mediaFileStore.GetDirectoryInfoAsync(mediaFileStore.Combine(mediaOptions.AssetsUsersFolder, userAssetFolderNameProvider.GetUserAssetFolderName(httpContext.User))) == null)
        {
            await mediaFileStore.TryCreateDirectoryAsync(mediaFileStore.Combine(mediaOptions.AssetsUsersFolder, userAssetFolderNameProvider.GetUserAssetFolderName(httpContext.User)));
        }

        var allowedExtensions = MediaEndpointHelpers.GetRequestedExtensions(mediaOptions, extensions, false);
        var allItems = new List<FileStoreEntryDto>();

        await MediaEndpointHelpers.CollectAllItemsRecursiveAsync(mediaFileStore, authorizationService, httpContext, contentTypeProvider, fileVersionProvider, string.Empty, allowedExtensions, allItems);
        allItems.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.FilePath ?? left.DirectoryPath, right.FilePath ?? right.DirectoryPath));

        return isManagementEndpoint
            ? TypedResults.Ok(new MediaListResponse
            {
                Skip = skip,
                Take = take,
                TotalCount = allItems.Count,
                Items = allItems.Skip(skip).Take(take).ToArray(),
            })
            : TypedResults.Ok(allItems);
    }
}
