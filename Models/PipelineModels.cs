namespace NetworkValidationAutomation.Models;

public record PipelineRunInfo(int Id, string State, string? Result, string WebUrl);

public class ParameterRunResult
{
    public string PipelineName { get; set; } = string.Empty;
    public int PipelineId { get; set; }
    public string ParameterDisplayName { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public int? RunId { get; set; }
    public string State { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string PipelineUrl { get; set; } = string.Empty;
    public string ArtifactPath { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
}

public class ValidationResult
{
    public string PipelineName { get; set; } = string.Empty;
    public string ParameterDisplayName { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public int? RunId { get; set; }
    public string Target { get; set; } = string.Empty;
    public string ValidationType { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string PipelineUrl { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; } = DateTime.Now;
}

public class ReportSummary
{
    public int TotalRuns { get; set; }
    public int SuccessfulRuns { get; set; }
    public int FailedRuns { get; set; }
    public int TotalChecks { get; set; }
    public int PassedChecks { get; set; }
    public int FailedChecks { get; set; }
}
