using ClosedXML.Excel;
using NetworkValidationAutomation.Models;

namespace NetworkValidationAutomation.Services;

public class ExcelReportService
{
    public List<ValidationResult> LoadResultsOrCreatePipelineStatusRecord(ParameterRunResult runResult)
    {
        // Replace this method with a real CSV/JSON parser once the pipeline publishes detailed validation output.
        return new List<ValidationResult>
        {
            new()
            {
                PipelineName = runResult.PipelineName,
                ParameterDisplayName = runResult.ParameterDisplayName,
                ParameterName = runResult.ParameterName,
                RunId = runResult.RunId,
                Target = "Pipeline execution",
                ValidationType = "Pipeline Result",
                Port = string.Empty,
                Status = string.Equals(runResult.Result, "succeeded", StringComparison.OrdinalIgnoreCase) ? "Passed" : "Failed",
                ErrorMessage = runResult.ErrorMessage,
                PipelineUrl = runResult.PipelineUrl,
                CheckedAt = runResult.CompletedAt ?? DateTime.Now
            }
        };
    }

    public ReportSummary GetSummary(List<ParameterRunResult> runs, List<ValidationResult> checks) => new()
    {
        TotalRuns = runs.Count,
        SuccessfulRuns = runs.Count(x => string.Equals(x.Result, "succeeded", StringComparison.OrdinalIgnoreCase)),
        FailedRuns = runs.Count(x => !string.Equals(x.Result, "succeeded", StringComparison.OrdinalIgnoreCase)),
        TotalChecks = checks.Count,
        PassedChecks = checks.Count(x => x.Status.Equals("Passed", StringComparison.OrdinalIgnoreCase) || x.Status.Equals("Success", StringComparison.OrdinalIgnoreCase)),
        FailedChecks = checks.Count(x => x.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase) || x.Status.Equals("Error", StringComparison.OrdinalIgnoreCase))
    };

    public string CreateExcelReport(List<ParameterRunResult> runResults, List<ValidationResult> validationResults, ReportSummary summary, string outputFolder, string filePrefix)
    {
        Directory.CreateDirectory(outputFolder);
        var filePath = Path.Combine(outputFolder, $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

        using var workbook = new XLWorkbook();

        var summarySheet = workbook.Worksheets.Add("Summary");
        summarySheet.Cell(1, 1).Value = "Network Prevalidation Report Summary";
        summarySheet.Cell(3, 1).Value = "Generated At";
        summarySheet.Cell(3, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        summarySheet.Cell(5, 1).Value = "Total Runs";
        summarySheet.Cell(5, 2).Value = summary.TotalRuns;
        summarySheet.Cell(6, 1).Value = "Successful Runs";
        summarySheet.Cell(6, 2).Value = summary.SuccessfulRuns;
        summarySheet.Cell(7, 1).Value = "Failed Runs";
        summarySheet.Cell(7, 2).Value = summary.FailedRuns;
        summarySheet.Cell(9, 1).Value = "Total Checks";
        summarySheet.Cell(9, 2).Value = summary.TotalChecks;
        summarySheet.Cell(10, 1).Value = "Passed Checks";
        summarySheet.Cell(10, 2).Value = summary.PassedChecks;
        summarySheet.Cell(11, 1).Value = "Failed Checks";
        summarySheet.Cell(11, 2).Value = summary.FailedChecks;
        summarySheet.Columns().AdjustToContents();

        var runsSheet = workbook.Worksheets.Add("Pipeline Runs");
        runsSheet.Cell(1, 1).InsertTable(runResults);
        runsSheet.Columns().AdjustToContents();

        var detailsSheet = workbook.Worksheets.Add("Validation Details");
        detailsSheet.Cell(1, 1).InsertTable(validationResults);
        detailsSheet.Columns().AdjustToContents();

        var failed = validationResults.Where(x => x.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase) || x.Status.Equals("Error", StringComparison.OrdinalIgnoreCase)).ToList();
        var failedSheet = workbook.Worksheets.Add("Failed Checks");
        failedSheet.Cell(1, 1).InsertTable(failed.Any() ? failed : new List<ValidationResult>());
        failedSheet.Columns().AdjustToContents();

        workbook.SaveAs(filePath);
        return filePath;
    }
}
