using System.IO;
using System.Text.Json;

namespace TempleLampSystem.Services;

public class SupabaseSettings
{
    public string Url { get; set; } = string.Empty;
    public string AnonKey { get; set; } = string.Empty;
}

public class PrintSettings
{
    public string DefaultPrinterName { get; set; } = string.Empty;
    public string TempleName { get; set; } = "○○宮";
    public string TempleAddress { get; set; } = string.Empty;
    public string TemplePhone { get; set; } = string.Empty;
}

public class UpdateSettings
{
    public string CheckUrl { get; set; } = string.Empty;
    public bool AutoCheck { get; set; } = true;
    public int CheckIntervalHours { get; set; } = 24;
}

public class AppSettings
{
    public SupabaseSettings Supabase { get; set; } = new();
    public PrintSettings Print { get; set; } = new();
    public UpdateSettings Update { get; set; } = new();

    private static AppSettings? _instance;

    public static AppSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    _instance = JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
                }
                else
                {
                    _instance = new AppSettings();
                }
            }
            return _instance;
        }
    }
}
