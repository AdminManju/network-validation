using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NetworkValidationAutomation.Models;
using Serilog;

namespace NetworkValidationAutomation.Services;

public class AzureDevOpsService
{
    private readonly AzureDevOpsConfig _config;
    private readonly HttpClient _httpClient;

    public AzureDevOpsService(AzureDevOpsConfig config)
    {
        _config = config;
        _httpClient = new HttpClient();

        var pat = !string.IsNullOrWhiteSpace(config.PersonalAccessToken)
            ? config.PersonalAccessToken
            : Environment.GetEnvironmentVariable(config.PatEnvironmentVariable) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(pat))
        {
            throw new InvalidOperationException($"Azure DevOps PAT not found. Set appsettings AzureDevOps:PersonalAccessToken or environment variable {config.PatEnvironmentVariable}.");
        }

        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<PipelineRunInfo> TriggerPipelineForParameterAsync(CheckboxParameterConfig selectedParameter)
    {
        if (_config.PipelineId <= 0)
        {
            throw new InvalidOperationException("PipelineId is not configured. Update appsettings.json.");
        }

        var url = $"https://dev.azure.com/{_config.Organization}/{Uri.EscapeDataString(_config.Project)}/_apis/pipelines/{_config.PipelineId}/runs?api-version={_config.ApiVersion}";

        // Build all checkbox parameters as false and set only the selected parameter as true.
        var templateParameters = _config.Parameters
            .ToDictionary(parameter => parameter.ParameterName, parameter => (object)false);

        templateParameters[selectedParameter.ParameterName] = true;

        var body = new
        {
            resources = new
            {
                repositories = new
                {
                    self = new
                    {
                        refName = $"refs/heads/{_config.Branch}"
                    }
                }
            },
            templateParameters = templateParameters
        };

        var json = JsonSerializer.Serialize(body);
        Log.Information("Triggering pipeline {PipelineName} for parameter {ParameterDisplayName}. Body: {Body}", _config.PipelineName, selectedParameter.DisplayName, json);

        var response = await _httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to trigger pipeline for parameter '{selectedParameter.DisplayName}'. HTTP {(int)response.StatusCode}: {content}");
        }

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var runId = root.GetProperty("id").GetInt32();
        var state = root.TryGetProperty("state", out var st) ? st.GetString() ?? "unknown" : "unknown";
        var webUrl = root.GetProperty("_links").GetProperty("web").GetProperty("href").GetString() ?? string.Empty;

        return new PipelineRunInfo(runId, state, null, webUrl);
    }

    public async Task<PipelineRunInfo> WaitForCompletionAsync(int runId)
    {
        var endTime = DateTime.UtcNow.AddMinutes(_config.MaxWaitTimeMinutes);

        while (DateTime.UtcNow < endTime)
        {
            var run = await GetRunAsync(runId);
            Log.Information("Pipeline={PipelineName}, RunId={RunId}, State={State}, Result={Result}", _config.PipelineName, run.Id, run.State, run.Result);

            if (string.Equals(run.State, "completed", StringComparison.OrdinalIgnoreCase))
            {
                return run;
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.PollingIntervalSeconds));
        }

        throw new TimeoutException($"Pipeline run {runId} did not complete within {_config.MaxWaitTimeMinutes} minutes.");
    }

    public async Task<PipelineRunInfo> GetRunAsync(int runId)
    {
        var url = $"https://dev.azure.com/{_config.Organization}/{Uri.EscapeDataString(_config.Project)}/_apis/pipelines/{_config.PipelineId}/runs/{runId}?api-version={_config.ApiVersion}";
        var response = await _httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get pipeline status. HTTP {(int)response.StatusCode}: {content}");
        }

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var state = root.TryGetProperty("state", out var st) ? st.GetString() ?? "unknown" : "unknown";
        var result = root.TryGetProperty("result", out var rs) ? rs.GetString() : null;
        var webUrl = root.GetProperty("_links").GetProperty("web").GetProperty("href").GetString() ?? string.Empty;

        return new PipelineRunInfo(runId, state, result, webUrl);
    }

    public async Task<string?> TryDownloadArtifactAsync(int runId, string downloadFolder, string parameterDisplayName)
    {
        Directory.CreateDirectory(downloadFolder);

        var url = $"https://dev.azure.com/{_config.Organization}/{Uri.EscapeDataString(_config.Project)}/_apis/build/builds/{runId}/artifacts?artifactName={Uri.EscapeDataString(_config.ArtifactName)}&api-version=7.1";
        var response = await _httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Log.Warning("Artifact not downloaded for RunId={RunId}, Parameter={Parameter}. HTTP {Status}: {Content}", runId, parameterDisplayName, (int)response.StatusCode, content);
            return null;
        }

        using var doc = JsonDocument.Parse(content);
        if (!doc.RootElement.TryGetProperty("resource", out var resource) || !resource.TryGetProperty("downloadUrl", out var dl))
        {
            return null;
        }

        var downloadUrl = dl.GetString();
        if (string.IsNullOrWhiteSpace(downloadUrl)) return null;

        var artifactResponse = await _httpClient.GetAsync(downloadUrl);
        if (!artifactResponse.IsSuccessStatusCode) return null;

        var safeParameter = string.Join("_", parameterDisplayName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
        var zipPath = Path.Combine(downloadFolder, $"{_config.ArtifactName}_{safeParameter}_{runId}.zip");
        await File.WriteAllBytesAsync(zipPath, await artifactResponse.Content.ReadAsByteArrayAsync());

        return zipPath;
    }
}
