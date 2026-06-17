using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NetworkValidationAutomation.Models;

namespace NetworkValidationAutomation.Services;

public class GraphService
{
    private readonly SharePointConfig _sp;
    private readonly EmailConfig _email;
    private readonly HttpClient _httpClient = new();

    public GraphService(SharePointConfig sp, EmailConfig email)
    {
        _sp = sp;
        _email = email;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var secret = !string.IsNullOrWhiteSpace(_sp.ClientSecret)
            ? _sp.ClientSecret
            : Environment.GetEnvironmentVariable(_sp.ClientSecretEnvironmentVariable) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException($"Graph client secret not found. Set appsettings SharePoint:ClientSecret or environment variable {_sp.ClientSecretEnvironmentVariable}.");
        }

        var tokenUrl = $"https://login.microsoftonline.com/{_sp.TenantId}/oauth2/v2.0/token";
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _sp.ClientId,
            ["client_secret"] = secret,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        };

        var response = await _httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(form));
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get Graph token. HTTP {(int)response.StatusCode}: {content}");
        }

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task AuthorizeAsync()
    {
        var token = await GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<string> UploadFileToSharePointAsync(string localFilePath)
    {
        await AuthorizeAsync();

        var siteUrl = $"https://graph.microsoft.com/v1.0/sites/{_sp.SiteHostName}:{_sp.SitePath}";
        var siteResponse = await _httpClient.GetAsync(siteUrl);
        var siteJson = await siteResponse.Content.ReadAsStringAsync();
        if (!siteResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get SharePoint site. HTTP {(int)siteResponse.StatusCode}: {siteJson}");
        }

        using var siteDoc = JsonDocument.Parse(siteJson);
        var siteId = siteDoc.RootElement.GetProperty("id").GetString();

        var drivesResponse = await _httpClient.GetAsync($"https://graph.microsoft.com/v1.0/sites/{siteId}/drives");
        var drivesJson = await drivesResponse.Content.ReadAsStringAsync();
        if (!drivesResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to get SharePoint drives. HTTP {(int)drivesResponse.StatusCode}: {drivesJson}");
        }

        using var drivesDoc = JsonDocument.Parse(drivesJson);
        var drive = drivesDoc.RootElement.GetProperty("value")
            .EnumerateArray()
            .FirstOrDefault(d => string.Equals(d.GetProperty("name").GetString(), _sp.DriveName, StringComparison.OrdinalIgnoreCase));

        if (drive.ValueKind == JsonValueKind.Undefined)
        {
            throw new Exception($"Drive '{_sp.DriveName}' not found in SharePoint site.");
        }

        var driveId = drive.GetProperty("id").GetString();
        var fileName = Path.GetFileName(localFilePath);
        var targetPath = string.IsNullOrWhiteSpace(_sp.FolderPath) ? fileName : $"{_sp.FolderPath.Trim('/')}/{fileName}";
        var uploadUrl = $"https://graph.microsoft.com/v1.0/drives/{driveId}/root:/{targetPath}:/content";

        using var stream = File.OpenRead(localFilePath);
        var uploadResponse = await _httpClient.PutAsync(uploadUrl, new StreamContent(stream));
        var uploadJson = await uploadResponse.Content.ReadAsStringAsync();
        if (!uploadResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to upload file to SharePoint. HTTP {(int)uploadResponse.StatusCode}: {uploadJson}");
        }

        using var uploadDoc = JsonDocument.Parse(uploadJson);
        return uploadDoc.RootElement.GetProperty("webUrl").GetString() ?? string.Empty;
    }

    public async Task SendEmailAsync(string subject, string htmlBody)
    {
        if (!_email.SendEmail) return;
        await AuthorizeAsync();

        object[] ToRecipients(IEnumerable<string> emails) => emails
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => new { emailAddress = new { address = x } })
            .Cast<object>()
            .ToArray();

        var payload = new
        {
            message = new
            {
                subject,
                body = new { contentType = "HTML", content = htmlBody },
                toRecipients = ToRecipients(_email.To),
                ccRecipients = ToRecipients(_email.Cc)
            },
            saveToSentItems = true
        };

        var url = $"https://graph.microsoft.com/v1.0/users/{_email.From}/sendMail";
        var response = await _httpClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to send email. HTTP {(int)response.StatusCode}: {content}");
        }
    }
}
