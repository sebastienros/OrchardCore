namespace OrchardCore.Deployment;

/// <summary>Reads and maintains tenant deployment plans for admin, recipes and remote callers.</summary>
public interface IDeploymentPlanService
{
    /// <summary>Gets a tracked plan by its tenant-local identifier.</summary>
    Task<DeploymentPlan> GetAsync(long id);
    /// <summary>Gets a tracked plan using the store's name comparison.</summary>
    Task<DeploymentPlan> FindByNameAsync(string name);
    /// <summary>Lists plans using the existing admin search and stable name/identifier ordering.</summary>
    Task<DeploymentPlanPage> ListAsync(string search, int skip, int take);
    /// <summary>Creates an empty plan, rejecting an empty or already used name.</summary>
    Task<DeploymentPlanManagementResult> CreateAsync(string name);
    /// <summary>Renames a plan without changing its steps; equivalent names do not save.</summary>
    Task<DeploymentPlanManagementResult> RenameAsync(long id, string name);
    /// <summary>Deletes a plan, returning false if it was already absent.</summary>
    Task<bool> DeleteAsync(long id);

    Task<bool> DoesUserHavePermissionsAsync();
    Task<bool> DoesUserHaveExportPermissionAsync();
    Task<IEnumerable<string>> GetAllDeploymentPlanNamesAsync();
    Task<IEnumerable<DeploymentPlan>> GetAllDeploymentPlansAsync();
    Task<IEnumerable<DeploymentPlan>> GetDeploymentPlansAsync(params string[] deploymentPlanNames);
    Task CreateOrUpdateDeploymentPlansAsync(IEnumerable<DeploymentPlan> deploymentPlans);
}
