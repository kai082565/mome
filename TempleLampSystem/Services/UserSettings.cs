using System.IO;
using System.Text.Json;

namespace TempleLampSystem.Services;

public class UserSettings
{
    public string TempleName { get; set; } = "";
    public string TempleAddress { get; set; } = "";
    public string TemplePhone { get; set; } = "";
    public string SupabaseUrl { get; set; } = "";
    public string SupabaseAnonKey { get; set; } = "";

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TempleLampSystem", "appsettings.user.json");

    public static bool FileExists() => File.Exists(FilePath);

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static UserSettings? Load()
    {
        if (!FileExists()) return null;
        try
        {
            return JsonSerializer.Deserialize<UserSettings>(
                File.ReadAllText(FilePath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }
}
