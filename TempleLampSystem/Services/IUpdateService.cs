namespace TempleLampSystem.Services;

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync();
    Task<bool> DownloadAndInstallAsync(UpdateInfo updateInfo);
}

public class UpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
}
