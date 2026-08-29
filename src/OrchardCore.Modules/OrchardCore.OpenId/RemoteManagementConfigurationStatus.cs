namespace OrchardCore.OpenId;

public sealed class RemoteManagementConfigurationStatus
{
    public bool ServerEndpointsConfigured { get; init; }
    public bool AuthorizationCodeFlowConfigured { get; init; }
    public bool ClientCredentialsFlowConfigured { get; init; }
    public bool DeviceFlowConfigured { get; init; }
    public bool RefreshFlowConfigured { get; init; }
    public bool ProofKeyForCodeExchangeRequired { get; init; }
    public bool ValidationConfigured { get; init; }
    public bool ManagementScopeConfigured { get; init; }
    public bool CliApplicationConfigured { get; init; }

    public bool IsReady =>
        ServerEndpointsConfigured &&
        AuthorizationCodeFlowConfigured &&
        ClientCredentialsFlowConfigured &&
        DeviceFlowConfigured &&
        RefreshFlowConfigured &&
        ProofKeyForCodeExchangeRequired &&
        ValidationConfigured &&
        ManagementScopeConfigured &&
        CliApplicationConfigured;
}
