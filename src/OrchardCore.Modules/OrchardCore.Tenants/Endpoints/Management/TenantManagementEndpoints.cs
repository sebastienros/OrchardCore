using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.Environment.Shell.Removing;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.RemoteManagement;
using OrchardCore.Tenants.Controllers;
using OrchardCore.Tenants.Models;
using OrchardCore.Tenants.Services;

namespace OrchardCore.Tenants.Endpoints.Management;

internal static class TenantManagementEndpoints
{
    private const string RoutePrefix = "api/tenants";

    public static IEndpointRouteBuilder AddTenantManagementEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapManagementGet(RoutePrefix, ListAsync)
            .WithName("ApiListTenants")
            .WithSummary("Lists tenants.")
            .WithDescription("Returns tenants that can be managed from the Default tenant, including exact tenant URLs when they can be derived safely.")
            .WithCliCommand(new CliOperationMetadata(["tenants"], "list")
            {
                Capability = TenantManagementApiEndpointConventions.CapabilityName,
                TableColumns =
                {
                    new CliTableColumnMetadata("items.name", "Name"),
                    new CliTableColumnMetadata("items.state", "State"),
                    new CliTableColumnMetadata("items.category", "Category"),
                    new CliTableColumnMetadata("items.primaryUrl", "Url"),
                },
            })
            .Produces<TenantListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        builder.MapManagementGet(RoutePrefix + "/{tenantName}", GetAsync)
            .WithName("ApiGetTenant")
            .WithSummary("Gets a tenant.")
            .WithDescription("Returns a single tenant with redacted connection details and exact URLs when they can be derived safely.")
            .WithCliCommand(new CliOperationMetadata(["tenants"], "show")
            {
                Capability = TenantManagementApiEndpointConventions.CapabilityName,
                Arguments = { new CliArgumentMetadata("tenantName", 0) },
            })
            .Produces<TenantResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        builder.MapManagementPost(RoutePrefix, CreateAsync)
            .WithName("ApiCreateTenantManagement")
            .WithSummary("Creates a tenant.")
            .WithDescription("Creates a tenant shell settings document and returns the created tenant with any setup URL that can be derived safely.")
            .WithCliCommand(new CliOperationMetadata(["tenants"], "create")
            {
                Capability = TenantManagementApiEndpointConventions.CapabilityName,
                InputMode = CliInputMode.Json,
            })
            .Accepts<TenantCreateRequest>("application/json")
            .Produces<TenantResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        builder.MapManagementPut(RoutePrefix + "/{tenantName}", UpdateAsync)
            .WithName("ApiUpdateTenantManagement")
            .WithSummary("Updates a tenant.")
            .WithDescription("Updates mutable tenant settings. Database settings can only be changed while a tenant is uninitialized.")
            .WithCliCommand(new CliOperationMetadata(["tenants"], "update")
            {
                Capability = TenantManagementApiEndpointConventions.CapabilityName,
                Arguments = { new CliArgumentMetadata("tenantName", 0) },
                InputMode = CliInputMode.Json,
            })
            .Accepts<TenantUpdateRequest>("application/json")
            .Produces<TenantResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        builder.MapManagementDelete(RoutePrefix + "/{tenantName}", DeleteAsync)
            .WithName("ApiDeleteTenant")
            .WithSummary("Deletes a tenant.")
            .WithDescription("Deletes a disabled or uninitialized tenant when tenant removal is enabled.")
            .WithCliCommand(new CliOperationMetadata(["tenants"], "delete")
            {
                Capability = TenantManagementApiEndpointConventions.CapabilityName,
                Arguments = { new CliArgumentMetadata("tenantName", 0) },
                RequiresConfirmation = true,
            })
            .Produces<TenantOperationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        builder.MapManagementPost(RoutePrefix + "/{tenantName}:start", StartAsync)
            .WithName("ApiStartTenant")
            .WithSummary("Starts a tenant.")
            .WithDescription("Transitions a disabled tenant back to the running state.")
            .WithCliCommand(new CliOperationMetadata(["tenants"], "start")
            {
                Capability = TenantManagementApiEndpointConventions.CapabilityName,
                Arguments = { new CliArgumentMetadata("tenantName", 0) },
            })
            .Produces<TenantResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        builder.MapManagementPost(RoutePrefix + "/{tenantName}:stop", StopAsync)
            .WithName("ApiStopTenant")
            .WithSummary("Stops a tenant.")
            .WithDescription("Transitions a running tenant to the disabled state.")
            .WithCliCommand(new CliOperationMetadata(["tenants"], "stop")
            {
                Capability = TenantManagementApiEndpointConventions.CapabilityName,
                Arguments = { new CliArgumentMetadata("tenantName", 0) },
                RequiresConfirmation = true,
            })
            .Produces<TenantResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        builder.MapManagementPost(RoutePrefix + "/{tenantName}:enable-remote-management", EnableRemoteManagementAsync)
            .WithName("ApiEnableTenantRemoteManagement")
            .WithSummary("Enables remote management for a tenant.")
            .WithDescription("Enables and configures Remote Management and OpenID Connect in a running tenant so the CLI can register its URL and authenticate directly.")
            .WithCliCommand(new CliOperationMetadata(["tenants"], "enable-remote-management")
            {
                Capability = TenantManagementApiEndpointConventions.CapabilityName,
                Arguments = { new CliArgumentMetadata("tenantName", 0) },
            })
            .Produces<TenantOperationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return builder;
    }

    internal static async Task<IResult> EnableRemoteManagementAsync(
        HttpContext httpContext,
        string tenantName,
        [FromServices] IShellHost shellHost,
        [FromServices] ShellSettings currentShellSettings,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IStringLocalizer<TenantApiController> localizer)
    {
        if (await AuthorizeManageTenantsAsync(httpContext, authorizationService, currentShellSettings, localizer) is { } authError)
        {
            return authError;
        }

        if (!shellHost.TryGetSettings(tenantName, out var settings))
        {
            return httpContext.ApiNotFoundProblem(detail: localizer["Tenant not found: '{0}'.", tenantName]);
        }

        if (!settings.IsRunning())
        {
            return TypedResults.Problem(
                title: localizer["Conflict"],
                detail: localizer["Tenant '{0}' must be running before remote management can be enabled.", tenantName],
                statusCode: StatusCodes.Status409Conflict);
        }

        var scope = await shellHost.GetScopeAsync(settings);
        await scope.UsingAsync(async childScope =>
        {
            var featureManager = childScope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();
            var feature = (await featureManager.GetAvailableFeaturesAsync())
                .FirstOrDefault(feature => feature.Id == "OrchardCore.RemoteManagement");

            if (feature is not null &&
                !(await featureManager.GetEnabledFeaturesAsync()).Any(candidate => candidate.Id == feature.Id))
            {
                _ = await featureManager.EnableFeaturesAsync([feature], force: true);
            }
        });

        var verificationScope = await shellHost.GetScopeAsync(settings);
        var enabled = false;
        await verificationScope.UsingAsync(async childScope =>
        {
            enabled = (await childScope.ServiceProvider.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync())
                .Any(candidate => candidate.Id == "OrchardCore.RemoteManagement");

            if (enabled)
            {
                await childScope.ServiceProvider
                    .GetRequiredService<IRemoteManagementTenantConfigurationService>()
                    .ConfigureAsync();
            }
        });

        if (!enabled)
        {
            return httpContext.ApiBadRequestProblem(detail: localizer["Remote management could not be enabled for tenant '{0}'. Check its active feature profile.", tenantName]);
        }

        await shellHost.ReloadShellContextAsync(settings);

        return TypedResults.Ok(new TenantOperationResponse
        {
            Name = settings.Name,
            Action = "enable-remote-management",
            State = settings.State.ToString(),
            Url = GetTenantUrls(httpContext, settings).FirstOrDefault(),
        });
    }

    internal static async Task<IResult> ListAsync(
        HttpContext httpContext,
        [FromServices] IShellHost shellHost,
        [FromServices] ShellSettings currentShellSettings,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IDataProtectionProvider dataProtectionProvider,
        [FromServices] IClock clock,
        [FromServices] IStringLocalizer<TenantApiController> localizer,
        [AsParameters] TenantListRequest request)
    {
        var skip = request.Skip ?? 0;
        var take = request.Take ?? 20;

        if (ValidatePaging(httpContext, skip, take, localizer) is { } pagingError)
        {
            return pagingError;
        }

        if (await AuthorizeManageTenantsAsync(httpContext, authorizationService, currentShellSettings, localizer) is { } authError)
        {
            return authError;
        }

        var items = shellHost.GetAllSettings()
            .Where(settings => MatchesFilter(settings, request))
            .OrderBy(settings => settings.Name, StringComparer.OrdinalIgnoreCase)
            .Select(settings => CreateTenantResponse(httpContext, settings, dataProtectionProvider, clock))
            .ToArray();

        var pagedItems = items
            .Skip(skip)
            .Take(take)
            .ToArray();

        return TypedResults.Ok(new TenantListResponse
        {
            Skip = skip,
            Take = take,
            TotalCount = items.Length,
            Items = pagedItems,
        });
    }

    internal static async Task<IResult> GetAsync(
        HttpContext httpContext,
        string tenantName,
        [FromServices] IShellHost shellHost,
        [FromServices] ShellSettings currentShellSettings,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IDataProtectionProvider dataProtectionProvider,
        [FromServices] IClock clock,
        [FromServices] IStringLocalizer<TenantApiController> localizer)
    {
        if (await AuthorizeManageTenantsAsync(httpContext, authorizationService, currentShellSettings, localizer) is { } authError)
        {
            return authError;
        }

        if (!shellHost.TryGetSettings(tenantName, out var shellSettings))
        {
            return httpContext.ApiNotFoundProblem(detail: localizer["Tenant not found: '{0}'.", tenantName]);
        }

        return TypedResults.Ok(CreateTenantResponse(httpContext, shellSettings, dataProtectionProvider, clock));
    }

    internal static async Task<IResult> CreateAsync(
        HttpContext httpContext,
        [FromBody] TenantCreateRequest request,
        [FromServices] IShellHost shellHost,
        [FromServices] ShellSettings currentShellSettings,
        [FromServices] IShellSettingsManager shellSettingsManager,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IDataProtectionProvider dataProtectionProvider,
        [FromServices] IClock clock,
        [FromServices] IEnumerable<DatabaseProvider> databaseProviders,
        [FromServices] ITenantValidator tenantValidator,
        [FromServices] TenantDatabasePatternResolver tenantDatabasePatternResolver,
        [FromServices] IStringLocalizer<TenantApiController> localizer,
        [FromServices] ILogger<TenantApiController> logger)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await AuthorizeManageTenantsAsync(httpContext, authorizationService, currentShellSettings, localizer) is { } authError)
        {
            return authError;
        }

        if (shellHost.TryGetSettings(request.Name, out _))
        {
            var modelState = new ModelStateDictionary();
            modelState.AddModelError(nameof(TenantCreateRequest.Name), localizer["A tenant with the same name already exists."]);
            return httpContext.ApiValidationProblem(modelState: modelState);
        }

        var apiModel = request.ToApiModel();
        ApplyConfiguredDatabasePatterns(apiModel, tenantDatabasePatternResolver);
        ApplyPresetDatabaseConfiguration(apiModel, shellSettingsManager, databaseProviders);

        var validationState = new ModelStateDictionary();
        try
        {
            apiModel.IsNewTenant = true;
            validationState.AddModelErrors(await tenantValidator.ValidateAsync(apiModel));
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            logger.LogError(ex, "An error occurred while validating tenant '{TenantName}'.", request.Name);
            validationState.AddModelError(string.Empty, localizer["An error occurred while validating the tenant database settings."]);
        }

        if (!validationState.IsValid)
        {
            return httpContext.ApiValidationProblem(modelState: validationState);
        }

        try
        {
            using var shellSettings = shellSettingsManager.CreateDefaultSettings().AsUninitialized().AsDisposable();
            shellSettings.Name = apiModel.Name;
            shellSettings.RequestUrlHost = apiModel.RequestUrlHost;
            shellSettings.RequestUrlPrefix = apiModel.RequestUrlPrefix;
            shellSettings["Category"] = apiModel.Category;
            shellSettings["Description"] = apiModel.Description;
            shellSettings["ConnectionString"] = apiModel.ConnectionString;
            shellSettings["TablePrefix"] = apiModel.TablePrefix;
            shellSettings["Schema"] = apiModel.Schema;
            shellSettings["DatabaseProvider"] = apiModel.DatabaseProvider;
            shellSettings["Secret"] = Guid.NewGuid().ToString();
            shellSettings["RecipeName"] = apiModel.RecipeName;
            shellSettings["FeatureProfile"] = string.Join(',', apiModel.FeatureProfiles ?? []);

            await shellHost.UpdateShellSettingsAsync(shellSettings);

            var reloadedSettings = shellHost.GetSettings(shellSettings.Name);
            var response = CreateTenantResponse(httpContext, reloadedSettings, dataProtectionProvider, clock);
            return TypedResults.Created($"/{RoutePrefix}/{Uri.EscapeDataString(reloadedSettings.Name)}", response);
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            logger.LogError(ex, "An error occurred while saving tenant '{TenantName}'.", request.Name);
            return httpContext.ApiBadRequestProblem(detail: localizer["An error occurred while saving the tenant settings."]);
        }
    }

    internal static async Task<IResult> UpdateAsync(
        HttpContext httpContext,
        string tenantName,
        [FromBody] TenantUpdateRequest request,
        [FromServices] IShellHost shellHost,
        [FromServices] ShellSettings currentShellSettings,
        [FromServices] IShellSettingsManager shellSettingsManager,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IDataProtectionProvider dataProtectionProvider,
        [FromServices] IClock clock,
        [FromServices] IEnumerable<DatabaseProvider> databaseProviders,
        [FromServices] ITenantValidator tenantValidator,
        [FromServices] TenantDatabasePatternResolver tenantDatabasePatternResolver,
        [FromServices] IStringLocalizer<TenantApiController> localizer,
        [FromServices] ILogger<TenantApiController> logger)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await AuthorizeManageTenantsAsync(httpContext, authorizationService, currentShellSettings, localizer) is { } authError)
        {
            return authError;
        }

        if (!shellHost.TryGetSettings(tenantName, out var shellSettings))
        {
            return httpContext.ApiNotFoundProblem(detail: localizer["Tenant not found: '{0}'.", tenantName]);
        }

        var apiModel = request.ToApiModel(shellSettings.Name);
        apiModel.ConnectionString = TenantConnectionStringRedactor.RestoreIfRedacted(shellSettings["ConnectionString"], apiModel.ConnectionString);

        ApplyConfiguredDatabasePatterns(apiModel, tenantDatabasePatternResolver);
        ApplyPresetDatabaseConfiguration(apiModel, shellSettingsManager, databaseProviders);

        var validationState = new ModelStateDictionary();
        try
        {
            apiModel.IsNewTenant = false;
            validationState.AddModelErrors(await tenantValidator.ValidateAsync(apiModel));
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            logger.LogError(ex, "An error occurred while validating tenant '{TenantName}'.", tenantName);
            validationState.AddModelError(string.Empty, localizer["An error occurred while validating the tenant database settings."]);
        }

        if (!validationState.IsValid)
        {
            return httpContext.ApiValidationProblem(modelState: validationState);
        }

        try
        {
            shellSettings["Description"] = apiModel.Description;
            shellSettings["Category"] = apiModel.Category;
            shellSettings.RequestUrlPrefix = apiModel.RequestUrlPrefix;
            shellSettings.RequestUrlHost = apiModel.RequestUrlHost;
            shellSettings["FeatureProfile"] = string.Join(',', apiModel.FeatureProfiles ?? []);

            if (shellSettings.IsUninitialized())
            {
                shellSettings["DatabaseProvider"] = apiModel.DatabaseProvider;
                shellSettings["TablePrefix"] = apiModel.TablePrefix;
                shellSettings["Schema"] = apiModel.Schema;
                shellSettings["ConnectionString"] = apiModel.ConnectionString;
                shellSettings["RecipeName"] = apiModel.RecipeName;
                shellSettings["Secret"] = shellSettings["Secret"] ?? Guid.NewGuid().ToString();
            }

            await shellHost.UpdateShellSettingsAsync(shellSettings);
            return TypedResults.Ok(CreateTenantResponse(httpContext, shellSettings, dataProtectionProvider, clock));
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            logger.LogError(ex, "An error occurred while saving tenant '{TenantName}'.", tenantName);
            return httpContext.ApiBadRequestProblem(detail: localizer["An error occurred while saving the tenant settings."]);
        }
    }

    internal static async Task<IResult> StartAsync(
        HttpContext httpContext,
        string tenantName,
        [FromServices] IShellHost shellHost,
        [FromServices] ShellSettings currentShellSettings,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IDataProtectionProvider dataProtectionProvider,
        [FromServices] IClock clock,
        [FromServices] IStringLocalizer<TenantApiController> localizer)
    {
        if (await AuthorizeManageTenantsAsync(httpContext, authorizationService, currentShellSettings, localizer) is { } authError)
        {
            return authError;
        }

        if (!shellHost.TryGetSettings(tenantName, out var shellSettings))
        {
            return httpContext.ApiNotFoundProblem(detail: localizer["Tenant not found: '{0}'.", tenantName]);
        }

        if (!shellSettings.IsDisabled())
        {
            return httpContext.ApiBadRequestProblem(detail: localizer["You can only start a disabled tenant."]);
        }

        await shellHost.UpdateShellSettingsAsync(shellSettings.AsRunning());
        return TypedResults.Ok(CreateTenantResponse(httpContext, shellSettings, dataProtectionProvider, clock));
    }

    internal static async Task<IResult> StopAsync(
        HttpContext httpContext,
        string tenantName,
        [FromServices] IShellHost shellHost,
        [FromServices] ShellSettings currentShellSettings,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IDataProtectionProvider dataProtectionProvider,
        [FromServices] IClock clock,
        [FromServices] IStringLocalizer<TenantApiController> localizer)
    {
        if (await AuthorizeManageTenantsAsync(httpContext, authorizationService, currentShellSettings, localizer) is { } authError)
        {
            return authError;
        }

        if (!shellHost.TryGetSettings(tenantName, out var shellSettings))
        {
            return httpContext.ApiNotFoundProblem(detail: localizer["Tenant not found: '{0}'.", tenantName]);
        }

        if (!shellSettings.IsRunning())
        {
            return httpContext.ApiBadRequestProblem(detail: localizer["You can only stop a running tenant."]);
        }

        await shellHost.UpdateShellSettingsAsync(shellSettings.AsDisabled());
        return TypedResults.Ok(CreateTenantResponse(httpContext, shellSettings, dataProtectionProvider, clock));
    }

    internal static async Task<IResult> DeleteAsync(
        HttpContext httpContext,
        string tenantName,
        [FromServices] IShellHost shellHost,
        [FromServices] ShellSettings currentShellSettings,
        [FromServices] IShellRemovalManager shellRemovalManager,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IOptions<TenantsOptions> tenantsOptions,
        [FromServices] IStringLocalizer<TenantApiController> localizer,
        [FromServices] ILogger<TenantApiController> logger)
    {
        if (await AuthorizeManageTenantsAsync(httpContext, authorizationService, currentShellSettings, localizer) is { } authError)
        {
            return authError;
        }

        if (!tenantsOptions.Value.TenantRemovalAllowed)
        {
            return httpContext.ApiForbidProblem();
        }

        if (!shellHost.TryGetSettings(tenantName, out var shellSettings))
        {
            return httpContext.ApiNotFoundProblem(detail: localizer["Tenant not found: '{0}'.", tenantName]);
        }

        if (!shellSettings.IsRemovable())
        {
            return httpContext.ApiBadRequestProblem(detail: localizer["You can only delete a disabled or uninitialized tenant."]);
        }

        var context = await shellRemovalManager.RemoveAsync(shellSettings);
        if (!context.Success)
        {
            return TypedResults.Problem(
                title: localizer["An error occurred while deleting tenant '{0}'.", tenantName],
                detail: context.ErrorMessage,
                statusCode: StatusCodes.Status400BadRequest);
        }

        logger.LogWarning("The tenant '{TenantName}' was removed.", shellSettings.Name);

        return TypedResults.Ok(new TenantOperationResponse
        {
            Name = tenantName,
            Action = "delete",
            State = nameof(TenantState.Disabled),
        });
    }

    internal static TenantResponse CreateTenantResponse(HttpContext httpContext, ShellSettings shellSettings, IDataProtectionProvider dataProtectionProvider, IClock clock)
    {
        var urls = GetTenantUrls(httpContext, shellSettings);
        var setupUrl = shellSettings.IsUninitialized()
            ? BuildSetupUrl(httpContext, shellSettings, dataProtectionProvider, clock)
            : null;

        return new TenantResponse
        {
            Name = shellSettings.Name,
            TenantId = shellSettings.TenantId,
            State = shellSettings.State.ToString(),
            Category = shellSettings["Category"],
            Description = shellSettings["Description"],
            RequestUrlHost = shellSettings.RequestUrlHost,
            RequestUrlPrefix = shellSettings.RequestUrlPrefix,
            DatabaseProvider = shellSettings["DatabaseProvider"],
            ConnectionString = TenantConnectionStringRedactor.RedactPassword(shellSettings["ConnectionString"]),
            TablePrefix = shellSettings["TablePrefix"],
            Schema = shellSettings["Schema"],
            RecipeName = shellSettings["RecipeName"],
            FeatureProfiles = ReadFeatureProfiles(shellSettings),
            Urls = urls,
            PrimaryUrl = urls.FirstOrDefault(),
            SetupUrl = setupUrl,
            CanStart = shellSettings.IsDisabled(),
            CanStop = shellSettings.IsRunning(),
            CanDelete = shellSettings.IsRemovable(),
        };
    }

    private static async Task<IResult> AuthorizeManageTenantsAsync(HttpContext httpContext, IAuthorizationService authorizationService, ShellSettings currentShellSettings, IStringLocalizer<TenantApiController> localizer)
    {
        if (!currentShellSettings.IsDefaultShell())
        {
            return TypedResults.Problem(title: localizer["Forbidden"], detail: localizer["Only the Default tenant can manage tenants."], statusCode: StatusCodes.Status403Forbidden);
        }

        if (!await authorizationService.AuthorizeAsync(httpContext.User, Permissions.ManageTenants))
        {
            return httpContext.ApiForbidProblem();
        }

        return null;
    }

    private static IResult ValidatePaging(HttpContext httpContext, int skip, int take, IStringLocalizer<TenantApiController> localizer)
    {
        if (skip < 0 || take < 1)
        {
            return httpContext.ApiBadRequestProblem(detail: localizer["Skip must be zero or greater and take must be greater than zero."]);
        }

        if (take > 200)
        {
            return httpContext.ApiBadRequestProblem(detail: localizer["Take cannot exceed 200."]);
        }

        return null;
    }

    private static bool MatchesFilter(ShellSettings settings, TenantListRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Search) &&
            !settings.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase) &&
            !(settings["Description"]?.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.Category) &&
            !string.Equals(settings["Category"], request.Category, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.State.HasValue && settings.State != request.State.Value)
        {
            return false;
        }

        return true;
    }

    private static void ApplyConfiguredDatabasePatterns(TenantModelBase model, TenantDatabasePatternResolver tenantDatabasePatternResolver)
    {
        if (!string.IsNullOrWhiteSpace(model.Name))
        {
            tenantDatabasePatternResolver.Apply(model);
        }
    }

    private static void ApplyPresetDatabaseConfiguration(TenantModelBase model, IShellSettingsManager shellSettingsManager, IEnumerable<DatabaseProvider> databaseProviders)
    {
        var providerLookup = databaseProviders.ToDictionary(provider => provider.Value, StringComparer.OrdinalIgnoreCase);
        using var defaultSettings = shellSettingsManager.CreateDefaultSettings().AsDisposable();
        var databaseProvider = defaultSettings["DatabaseProvider"];
        var connectionString = defaultSettings["ConnectionString"];

        if (string.IsNullOrEmpty(databaseProvider) || !providerLookup.TryGetValue(databaseProvider, out var provider))
        {
            return;
        }

        model.DatabaseProvider = databaseProvider;
        if (!provider.HasConnectionString || !string.IsNullOrWhiteSpace(connectionString))
        {
            model.ConnectionString = connectionString;
            model.Schema = defaultSettings["Schema"];
        }
    }

    private static string[] ReadFeatureProfiles(ShellSettings shellSettings)
        => (shellSettings["FeatureProfile"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string[] GetTenantUrls(HttpContext httpContext, ShellSettings shellSettings)
    {
        var hosts = shellSettings.RequestUrlHosts
            .Where(IsConcreteHost)
            .ToList();

        if (hosts.Count == 0 && !string.IsNullOrEmpty(httpContext.Request.Host.Host))
        {
            hosts.Add(httpContext.Request.Host.Value);
        }

        if (hosts.Count == 0)
        {
            return [];
        }

        var pathBase = httpContext.Features.Get<ShellContextFeature>()?.OriginalPathBase ?? PathString.Empty;
        if (!string.IsNullOrEmpty(shellSettings.RequestUrlPrefix))
        {
            pathBase = pathBase.Add('/' + shellSettings.RequestUrlPrefix);
        }

        return hosts
            .Select(host => $"{httpContext.Request.Scheme}://{new HostString(host) + pathBase}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildSetupUrl(HttpContext httpContext, ShellSettings shellSettings, IDataProtectionProvider dataProtectionProvider, IClock clock)
    {
        var primaryUrl = GetTenantUrls(httpContext, shellSettings).FirstOrDefault();
        var secret = shellSettings["Secret"];

        if (string.IsNullOrEmpty(primaryUrl) || string.IsNullOrEmpty(secret))
        {
            return null;
        }

        var dataProtector = dataProtectionProvider.CreateProtector("Tokens").ToTimeLimitedDataProtector();
        var token = dataProtector.Protect(secret, clock.UtcNow.Add(TimeSpan.FromHours(24)));
        return primaryUrl + QueryString.Create("token", token);
    }

    private static bool IsConcreteHost(string host)
        => !string.IsNullOrWhiteSpace(host)
            && !host.Contains('*', StringComparison.Ordinal)
            && !host.Contains('{', StringComparison.Ordinal)
            && !host.Contains('}', StringComparison.Ordinal);

    internal sealed class TenantListRequest
    {
        public int? Skip { get; init; }
        public int? Take { get; init; }
        public string Search { get; init; }
        public string Category { get; init; }
        public TenantState? State { get; init; }
    }

    internal sealed class TenantCreateRequest
    {
        public string Name { get; init; } = string.Empty;
        public string RequestUrlHost { get; init; }
        public string RequestUrlPrefix { get; init; }
        public string Category { get; init; }
        public string Description { get; init; }
        public string DatabaseProvider { get; init; }
        public string ConnectionString { get; init; }
        public string TablePrefix { get; init; }
        public string Schema { get; init; }
        public string RecipeName { get; init; }
        public string[] FeatureProfiles { get; init; } = [];

        public TenantApiModel ToApiModel() => new()
        {
            Name = Name,
            RequestUrlHost = RequestUrlHost,
            RequestUrlPrefix = RequestUrlPrefix,
            Category = Category,
            Description = Description,
            DatabaseProvider = DatabaseProvider,
            ConnectionString = ConnectionString,
            TablePrefix = TablePrefix,
            Schema = Schema,
            RecipeName = RecipeName,
            FeatureProfiles = FeatureProfiles,
        };
    }

    internal sealed class TenantUpdateRequest
    {
        public string RequestUrlHost { get; init; }
        public string RequestUrlPrefix { get; init; }
        public string Category { get; init; }
        public string Description { get; init; }
        public string DatabaseProvider { get; init; }
        public string ConnectionString { get; init; }
        public string TablePrefix { get; init; }
        public string Schema { get; init; }
        public string RecipeName { get; init; }
        public string[] FeatureProfiles { get; init; } = [];

        public TenantApiModel ToApiModel(string name) => new()
        {
            Name = name,
            RequestUrlHost = RequestUrlHost,
            RequestUrlPrefix = RequestUrlPrefix,
            Category = Category,
            Description = Description,
            DatabaseProvider = DatabaseProvider,
            ConnectionString = ConnectionString,
            TablePrefix = TablePrefix,
            Schema = Schema,
            RecipeName = RecipeName,
            FeatureProfiles = FeatureProfiles,
        };
    }

    internal sealed class TenantListResponse
    {
        public int Skip { get; init; }
        public int Take { get; init; }
        public int TotalCount { get; init; }
        public TenantResponse[] Items { get; init; } = [];
    }

    internal sealed class TenantResponse
    {
        public string Name { get; init; } = string.Empty;
        public string TenantId { get; init; }
        public string State { get; init; }
        public string Category { get; init; }
        public string Description { get; init; }
        public string RequestUrlHost { get; init; }
        public string RequestUrlPrefix { get; init; }
        public string DatabaseProvider { get; init; }
        public string ConnectionString { get; init; }
        public string TablePrefix { get; init; }
        public string Schema { get; init; }
        public string RecipeName { get; init; }
        public string[] FeatureProfiles { get; init; } = [];
        public string[] Urls { get; init; } = [];
        public string PrimaryUrl { get; init; }
        public string SetupUrl { get; init; }
        public bool CanStart { get; init; }
        public bool CanStop { get; init; }
        public bool CanDelete { get; init; }
    }

    internal sealed class TenantOperationResponse
    {
        public string Name { get; init; } = string.Empty;
        public string Action { get; init; } = string.Empty;
        public string State { get; init; } = string.Empty;
        public string Url { get; init; }
    }
}

internal static class TenantManagementApiEndpointConventions
{
    public const string CapabilityName = "tenants";
    public const string TagName = "Tenants";

    public static RouteHandlerBuilder MapManagementGet(this IEndpointRouteBuilder builder, string pattern, Delegate handler)
        => builder.MapGet(pattern, handler)
            .WithTags(TagName)
            .DisableAntiforgery()
            .RequireAuthorization(CreateBearerPolicy());

    public static RouteHandlerBuilder MapManagementPost(this IEndpointRouteBuilder builder, string pattern, Delegate handler)
        => builder.MapPost(pattern, handler)
            .WithTags(TagName)
            .DisableAntiforgery()
            .RequireAuthorization(CreateBearerPolicy());

    public static RouteHandlerBuilder MapManagementPut(this IEndpointRouteBuilder builder, string pattern, Delegate handler)
        => builder.MapPut(pattern, handler)
            .WithTags(TagName)
            .DisableAntiforgery()
            .RequireAuthorization(CreateBearerPolicy());

    public static RouteHandlerBuilder MapManagementDelete(this IEndpointRouteBuilder builder, string pattern, Delegate handler)
        => builder.MapDelete(pattern, handler)
            .WithTags(TagName)
            .DisableAntiforgery()
            .RequireAuthorization(CreateBearerPolicy());

    private static Action<AuthorizationPolicyBuilder> CreateBearerPolicy()
        => static policy => policy
            .AddAuthenticationSchemes(OrchardCoreConstants.AuthenticationSchemes.Api)
            .AddRequirements(new OrchardCore.Security.PermissionRequirement(RemoteManagementPermissions.AccessRemoteManagement))
            .RequireAuthenticatedUser();
}
