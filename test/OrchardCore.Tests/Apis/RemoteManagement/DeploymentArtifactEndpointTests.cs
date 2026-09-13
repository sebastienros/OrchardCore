using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using OrchardCore.Deployment;
using OrchardCore.Deployment.Artifacts;
using OrchardCore.Deployment.Endpoints.Management;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.RemoteManagement;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace OrchardCore.Tests.Apis.RemoteManagement;

public class DeploymentArtifactEndpointTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedArtifact_RequiresPurposePermission_StreamsBytesAndReleasesLease(bool import)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        using var services = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(() => DateTime.UtcNow);
        var store = new DeploymentArtifactStore(Options.Create(new ShellOptions { ShellsApplicationDataPath = root, ShellsContainerName = "Sites" }),
            new ShellSettings { Name = "Tenant" }, Options.Create(new DeploymentArtifactOptions()), clock.Object);
        try
        {
            var context = new DefaultHttpContext { RequestServices = services, User = Principal("application", "same-subject") };
            context.Request.Method = "GET";
            var owner = DeploymentArtifactOwner.Get(context.User);
            var bytes = Encoding.UTF8.GetBytes("artifact content");
            using var input = new MemoryStream(bytes);
            var artifact = await store.CreateAsync(owner, import ? DeploymentArtifactKind.Import : DeploymentArtifactKind.Export,
                "artifact.zip", "application/zip", input, TestContext.Current.CancellationToken);
            var allowed = Authorize(RemoteManagementPermissions.AccessRemoteManagement, import ? DeploymentPermissions.Import : DeploymentPermissions.Export);
            var denied = Authorize(RemoteManagementPermissions.AccessRemoteManagement, DeploymentPermissions.ManageDeploymentPlan);
            Assert.Equal(403, Status(await DeploymentArtifactEndpoints.GetAsync(context, denied, store, artifact.Id)));
            Assert.Equal(403, Status(await DeploymentArtifactEndpoints.DownloadAsync(context, denied, store, artifact.Id)));
            Assert.Equal(403, Status(await DeploymentArtifactEndpoints.DeleteAsync(context, denied, store, artifact.Id)));
            var metadata = Assert.IsType<Ok<DeploymentArtifactResponse>>(await DeploymentArtifactEndpoints.GetAsync(context, allowed, store, artifact.Id)).Value;
            var json = JsonSerializer.Serialize(metadata);
            Assert.DoesNotContain("same-subject", json);
            Assert.DoesNotContain(root, json);
            Assert.Equal(bytes.Length, metadata.Length);
            context.User = Principal("user", "same-subject");
            Assert.Equal(404, Status(await DeploymentArtifactEndpoints.GetAsync(context, allowed, store, artifact.Id)));
            context.User = Principal("application", "same-subject");
            var download = await DeploymentArtifactEndpoints.DownloadAsync(context, allowed, store, artifact.Id);
            Assert.Equal(409, Status(await DeploymentArtifactEndpoints.DeleteAsync(context, allowed, store, artifact.Id)));
            using var output = new MemoryStream();
            context.Response.Body = output;
            await download.ExecuteAsync(context);
            Assert.Equal(bytes, output.ToArray());
            Assert.True(Assert.IsType<Ok<DeploymentArtifactDeleteResponse>>(await DeploymentArtifactEndpoints.DeleteAsync(context, allowed, store, artifact.Id)).Value.Changed);
            Assert.False(Assert.IsType<Ok<DeploymentArtifactDeleteResponse>>(await DeploymentArtifactEndpoints.DeleteAsync(context, allowed, store, artifact.Id)).Value.Changed);
        }
        finally
        {
            if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
        }
    }

    [Fact]
    public void OwnerIdentity_SeparatesEntityKindsAndIssuers_AndRejectsIncompletePrincipals()
    {
        Assert.NotEqual(DeploymentArtifactOwner.Get(Principal("user", "same")), DeploymentArtifactOwner.Get(Principal("application", "same")));
        var first = Principal("user", "same");
        first.Identities.Single().AddClaim(new Claim("iss", "first"));
        var second = Principal("user", "same");
        second.Identities.Single().AddClaim(new Claim("iss", "second"));
        Assert.NotEqual(DeploymentArtifactOwner.Get(first), DeploymentArtifactOwner.Get(second));
        Assert.Null(DeploymentArtifactOwner.Get(Principal("unknown", "same")));
        Assert.Null(DeploymentArtifactOwner.Get(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "same")]))));
    }

    private static ClaimsPrincipal Principal(string kind, string subject) => new(new ClaimsIdentity(
        [new Claim("oc:entyp", kind), new Claim("sub", subject)], "test"));

    private static int? Status(IResult result) => Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode;

    private static IAuthorizationService Authorize(params Permission[] permissions)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => permissions.Any(permission => permission.Name == requirement.Permission.Name))
                    ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        return authorization.Object;
    }
}
