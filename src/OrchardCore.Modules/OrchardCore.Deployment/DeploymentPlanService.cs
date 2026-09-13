using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.Deployment.Indexes;
using YesSql;
using YesSql.Services;

namespace OrchardCore.Deployment;

public class DeploymentPlanService : IDeploymentPlanService
{
    private readonly YesSql.ISession _session;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private Dictionary<string, DeploymentPlan> _deploymentPlans;

    public DeploymentPlanService(
        YesSql.ISession session,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _session = session;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc />
    public Task<DeploymentPlan> GetAsync(long id) => _session.GetAsync<DeploymentPlan>(id);

    /// <inheritdoc />
    public Task<DeploymentPlan> FindByNameAsync(string name) =>
        _session.Query<DeploymentPlan, DeploymentPlanIndex>(index => index.Name == name).FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<DeploymentPlanPage> ListAsync(string search, int skip, int take)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegative(take);
        var query = _session.Query<DeploymentPlan, DeploymentPlanIndex>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(index => index.Name.Contains(search));
        }

        var count = await query.CountAsync();
        var items = await query.OrderBy(index => index.Name).ThenBy(index => index.DocumentId)
            .Skip(skip).Take(take).ListAsync();
        return new DeploymentPlanPage { TotalCount = count, Items = items.ToArray() };
    }

    /// <inheritdoc />
    public async Task<DeploymentPlanManagementResult> CreateAsync(string name)
    {
        var error = await ValidateNameAsync(name, 0);
        if (error != DeploymentPlanManagementError.None)
        {
            return new() { Error = error };
        }

        var plan = new DeploymentPlan { Name = name };
        await SaveAsync(plan);
        return new() { Plan = plan, Changed = true };
    }

    /// <inheritdoc />
    public async Task<DeploymentPlanManagementResult> RenameAsync(long id, string name)
    {
        var plan = await GetAsync(id);
        if (plan is null)
        {
            return new() { Error = DeploymentPlanManagementError.NotFound };
        }
        if (plan.Name == name)
        {
            return new() { Plan = plan };
        }

        var error = await ValidateNameAsync(name, id);
        if (error != DeploymentPlanManagementError.None)
        {
            return new() { Error = error };
        }

        plan.Name = name;
        await SaveAsync(plan);
        return new() { Plan = plan, Changed = true };
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(long id)
    {
        var plan = await GetAsync(id);
        if (plan is null)
        {
            return false;
        }
        _session.Delete(plan);
        _deploymentPlans = null;
        return true;
    }

    private async Task<DeploymentPlanManagementError> ValidateNameAsync(string name, long id)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return DeploymentPlanManagementError.MissingName;
        }
        var count = await _session.QueryIndex<DeploymentPlanIndex>(index => index.Name == name && index.DocumentId != id).CountAsync();
        return count > 0 ? DeploymentPlanManagementError.DuplicateName : DeploymentPlanManagementError.None;
    }

    private async Task SaveAsync(DeploymentPlan plan)
    {
        await _session.SaveAsync(plan);
        _deploymentPlans = null;
    }

    private async Task<Dictionary<string, DeploymentPlan>> GetDeploymentPlans()
    {
        if (_deploymentPlans == null)
        {
            var deploymentPlanQuery = _session.Query<DeploymentPlan, DeploymentPlanIndex>();
            var deploymentPlans = await deploymentPlanQuery.ListAsync();
            _deploymentPlans = deploymentPlans.ToDictionary(x => x.Name);
        }

        return _deploymentPlans;
    }

    public async Task<bool> DoesUserHavePermissionsAsync()
    {
        var user = _httpContextAccessor.HttpContext.User;

        var result = await _authorizationService.AuthorizeAsync(user, DeploymentPermissions.ManageDeploymentPlan) &&
                     await _authorizationService.AuthorizeAsync(user, DeploymentPermissions.Export);

        return result;
    }

    public async Task<bool> DoesUserHaveExportPermissionAsync()
    {
        var user = _httpContextAccessor.HttpContext.User;

        var result = await _authorizationService.AuthorizeAsync(user, DeploymentPermissions.Export);

        return result;
    }

    public async Task<IEnumerable<string>> GetAllDeploymentPlanNamesAsync()
    {
        var deploymentPlans = await GetDeploymentPlans();

        return deploymentPlans.Keys;
    }

    public async Task<IEnumerable<DeploymentPlan>> GetAllDeploymentPlansAsync()
    {
        var deploymentPlans = await GetDeploymentPlans();

        return deploymentPlans.Values;
    }

    public async Task<IEnumerable<DeploymentPlan>> GetDeploymentPlansAsync(params string[] deploymentPlanNames)
    {
        var deploymentPlans = await GetDeploymentPlans();

        return GetDeploymentPlans(deploymentPlans, deploymentPlanNames);
    }

    private static IEnumerable<DeploymentPlan> GetDeploymentPlans(Dictionary<string, DeploymentPlan> deploymentPlans, params string[] deploymentPlanNames)
    {
        foreach (var deploymentPlanName in deploymentPlanNames)
        {
            if (deploymentPlans.TryGetValue(deploymentPlanName, out var deploymentPlan))
            {
                yield return deploymentPlan;
            }
        }
    }

    /// <summary>
    /// Creates or replaces plans while preserving steps when a caller passes a tracked plan.
    /// Invalidates request-local discovery after writes so subsequent callers see the changes.
    /// </summary>
    /// <param name="deploymentPlans">The plans whose names and steps should be saved.</param>
    public async Task CreateOrUpdateDeploymentPlansAsync(IEnumerable<DeploymentPlan> deploymentPlans)
    {
        var plans = deploymentPlans.ToArray();
        var names = plans.Select(x => x.Name);

        var existingDeploymentPlans = (await _session.Query<DeploymentPlan, DeploymentPlanIndex>(x => x.Name.IsIn(names))
            .ListAsync())
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var deploymentPlan in plans)
        {
            if (existingDeploymentPlans.TryGetValue(deploymentPlan.Name, out var existingDeploymentPlan))
            {
                var steps = deploymentPlan.DeploymentSteps.ToArray();
                existingDeploymentPlan.Name = deploymentPlan.Name;
                existingDeploymentPlan.DeploymentSteps.Clear();
                existingDeploymentPlan.DeploymentSteps.AddRange(steps);

                await SaveAsync(existingDeploymentPlan);
            }
            else
            {
                await SaveAsync(deploymentPlan);
            }
        }

        _deploymentPlans = null;
    }
}
