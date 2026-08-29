using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using OrchardCore.RemoteManagement;
using OrchardCore.Security;
using OrchardCore.Tenants.Services;

namespace OrchardCore.Tenants.Endpoints.Management;

internal static class StaticFileManagementEndpoints
{
    private const string RoutePrefix = "api/static-files";
    private const int DefaultTake = 50;
    private const int MaximumTake = 200;

    public static IEndpointRouteBuilder AddStaticFileManagementEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapManagementGet(RoutePrefix, ListAsync)
            .RequireAuthorization(policy => policy.AddRequirements(new PermissionRequirement(Permissions.ViewTenantStaticFiles)))
            .WithName("ApiListStaticFiles")
            .WithSummary("Lists tenant static files.")
            .WithDescription("Lists files and directories in the tenant static-file root. Use an empty path for the root directory.")
            .WithCliCommand(new CliOperationMetadata(["static", "files"], "list")
            {
                Capability = StaticFileRemoteManagementCapabilityProvider.CapabilityName,
                TableColumns =
                {
                    new CliTableColumnMetadata("items.path", "Path"),
                    new CliTableColumnMetadata("items.isDirectory", "Directory"),
                    new CliTableColumnMetadata("items.length", "Size"),
                },
            })
            .Produces<StaticFileListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        builder.MapManagementGet(RoutePrefix + "/file", GetAsync)
            .RequireAuthorization(policy => policy.AddRequirements(new PermissionRequirement(Permissions.ViewTenantStaticFiles)))
            .WithName("ApiGetStaticFile")
            .WithSummary("Gets tenant static-file metadata.")
            .WithDescription("Returns metadata and the public URL for a tenant static file.")
            .WithCliCommand(new CliOperationMetadata(["static", "files"], "show")
            {
                Capability = StaticFileRemoteManagementCapabilityProvider.CapabilityName,
                Arguments = { new CliArgumentMetadata("path", 0) },
            })
            .Produces<StaticFileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        builder.MapManagementPut(RoutePrefix + "/content", UploadAsync)
            .RequireAuthorization(policy => policy.AddRequirements(new PermissionRequirement(Permissions.ManageTenantStaticFiles)))
            .WithName("ApiUploadStaticFile")
            .WithSummary("Uploads a tenant static file.")
            .WithDescription("Writes the binary request body to a path under the tenant static-file root. For example: oc static files upload styles/site.css --file ./site.css")
            .WithCliCommand(new CliOperationMetadata(["static", "files"], "upload")
            {
                Capability = StaticFileRemoteManagementCapabilityProvider.CapabilityName,
                InputMode = CliInputMode.Stream,
                Arguments = { new CliArgumentMetadata("path", 0) },
                TableColumns =
                {
                    new CliTableColumnMetadata("path", "Path"),
                    new CliTableColumnMetadata("url", "Url"),
                    new CliTableColumnMetadata("length", "Size"),
                },
            })
            .Accepts<Stream>("application/octet-stream")
            .Produces<StaticFileResponse>(StatusCodes.Status200OK)
            .Produces<StaticFileResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return builder;
    }

    private static IResult ListAsync(HttpContext httpContext, TenantFileProvider fileProvider, [AsParameters] StaticFileListRequest request)
    {
        var skip = request.Skip ?? 0;
        var take = request.Take ?? DefaultTake;
        if (ValidatePaging(skip, take) is { } pagingError)
        {
            return pagingError;
        }

        if (!TryNormalizePath(request.Path, allowEmpty: true, out var path))
        {
            return TypedResults.Problem(detail: "The static-file path must be relative and cannot contain '.' or '..' segments.", statusCode: StatusCodes.Status400BadRequest);
        }

        var contents = fileProvider.GetDirectoryContents(path);
        if (!contents.Exists)
        {
            return TypedResults.Problem(detail: $"Static-file directory '{path}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        var items = contents
            .OrderBy(entry => entry.IsDirectory ? 0 : 1)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => ToResponse(httpContext, CombinePath(path, entry.Name), entry))
            .ToArray();

        return TypedResults.Ok(new StaticFileListResponse
        {
            Skip = skip,
            Take = take,
            TotalCount = items.Length,
            Items = items.Skip(skip).Take(take).ToArray(),
        });
    }

    private static IResult GetAsync(HttpContext httpContext, TenantFileProvider fileProvider, string path)
    {
        if (!TryNormalizePath(path, allowEmpty: false, out var normalizedPath))
        {
            return TypedResults.Problem(detail: "The static-file path must be relative and cannot contain '.' or '..' segments.", statusCode: StatusCodes.Status400BadRequest);
        }

        var file = fileProvider.GetFileInfo(normalizedPath);
        return file.Exists && !file.IsDirectory
            ? TypedResults.Ok(ToResponse(httpContext, normalizedPath, file))
            : TypedResults.Problem(detail: $"Static file '{normalizedPath}' was not found.", statusCode: StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> UploadAsync(
        HttpContext httpContext,
        TenantFileProvider fileProvider,
        string path,
        bool overwrite = false)
    {
        if (!TryNormalizePath(path, allowEmpty: false, out var normalizedPath))
        {
            return TypedResults.Problem(detail: "The static-file path must be relative and cannot contain '.' or '..' segments.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TryResolvePhysicalPath(fileProvider.Root, normalizedPath, out var physicalPath))
        {
            return TypedResults.Problem(detail: "The static-file path resolves outside the tenant static-file root or traverses a symbolic link.", statusCode: StatusCodes.Status400BadRequest);
        }

        var fileExists = File.Exists(physicalPath);
        if (!overwrite && fileExists)
        {
            if (await ContentEqualsAsync(httpContext.Request.Body, physicalPath, httpContext.RequestAborted))
            {
                return TypedResults.Ok(ToResponse(httpContext, normalizedPath, fileProvider.GetFileInfo(normalizedPath)));
            }

            return TypedResults.Problem(
                detail: $"Static file '{normalizedPath}' already exists. Pass --overwrite true to replace it.",
                statusCode: StatusCodes.Status409Conflict);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        var temporaryPath = $"{physicalPath}.{Guid.NewGuid():n}.tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await httpContext.Request.Body.CopyToAsync(stream, httpContext.RequestAborted);
            }

            File.Move(temporaryPath, physicalPath, overwrite);
        }
        finally
        {
            File.Delete(temporaryPath);
        }

        var file = fileProvider.GetFileInfo(normalizedPath);
        return fileExists
            ? TypedResults.Ok(ToResponse(httpContext, normalizedPath, file))
            : TypedResults.Created(ToPublicUrl(httpContext, normalizedPath), ToResponse(httpContext, normalizedPath, file));
    }

    internal static async Task<bool> ContentEqualsAsync(Stream requestBody, string physicalPath, CancellationToken cancellationToken)
    {
        await using var existing = new FileStream(
            physicalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var existingBuffer = new byte[81920];
        var requestBuffer = new byte[81920];
        while (true)
        {
            var existingRead = await existing.ReadAtLeastAsync(existingBuffer, existingBuffer.Length, throwOnEndOfStream: false, cancellationToken: cancellationToken);
            var requestRead = await requestBody.ReadAtLeastAsync(requestBuffer, requestBuffer.Length, throwOnEndOfStream: false, cancellationToken: cancellationToken);
            if (existingRead != requestRead)
            {
                return false;
            }

            if (existingRead == 0)
            {
                return true;
            }

            if (!existingBuffer.AsSpan(0, existingRead).SequenceEqual(requestBuffer.AsSpan(0, requestRead)))
            {
                return false;
            }
        }
    }

    internal static bool TryNormalizePath(string path, bool allowEmpty, out string normalizedPath)
    {
        normalizedPath = path?.Replace('\\', '/')?.Trim('/') ?? string.Empty;
        if (normalizedPath.Length == 0)
        {
            return allowEmpty;
        }

        if (Path.IsPathRooted(path) || normalizedPath.Split('/').Any(segment => segment is "." or ".." || segment.Length == 0))
        {
            normalizedPath = string.Empty;
            return false;
        }

        return true;
    }

    internal static bool TryResolvePhysicalPath(string root, string path, out string physicalPath)
    {
        var rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        physicalPath = Path.GetFullPath(Path.Combine(rootPath, path.Replace('/', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!physicalPath.StartsWith(rootPath + Path.DirectorySeparatorChar, comparison))
        {
            return false;
        }

        var current = rootPath;
        foreach (var segment in path.Split('/').SkipLast(1))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) && new DirectoryInfo(current).LinkTarget is not null)
            {
                return false;
            }
        }

        return !File.Exists(physicalPath) || new FileInfo(physicalPath).LinkTarget is null;
    }

    private static StaticFileResponse ToResponse(HttpContext httpContext, string path, IFileInfo file) => new()
    {
        Path = path,
        Name = file.Name,
        IsDirectory = file.IsDirectory,
        Length = file.IsDirectory ? null : file.Length,
        LastModified = file.LastModified,
        Url = file.IsDirectory ? null : ToPublicUrl(httpContext, path),
    };

    private static string CombinePath(string path, string name) =>
        string.IsNullOrEmpty(path) ? name : $"{path}/{name}";

    private static string ToPublicUrl(HttpContext httpContext, string path)
    {
        var encodedPath = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
        return $"{httpContext.Request.PathBase}/{encodedPath}";
    }

    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult ValidatePaging(int skip, int take)
    {
        if (skip < 0 || take < 1)
        {
            return TypedResults.Problem(detail: "Skip must be zero or greater and take must be greater than zero.", statusCode: StatusCodes.Status400BadRequest);
        }

        return take > MaximumTake
            ? TypedResults.Problem(detail: $"Take cannot exceed {MaximumTake}.", statusCode: StatusCodes.Status400BadRequest)
            : null;
    }

    internal sealed class StaticFileListRequest
    {
        public string Path { get; init; }
        public int? Skip { get; init; }
        public int? Take { get; init; }
    }

    internal sealed class StaticFileListResponse
    {
        public int Skip { get; init; }
        public int Take { get; init; }
        public int TotalCount { get; init; }
        public StaticFileResponse[] Items { get; init; } = [];
    }

    internal sealed class StaticFileResponse
    {
        public string Path { get; init; }
        public string Name { get; init; }
        public bool IsDirectory { get; init; }
        public long? Length { get; init; }
        public DateTimeOffset LastModified { get; init; }
        public string Url { get; init; }
    }
}
