using Fluid;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Layers.Deployment;
using OrchardCore.Layers.Endpoints.Management;
using OrchardCore.Layers.Drivers;
using OrchardCore.Layers.Handlers;
using OrchardCore.Layers.Indexes;
using OrchardCore.Layers.Models;
using OrchardCore.Layers.Recipes;
using OrchardCore.Layers.Services;
using OrchardCore.Layers.ViewModels;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.RemoteManagement;
using OrchardCore.Scripting;
using OrchardCore.Security.Permissions;

namespace OrchardCore.Layers;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<WidgetWrapper>();
        });

        services.Configure<MvcOptions>((options) =>
        {
            options.Filters.Add<LayerFilter>();
        });

        services.AddSiteDisplayDriver<LayerSiteSettingsDisplayDriver>();
        services.AddContentPart<LayerMetadata>();
        services.AddScoped<IContentDisplayDriver, LayerMetadataWelder>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddScoped<ILayerService, LayerService>();
        services.AddSingleton<IRemoteManagementCapabilityProvider, LayersRemoteManagementCapabilityProvider>();
        services.AddScoped<IContentHandler, LayerMetadataHandler>();
        services.AddIndexProvider<LayerMetadataIndexProvider>();
        services.AddDataMigration<Migrations>();
        services.AddPermissionProvider<Permissions>();
        services.AddRecipeExecutionStep<LayerStep>();
        services.AddDeployment<AllLayersDeploymentSource, AllLayersDeploymentStep, AllLayersDeploymentStepDriver>();
        services.AddSingleton<IGlobalMethodProvider, DefaultLayersMethodProvider>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddLayerManagementEndpoints();
    }
}
