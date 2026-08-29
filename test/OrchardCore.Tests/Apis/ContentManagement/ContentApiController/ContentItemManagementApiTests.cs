using System.Security.Claims;
using OrchardCore.ContentManagement;
using OrchardCore.Contents.Models;
using OrchardCore.Contents.Services;
using OrchardCore.Tests.Apis.Context;

namespace OrchardCore.Tests.Apis.ContentManagement.ContentApiController;

public class ContentItemManagementApiTests
{
    [Fact]
    public async Task ContentService_ListsGetsAndDescribesSchema()
    {
        using var context = new SiteContext();

        await context.InitializeAsync();

        await context.UsingTenantScopeAsync(async scope =>
        {
            var service = scope.ServiceProvider.GetRequiredService<ContentApiService>();
            var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Test")], nameof(StubIdentity)));

            var list = await service.ListAsync(user, "BlogPost", "published", 0, 10);
            Assert.True(list.TotalCount >= 1);

            var contentItemId = Assert.Single(list.Items.Take(1)).ContentItemId;
            var contentItem = await service.GetAsync(contentItemId, "published");
            var schema = await service.GetSchemaAsync("BlogPost");

            Assert.IsType<ContentItem>(contentItem);
            Assert.Equal(contentItemId, contentItem.ContentItemId);
            Assert.Equal("BlogPost", schema.ContentType);
            Assert.Equal("BlogPost", schema.Schema["properties"]?[nameof(ContentItem.ContentType)]?["const"]?.ToString());
        });
    }
}
