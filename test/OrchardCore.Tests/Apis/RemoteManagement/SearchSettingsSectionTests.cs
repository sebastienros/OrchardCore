using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Zones;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Localization;
using OrchardCore.Search.Drivers;
using OrchardCore.Search.Models;
using OrchardCore.Search.Services;
using OrchardCore.Search.ViewModels;
using OrchardCore.Settings;

namespace OrchardCore.Tests.Apis.RemoteManagement;

public class SearchSettingsSectionTests
{
    [Theory]
    [InlineData("articles")]
    [InlineData(null)]
    public async Task AdminAndApi_ApplySameSelectionAndText(string selection)
    {
        var profiles = new Mock<IIndexProfileStore>();
        profiles.Setup(value => value.FindByNameAsync("articles")).ReturnsAsync(new IndexProfile { Name = "Articles" });
        var updater = new Mock<IUpdateModel>();
        updater.SetupGet(value => value.ModelState).Returns(new ModelStateDictionary());
        updater.Setup(value => value.TryUpdateModelAsync(It.IsAny<SearchSettingsViewModel>(), It.IsAny<string>()))
            .Callback((SearchSettingsViewModel model, string _) =>
            {
                model.DefaultIndexProfileName = selection; model.PageTitle = "Find articles"; model.Placeholder = "Search here";
            }).ReturnsAsync(true);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(value => value.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        var driver = new SearchSettingsDisplayDriver(new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            authorization.Object, profiles.Object, Localizer<SearchSettingsDisplayDriver>());
        var settings = new SearchSettings { DefaultIndexProfileName = "Old" };
        await driver.UpdateAsync(new SiteSettings(), settings, new UpdateEditorContext(new Shape(), "search", false, "",
            Mock.Of<IShapeFactory>(), Mock.Of<IZoneHolding>(), updater.Object));
        Assert.True(updater.Object.ModelState.IsValid);
        var site = new SiteSettings { SiteName = "Keep" };
        site.Put(nameof(SearchSettings), new SearchSettings { DefaultIndexProfileName = "Old" });
        var (provider, service) = Create(site, profiles.Object);
        var patch = new JsonObject { ["defaultIndexProfileName"] = selection, ["pageTitle"] = "Find articles", ["placeholder"] = "Search here" };

        var result = await provider.UpdateAsync(patch);

        Assert.Empty(result.Errors);
        Assert.True(result.Changed);
        Assert.Equal(settings.DefaultIndexProfileName, result.Section.Values["defaultIndexProfileName"]?.GetValue<string>());
        Assert.Equal(settings.PageTitle, result.Section.Values["pageTitle"].GetValue<string>());
        Assert.Equal(settings.Placeholder, result.Section.Values["placeholder"].GetValue<string>());
        Assert.False((await provider.UpdateAsync(patch)).Changed);
        Assert.Equal("Keep", site.SiteName);
        service.Verify(value => value.UpdateSiteSettingsAsync(site), Times.Once());
    }

    [Theory]
    [InlineData("{\"pageTitle\":true}")]
    [InlineData("{\"providerName\":\"Lucene\"}")]
    [InlineData("{\"defaultIndexProfileName\":\"missing\",\"pageTitle\":\"Changed\"}")]
    public async Task InvalidPatch_DoesNotSaveOrPartiallyChangeSettings(string json)
    {
        var site = new SiteSettings();
        site.Put(nameof(SearchSettings), new SearchSettings { DefaultIndexProfileName = "Old", PageTitle = "Original" });
        var (provider, service) = Create(site, Mock.Of<IIndexProfileStore>());

        var result = await provider.UpdateAsync(JsonNode.Parse(json).AsObject());

        Assert.NotEmpty(result.Errors);
        Assert.Equal("Original", site.As<SearchSettings>().PageTitle);
        Assert.Equal("Old", site.As<SearchSettings>().DefaultIndexProfileName);
        service.Verify(value => value.UpdateSiteSettingsAsync(It.IsAny<ISite>()), Times.Never());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("")]
    public async Task OmittedDefault_PreservesStaleOrEmptyReference(string existing)
    {
        var site = new SiteSettings();
        site.Put(nameof(SearchSettings), new SearchSettings { DefaultIndexProfileName = existing, Placeholder = "Keep" });
        var profiles = new Mock<IIndexProfileStore>(MockBehavior.Strict);
        var (provider, service) = Create(site, profiles.Object);
        Assert.False((await provider.UpdateAsync([])).Changed);
        var result = await provider.UpdateAsync(new JsonObject { ["pageTitle"] = "Changed" });
        Assert.Equal(existing, result.Section.Values["defaultIndexProfileName"].GetValue<string>());
        Assert.Equal("Keep", result.Section.Values["placeholder"].GetValue<string>());
        profiles.VerifyNoOtherCalls();
        service.Verify(value => value.UpdateSiteSettingsAsync(site), Times.Once());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminPartialSubmission_PreservesOmittedFieldsAndRejectsInvalidSelection(bool invalidSelection)
    {
        var profiles = new Mock<IIndexProfileStore>();
        var updater = new Mock<IUpdateModel>();
        updater.SetupGet(value => value.ModelState).Returns(new ModelStateDictionary());
        updater.Setup(value => value.TryUpdateModelAsync(It.IsAny<SearchSettingsViewModel>(), It.IsAny<string>()))
            .Callback((SearchSettingsViewModel model, string _) =>
            {
                model.PageTitle = "Changed";
                if (invalidSelection) { model.DefaultIndexProfileName = "missing"; }
            }).ReturnsAsync(true);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(value => value.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        var driver = new SearchSettingsDisplayDriver(new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            authorization.Object, profiles.Object, Localizer<SearchSettingsDisplayDriver>());
        var settings = new SearchSettings { DefaultIndexProfileName = "Old", Placeholder = "Keep", PageTitle = "Original" };

        await driver.UpdateAsync(new SiteSettings(), settings, new UpdateEditorContext(new Shape(), "search", false, "",
            Mock.Of<IShapeFactory>(), Mock.Of<IZoneHolding>(), updater.Object));

        Assert.Equal(!invalidSelection, updater.Object.ModelState.IsValid);
        Assert.Equal("Old", settings.DefaultIndexProfileName);
        Assert.Equal("Keep", settings.Placeholder);
        Assert.Equal(invalidSelection ? "Original" : "Changed", settings.PageTitle);
    }

    private static StringLocalizer<T> Localizer<T>() => new(new NullStringLocalizerFactory());

    private static (SearchSettingsSectionProvider, Mock<ISiteService>) Create(SiteSettings site, IIndexProfileStore profiles)
    {
        var service = new Mock<ISiteService>();
        service.Setup(value => value.LoadSiteSettingsAsync()).ReturnsAsync(site);
        service.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site);
        return (new SearchSettingsSectionProvider(service.Object, profiles, Localizer<SearchSettingsSectionProvider>()), service);
    }
}
