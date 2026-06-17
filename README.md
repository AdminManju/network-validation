# Network Prevalidation Automation - Single Pipeline Sequential Checkbox Runs

This Visual Studio / .NET 8 console application is designed for this exact model:

```text
One Azure DevOps pipeline
Multiple checkbox parameters
Different one-by-one runs
```

Example execution:

```text
Run 1 -> PEP_EIOTP_Dv_SCUS_1 = true, all others false
Run 2 -> PEP_EIOTP_Qa_SCUS_1 = true, all others false
Run 3 -> PEP_EIOTP_Pp_SCUS_1 = true, all others false
Run 4 -> PEP_EIOTP_Pr_SCUS_1 = true, all others false
```

## Pipeline

```text
Network-prevalidation-check
```

## Branch

```text
networks-validation
```

## Configured checkbox parameters

```text
PEP_EIOTP_Dv_SCUS_1
PEP_EIOTP_Qa_SCUS_1
PEP_EIOTP_Pp_SCUS_1
PEP_EIOTP_Pr_SCUS_1
```

## Configure appsettings.json

Update these values:

```json
"Organization": "your-ado-org",
"Project": "your-project",
"PipelineId": 123,
"PipelineName": "Network-prevalidation-check",
"Branch": "networks-validation"
```

Enable/disable parameter runs here:

```json
"Parameters": [
  {
    "DisplayName": "Deploy Dev SCUS",
    "ParameterName": "PEP_EIOTP_Dv_SCUS_1",
    "Enabled": true
  }
]
```

## Configure secrets

Use PowerShell:

```powershell
setx ADO_PAT "<your-azure-devops-pat>"
setx GRAPH_CLIENT_SECRET "<your-graph-client-secret>"
```

Restart Visual Studio after setting environment variables.

## Required permissions

Azure DevOps PAT/service account:

```text
Pipeline read
Queue build / Run pipeline
Build read and execute
Artifact read
Project read
```

Microsoft Graph app registration:

```text
Sites.ReadWrite.All
Files.ReadWrite.All
Mail.Send
```

## Run from Visual Studio

1. Extract this ZIP file.
2. Open `NetworkValidationAutomation.sln`.
3. Update `appsettings.json`.
4. Restore NuGet packages.
5. Set environment variables for secrets.
6. Press `Ctrl + F5`.

## Excel output

The generated Excel report contains:

```text
Summary
Pipeline Runs
Validation Details
Failed Checks
```

## Artifact parsing

Currently, the Excel captures pipeline-level status. If the pipeline publishes detailed CSV/JSON output, update:

```csharp
ExcelReportService.LoadResultsOrCreatePipelineStatusRecord()
```

Recommended detailed output format:

```csv
ParameterDisplayName,ParameterName,Target,ValidationType,Port,Status,ErrorMessage,CheckedAt
```
