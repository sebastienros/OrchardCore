using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.StaticFiles;
using OrchardCore.FileStorage;
using OrchardCore.FileStorage.FileSystem;
using OrchardCore.Media;
using OrchardCore.Media.Core;
using OrchardCore.Media.Endpoints.Api;
using OrchardCore.Media.Events;
using OrchardCore.Media.Services;
using OrchardCore.Media.ViewModels;

namespace OrchardCore.Tests.Apis.RemoteManagement;

public class MediaMutationIdempotencyTests
{
    [Fact]
    public async Task DeleteEndpoints_RepeatedAndPartialBatchRequests_Succeed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var testApp = await MediaTestApp.CreateAsync();
        await testApp.CreateFileAsync("delete/a.txt", "a");
        await testApp.CreateFileAsync("delete/b.txt", "b");

        var singleResponse = await testApp.Client.DeleteAsync("api/media/file?path=delete/a.txt", cancellationToken);
        var repeatedSingleResponse = await testApp.Client.DeleteAsync("api/media/file?path=delete/a.txt", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, singleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatedSingleResponse.StatusCode);

        var request = new DeleteMediaListRequest
        {
            Paths = ["delete/a.txt", "delete/b.txt"],
        };

        var batchResponse = await testApp.Client.PostAsJsonAsync("api/media/files:delete", request, cancellationToken);
        var repeatedBatchResponse = await testApp.Client.PostAsJsonAsync("api/media/files:delete", request, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, batchResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatedBatchResponse.StatusCode);
        Assert.Equal(
            await batchResponse.Content.ReadAsStringAsync(cancellationToken),
            await repeatedBatchResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Null(await testApp.Store.GetFileInfoAsync("delete/b.txt"));
    }

    [Fact]
    public async Task MoveEndpoints_RepeatedAndPartialBatchRequests_Succeed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var testApp = await MediaTestApp.CreateAsync();
        await testApp.CreateFileAsync("source/a.txt", "a");
        await testApp.CreateFileAsync("source/b.txt", "b");
        await testApp.Store.TryCreateDirectoryAsync("target");

        var singleRequest = new MoveMediaRequest
        {
            OldPath = "source/a.txt",
            NewPath = "target/a.txt",
        };

        var singleResponse = await testApp.Client.PostAsJsonAsync("api/media/files:move", singleRequest, cancellationToken);
        var repeatedSingleResponse = await testApp.Client.PostAsJsonAsync("api/media/files:move", singleRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, singleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatedSingleResponse.StatusCode);

        var batchRequest = new MoveMediaBatchRequest
        {
            MediaNames = ["a.txt", "b.txt"],
            SourceFolder = "source",
            TargetFolder = "target",
        };

        var batchResponse = await testApp.Client.PostAsJsonAsync("api/media/files:move-batch", batchRequest, cancellationToken);
        var repeatedBatchResponse = await testApp.Client.PostAsJsonAsync("api/media/files:move-batch", batchRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, batchResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatedBatchResponse.StatusCode);
        Assert.Equal(
            await batchResponse.Content.ReadAsStringAsync(cancellationToken),
            await repeatedBatchResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Null(await testApp.Store.GetFileInfoAsync("source/b.txt"));
        Assert.NotNull(await testApp.Store.GetFileInfoAsync("target/b.txt"));

        batchRequest.MediaNames = ["missing.txt"];
        var missingResponse = await testApp.Client.PostAsJsonAsync("api/media/files:move-batch", batchRequest, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);
    }

    [Fact]
    public async Task MoveEndpoint_SourceAndTargetExist_ReturnsConflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var testApp = await MediaTestApp.CreateAsync();
        await testApp.CreateFileAsync("source/file.txt", "source");
        await testApp.CreateFileAsync("target/file.txt", "target");

        var response = await testApp.Client.PostAsJsonAsync("api/media/files:move", new MoveMediaRequest
        {
            OldPath = "source/file.txt",
            NewPath = "target/file.txt",
        }, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("source", await testApp.ReadFileAsync("source/file.txt"));
        Assert.Equal("target", await testApp.ReadFileAsync("target/file.txt"));
    }

    [Fact]
    public async Task CopyEndpoint_OnlyAcceptsAnExistingIdenticalTargetAsCompleted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var testApp = await MediaTestApp.CreateAsync();
        await testApp.CreateFileAsync("source/file.txt", "content");
        await testApp.Store.TryCreateDirectoryAsync("target");

        var request = new CopyMediaRequest
        {
            OldPath = "source/file.txt",
            NewPath = "target/file.txt",
        };

        var response = await testApp.Client.PostAsJsonAsync("api/media/files:copy", request, cancellationToken);
        var repeatedResponse = await testApp.Client.PostAsJsonAsync("api/media/files:copy", request, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);

        await testApp.CreateFileAsync("target/conflict.txt", "different");
        request.NewPath = "target/conflict.txt";

        var conflictResponse = await testApp.Client.PostAsJsonAsync("api/media/files:copy", request, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, conflictResponse.StatusCode);
        Assert.Equal("different", await testApp.ReadFileAsync("target/conflict.txt"));
    }

    [Fact]
    public async Task FolderEndpoints_RepeatedRequests_Succeed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var testApp = await MediaTestApp.CreateAsync();
        var request = new CreateFolderRequest
        {
            Path = "parent",
            Name = "child",
        };

        var response = await testApp.Client.PostAsJsonAsync("api/media/folders", request, cancellationToken);
        var repeatedResponse = await testApp.Client.PostAsJsonAsync("api/media/folders", request, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);

        await testApp.CreateFileAsync("parent/conflict.txt", "content");
        request.Name = "conflict.txt";
        var conflictResponse = await testApp.Client.PostAsJsonAsync("api/media/folders", request, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, conflictResponse.StatusCode);

        var deleteResponse = await testApp.Client.DeleteAsync("api/media/folder?path=parent/child", cancellationToken);
        var repeatedDeleteResponse = await testApp.Client.DeleteAsync("api/media/folder?path=parent/child", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatedDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task UploadEndpoint_RepeatedAndReplacementRequests_Converge()
    {
        await using var testApp = await MediaTestApp.CreateAsync(maxFileSize: 16);

        Assert.Equal(HttpStatusCode.OK, (await testApp.UploadAsync("first")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await testApp.UploadAsync("first")).StatusCode);
        Assert.Equal("first", await testApp.ReadFileAsync("uploads/file.txt"));

        Assert.Equal(HttpStatusCode.OK, (await testApp.UploadAsync("replacement")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await testApp.UploadAsync("replacement")).StatusCode);
        Assert.Equal("replacement", await testApp.ReadFileAsync("uploads/file.txt"));

        var oversizedResponse = await testApp.UploadAsync(new string('x', 17));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversizedResponse.StatusCode);
        Assert.Equal("replacement", await testApp.ReadFileAsync("uploads/file.txt"));
    }

    private sealed class MediaTestApp : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly string _directory;

        private MediaTestApp(WebApplication app, HttpClient client, IMediaFileStore store, string directory)
        {
            _app = app;
            Client = client;
            Store = store;
            _directory = directory;
        }

        public HttpClient Client { get; }

        public IMediaFileStore Store { get; }

        public static async Task<MediaTestApp> CreateAsync(long maxFileSize = 1024)
        {
            var directory = Path.Combine(Path.GetTempPath(), "OrchardCore.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddAuthorization();
            builder.Services.AddLocalization();
            builder.Services.AddHttpContextAccessor();

            var authorizationService = new Mock<IAuthorizationService>();
            authorizationService
                .Setup(service => service.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(AuthorizationResult.Success());
            builder.Services.AddSingleton(authorizationService.Object);

            var authenticationService = new Mock<IAuthenticationService>();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test"));
            authenticationService
                .Setup(service => service.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string>()))
                .ReturnsAsync(AuthenticateResult.Success(
                    new AuthenticationTicket(principal, "Test")));
            builder.Services.AddSingleton(authenticationService.Object);

            var fileStore = new FileSystemStore(directory, NullLogger<FileSystemStore>.Instance);
            var mediaFileStore = new DefaultMediaFileStore(
                fileStore,
                "/media",
                string.Empty,
                [],
                [],
                NullLogger<DefaultMediaFileStore>.Instance);

            builder.Services.AddSingleton<IMediaFileStore>(mediaFileStore);
            builder.Services.AddSingleton<IMediaNameNormalizerService, NullMediaNameNormalizerService>();
            builder.Services.AddSingleton<IContentTypeProvider, FileExtensionContentTypeProvider>();
            builder.Services.AddSingleton(Mock.Of<IFileVersionProvider>(
                provider => provider.AddFileVersionToPath(It.IsAny<PathString>(), It.IsAny<string>()) == string.Empty));
            builder.Services.AddSingleton<FileCreationService>();
            builder.Services.AddSingleton(Mock.Of<IUserAssetFolderNameProvider>());
            builder.Services.AddSingleton<AttachedMediaFieldFileService>();
            builder.Services.AddSingleton<MediaDirectoryTreeCache>();
            builder.Services.Configure<MediaOptions>(options =>
            {
                options.AllowedFileExtensions = [".txt"];
                options.AssetsUsersFolder = "users";
                options.MaxFileSize = maxFileSize;
            });

            var app = builder.Build();
            app.AddCopyMediaEndpoint()
                .AddCreateFolderEndpoint()
                .AddDeleteFolderEndpoint()
                .AddDeleteMediaEndpoint()
                .AddDeleteMediaListEndpoint()
                .AddMoveMediaEndpoint()
                .AddMoveMediaListEndpoint()
                .AddUploadMediaEndpoint();

            await app.StartAsync();

            return new MediaTestApp(app, app.GetTestClient(), mediaFileStore, directory);
        }

        public async Task CreateFileAsync(string path, string content)
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            await Store.CreateFileFromStreamAsync(path, stream, overwrite: true);
        }

        public async Task<string> ReadFileAsync(string path)
        {
            await using var stream = await Store.GetFileStreamAsync(path);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        public Task<HttpResponseMessage> UploadAsync(string content)
        {
            var body = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
            body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return Client.PutAsync("api/media/files/content?path=uploads&fileName=file.txt", body);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync();
            Directory.Delete(_directory, recursive: true);
        }
    }
}
