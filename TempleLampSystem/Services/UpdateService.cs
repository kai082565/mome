using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace TempleLampSystem.Services;

public class UpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    private static readonly Version CurrentVersion = new("1.0.0");

    public UpdateService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        var settings = AppSettings.Instance.Update;
        if (string.IsNullOrEmpty(settings.CheckUrl) || settings.CheckUrl.Contains("your-server"))
            return null;

        try
        {
            var response = await _httpClient.GetStringAsync(settings.CheckUrl);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(response, options);

            if (updateInfo != null)
            {
                var latestVersion = new Version(updateInfo.Version);
                if (latestVersion > CurrentVersion)
                {
                    return updateInfo;
                }
            }
        }
        catch
        {
            // 檢查更新失敗，靜默忽略
        }

        return null;
    }

    public async Task<bool> DownloadAndInstallAsync(UpdateInfo updateInfo)
    {
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "TempleLampSystem_Update.exe");

            var response = await _httpClient.GetAsync(updateInfo.DownloadUrl);
            response.EnsureSuccessStatusCode();

            await using var fs = new FileStream(tempPath, FileMode.Create);
            await response.Content.CopyToAsync(fs);

            Process.Start(new ProcessStartInfo
            {
                FileName = tempPath,
                Arguments = "/SILENT",
                UseShellExecute = true
            });

            System.Windows.Application.Current.Shutdown();

            return true;
        }
        catch
        {
            return false;
        }
    }
}
