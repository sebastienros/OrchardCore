using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Deployment.Artifacts;
using OrchardCore.RemoteManagement;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace OrchardCore.Deployment.Endpoints.Management;

internal static class DeploymentArtifactEndpoints
{
    public static void AddDeploymentArtifactEndpoints(this IEndpointRouteBuilder routes)
    {
        Configure(routes.MapGet("api/deployment/artifacts/{id}", GetAsync), "ApiGetDeploymentArtifact", "show")
            .Produces<DeploymentArtifactResponse>();
        Configure(routes.MapDelete("api/deployment/artifacts/{id}", DeleteAsync), "ApiDeleteDeploymentArtifact", "delete")
            .Produces<DeploymentArtifactDeleteResponse>().ProducesProblem(409);
        // Binary download becomes a Pomi command when explicit file-output support is available.
        Secure(routes.MapGet("api/deployment/artifacts/{id}/content", DownloadAsync), "ApiDownloadDeploymentArtifact")
            .Produces(200, contentType: "application/zip").Produces(200, contentType: "application/json");
    }

    private static RouteHandlerBuilder Configure(RouteHandlerBuilder builder, string name, string verb) => Secure(builder, name)
        .WithCliCommand(new CliOperationMetadata(["deployment", "artifacts"], verb)
        {
            Capability = "deployment-plans", RequiresConfirmation = verb == "delete",
            Arguments = { new CliArgumentMetadata("id", 0) },
        });

    private static RouteHandlerBuilder Secure(RouteHandlerBuilder builder, string name) => builder.WithName(name)
        .WithTags("Deployment Management")
        .RequireAuthorization(policy => policy.AddAuthenticationSchemes(OrchardCoreConstants.AuthenticationSchemes.Api)
            .RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(RemoteManagementPermissions.AccessRemoteManagement)))
        .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);

    internal static async Task<IResult> GetAsync(HttpContext context, [FromServices] IAuthorizationService authorization,
        [FromServices] DeploymentArtifactStore store, string id)
    {
        var owner = await OwnerAsync(context, authorization);
        if (owner is null) { return context.ApiForbidProblem(); }
        var artifact = await store.FindAsync(id, owner);
        if (artifact is null) { return context.ApiNotFoundProblem(); }
        return await AllowedAsync(context, authorization, artifact)
            ? TypedResults.Ok(Describe(artifact)) : context.ApiForbidProblem();
    }

    internal static async Task<IResult> DeleteAsync(HttpContext context, [FromServices] IAuthorizationService authorization,
        [FromServices] DeploymentArtifactStore store, string id)
    {
        var owner = await OwnerAsync(context, authorization);
        if (owner is null) { return context.ApiForbidProblem(); }
        var artifact = await store.FindAsync(id, owner, includeExpired: true);
        if (artifact is null) { return TypedResults.Ok(new DeploymentArtifactDeleteResponse()); }
        if (!await AllowedAsync(context, authorization, artifact)) { return context.ApiForbidProblem(); }
        var result = await store.DeleteAsync(id, owner);
        return result == ArtifactDeleteResult.Busy
            ? TypedResults.Problem("The artifact is currently in use.", statusCode: 409)
            : TypedResults.Ok(new DeploymentArtifactDeleteResponse { Changed = result == ArtifactDeleteResult.Deleted });
    }

    internal static async Task<IResult> DownloadAsync(HttpContext context, [FromServices] IAuthorizationService authorization,
        [FromServices] DeploymentArtifactStore store, string id)
    {
        var owner = await OwnerAsync(context, authorization);
        if (owner is null) { return context.ApiForbidProblem(); }
        var artifact = await store.FindAsync(id, owner);
        if (artifact is null) { return context.ApiNotFoundProblem(); }
        if (!await AllowedAsync(context, authorization, artifact)) { return context.ApiForbidProblem(); }
        var lease = await store.OpenAsync(id, owner);
        return lease is null ? context.ApiNotFoundProblem() : new ArtifactDownloadResult(lease);
    }

    private static async Task<string> OwnerAsync(HttpContext context, IAuthorizationService authorization) =>
        await authorization.AuthorizeAsync(context.User, RemoteManagementPermissions.AccessRemoteManagement)
            ? DeploymentArtifactOwner.Get(context.User) : null;

    private static Task<bool> AllowedAsync(HttpContext context, IAuthorizationService authorization, DeploymentArtifact artifact) =>
        authorization.AuthorizeAsync(context.User, artifact.Kind == DeploymentArtifactKind.Export ? DeploymentPermissions.Export : DeploymentPermissions.Import);

    private static DeploymentArtifactResponse Describe(DeploymentArtifact artifact) => new()
    {
        Id = artifact.Id, Kind = artifact.Kind.ToString().ToLowerInvariant(), FileName = artifact.FileName,
        ContentType = artifact.ContentType, Length = artifact.Length, Sha256 = artifact.Sha256,
        CreatedUtc = artifact.CreatedUtc, ExpiresUtc = artifact.ExpiresUtc,
    };

    private sealed class ArtifactDownloadResult(DeploymentArtifactLease lease) : IResult
    {
        public async Task ExecuteAsync(HttpContext context)
        {
            using (lease)
            {
                await Results.Stream(lease.Stream, lease.Artifact.ContentType, lease.Artifact.FileName).ExecuteAsync(context);
            }
        }
    }
}

/// <summary>Safe metadata for the caller's tenant-local deployment artifact.</summary>
public sealed class DeploymentArtifactResponse
{
    /// <summary>Gets the opaque artifact ID.</summary>
    public string Id { get; init; }
    /// <summary>Gets the import or export purpose.</summary>
    public string Kind { get; init; }
    /// <summary>Gets the display/download filename.</summary>
    public string FileName { get; init; }
    /// <summary>Gets the package media type.</summary>
    public string ContentType { get; init; }
    /// <summary>Gets the actual byte length.</summary>
    public long Length { get; init; }
    /// <summary>Gets the SHA-256 digest.</summary>
    public string Sha256 { get; init; }
    /// <summary>Gets creation time in UTC.</summary>
    public DateTime CreatedUtc { get; init; }
    /// <summary>Gets expiry time in UTC.</summary>
    public DateTime ExpiresUtc { get; init; }
}

/// <summary>Reports whether artifact deletion changed storage.</summary>
public sealed class DeploymentArtifactDeleteResponse
{
    /// <summary>Gets whether an artifact was removed.</summary>
    public bool Changed { get; init; }
}
