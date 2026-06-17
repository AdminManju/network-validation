using Microsoft.Extensions.Configuration;
using NetworkValidationAutomation.Models;
using NetworkValidationAutomation.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("network-prevalidation-automation.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .Build();

    var config = configuration.Get<AppConfig>() ?? throw new Exception("Unable to load appsettings.json");
    var enabledParameters = config.AzureDevOps.Parameters.Where(x => x.Enabled).ToList();

    if (!enabledParameters.Any())
    {
        throw new Exception("No checkbox parameters are enabled in appsettings.json.");
    }

    var ado = new AzureDevOpsService(config.AzureDevOps);
    var excel = new ExcelReportService();
    var graph = new GraphService(config.SharePoint, config.Email);

    var runResults = new List<ParameterRunResult>();
    var validationResults = new List<ValidationResult>();

    foreach (var parameter in enabledParameters)
    {
        var parameterRunResult = new ParameterRunResult
        {
            PipelineName = config.AzureDevOps.PipelineName,
            PipelineId = config.AzureDevOps.PipelineId,
            ParameterDisplayName = parameter.DisplayName,
            ParameterName = parameter.ParameterName,
            StartedAt = DateTime.Now
        };

        try
        {
            Log.Information("Starting parameter run: {DisplayName} ({ParameterName})", parameter.DisplayName, parameter.ParameterName);

            var run = await ado.TriggerPipelineForParameterAsync(parameter);
            parameterRunResult.RunId = run.Id;
            parameterRunResult.PipelineUrl = run.WebUrl;

            var completedRun = await ado.WaitForCompletionAsync(run.Id);
            parameterRunResult.State = completedRun.State;
            parameterRunResult.Result = completedRun.Result ?? "unknown";
            parameterRunResult.PipelineUrl = completedRun.WebUrl;
            parameterRunResult.CompletedAt = DateTime.Now;

            var artifactPath = await ado.TryDownloadArtifactAsync(completedRun.Id, config.Report.LocalOutputFolder, parameter.DisplayName);
            parameterRunResult.ArtifactPath = artifactPath ?? string.Empty;
        }
        catch (Exception ex)
        {
            parameterRunResult.State = "failed";
            parameterRunResult.Result = "failed";
            parameterRunResult.ErrorMessage = ex.Message;
            parameterRunResult.CompletedAt = DateTime.Now;
            Log.Error(ex, "Parameter run failed for {DisplayName}", parameter.DisplayName);

            if (!config.Execution.ContinueOnParameterFailure)
            {
                runResults.Add(parameterRunResult);
                validationResults.AddRange(excel.LoadResultsOrCreatePipelineStatusRecord(parameterRunResult));
                break;
            }
        }

        runResults.Add(parameterRunResult);
        validationResults.AddRange(excel.LoadResultsOrCreatePipelineStatusRecord(parameterRunResult));
    }

    var summary = excel.GetSummary(runResults, validationResults);
    var reportPath = excel.CreateExcelReport(runResults, validationResults, summary, config.Report.LocalOutputFolder, config.Report.ReportFilePrefix);
    var sharePointLink = await graph.UploadFileToSharePointAsync(reportPath);

    var failedRunHtml = string.Join("", runResults
        .Where(x => !string.Equals(x.Result, "succeeded", StringComparison.OrdinalIgnoreCase))
        .Select(x => $"<li>{x.ParameterDisplayName} - Result: {x.Result} - RunId: {x.RunId} - <a href='{x.PipelineUrl}'>Pipeline Link</a> - Error: {System.Net.WebUtility.HtmlEncode(x.ErrorMessage)}</li>"));

    if (string.IsNullOrWhiteSpace(failedRunHtml))
    {
        failedRunHtml = "<li>No failed pipeline runs.</li>";
    }

    var subjectStatus = summary.FailedRuns > 0 ? "Completed with Failures" : "Success";
    var subject = $"{config.Email.SubjectPrefix} - {subjectStatus} - {DateTime.Now:yyyy-MM-dd}";

    var body = $@"
        <p>Hi Team,</p>
        <p>The automated Network Prevalidation pipeline execution has completed.</p>
        <p><b>Execution Summary:</b></p>
        <ul>
            <li>Total Runs Triggered: {summary.TotalRuns}</li>
            <li>Successful Runs: {summary.SuccessfulRuns}</li>
            <li>Failed Runs: {summary.FailedRuns}</li>
            <li>Total Checks: {summary.TotalChecks}</li>
            <li>Passed Checks: {summary.PassedChecks}</li>
            <li>Failed Checks: {summary.FailedChecks}</li>
        </ul>
        <p><b>Report Link:</b> <a href='{sharePointLink}'>{sharePointLink}</a></p>
        <p><b>Failed Runs:</b></p>
        <ul>{failedRunHtml}</ul>
        <p>Regards,<br/>Automation Team</p>";

    await graph.SendEmailAsync(subject, body);
    Log.Information("Automation completed. ReportPath={ReportPath}, SharePointLink={SharePointLink}", reportPath, sharePointLink);

    if (summary.FailedRuns > 0 && config.Execution.FailApplicationIfAnyRunFails)
    {
        Environment.ExitCode = 1;
    }
}
catch (Exception ex)
{
    Log.Error(ex, "Automation failed");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
