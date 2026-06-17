namespace NetworkValidationAutomation.Models;

public class AppConfig
{
    public AzureDevOpsConfig AzureDevOps { get; set; } = new();
    public SharePointConfig SharePoint { get; set; } = new();
    public EmailConfig Email { get; set; } = new();
    public ReportConfig Report { get; set; } = new();
    public ExecutionConfig Execution { get; set; } = new();
}

public class AzureDevOpsConfig
{
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public int PipelineId { get; set; }
    public string PipelineName { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public string PersonalAccessToken { get; set; } = string.Empty;
    public string PatEnvironmentVariable { get; set; } = "ADO_PAT";
    public string ArtifactName { get; set; } = "network-validation-results";
    public int PollingIntervalSeconds { get; set; } = 30;
    public int MaxWaitTimeMinutes { get; set; } = 60;
    public string ApiVersion { get; set; } = "7.1";
    public string RunMode { get; set; } = "Sequential";
    public List<CheckboxParameterConfig> Parameters { get; set; } = new();
}

public class CheckboxParameterConfig
{
    public string DisplayName { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

public class SharePointConfig
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ClientSecretEnvironmentVariable { get; set; } = "GRAPH_CLIENT_SECRET";
    public string SiteHostName { get; set; } = string.Empty;
    public string SitePath { get; set; } = string.Empty;
    public string DriveName { get; set; } = "Documents";
    public string FolderPath { get; set; } = string.Empty;
}

public class EmailConfig
{
    public string From { get; set; } = string.Empty;
    public List<string> To { get; set; } = new();
    public List<string> Cc { get; set; } = new();
    public string SubjectPrefix { get; set; } = "Network Prevalidation Report";
    public bool SendEmail { get; set; } = true;
}

public class ReportConfig
{
    public string ReportFilePrefix { get; set; } = "Network_Prevalidation_Report";
    public string LocalOutputFolder { get; set; } = "Reports";
}

public class ExecutionConfig
{
    public bool ContinueOnParameterFailure { get; set; } = true;
    public bool FailApplicationIfAnyRunFails { get; set; } = false;
}
