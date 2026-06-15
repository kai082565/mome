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

public class CertificateFieldPosition
{
    public double X { get; set; }
    public double Y { get; set; }
    public double FontSize { get; set; } = 12;
    public double Rotation { get; set; } = 0;
}

public class CertificateFormSettings
{
    public double PageWidthMm { get; set; } = 0;
    public double PageHeightMm { get; set; } = 0;

    public CertificateFieldPosition Name { get; set; } = new() { X = 0, Y = 0, FontSize = 14 };
    public CertificateFieldPosition CustomerCode { get; set; } = new() { X = 0, Y = 0, FontSize = 12 };
    public CertificateFieldPosition Phone { get; set; } = new() { X = 0, Y = 0, FontSize = 12 };
    public CertificateFieldPosition Address { get; set; } = new() { X = 0, Y = 0, FontSize = 12 };
    public CertificateFieldPosition BirthYear { get; set; } = new() { X = 0, Y = 0, FontSize = 12 };
    public CertificateFieldPosition BirthMonth { get; set; } = new() { X = 0, Y = 0, FontSize = 12 };
    public CertificateFieldPosition BirthDay { get; set; } = new() { X = 0, Y = 0, FontSize = 12 };
    public CertificateFieldPosition BirthHour { get; set; } = new() { X = 0, Y = 0, FontSize = 12 };
    public CertificateFieldPosition PrintDate { get; set; } = new() { X = 0, Y = 0, FontSize = 12 };
    public int RocYear { get; set; } = 115;
    public CertificateFieldPosition LunarStartDate { get; set; } = new() { X = 0, Y = 0, FontSize = 12 };
    public CertificateFieldPosition LunarEndDate { get; set; } = new() { X = 0, Y = 0, FontSize = 12 };
    public CertificateFieldPosition Amount { get; set; } = new() { X = 0, Y = 0, FontSize = 14 };
    public CertificateFieldPosition LampType { get; set; } = new() { X = 0, Y = 0, FontSize = 12 };
    public CertificateFieldPosition OrderNumber { get; set; } = new() { X = 0, Y = 0, FontSize = 11 };
    public CertificateFieldPosition Temple { get; set; } = new() { X = 0, Y = 0, FontSize = 11 };
}

public class LampConfig
{
    public string LampCode { get; set; } = string.Empty;
    public string LampName { get; set; } = string.Empty;
    public string Temple { get; set; } = string.Empty;
    public string Deity { get; set; } = string.Empty;
    public int MaxQuota { get; set; } = 0;
}

public class AppSettings
{
    public SupabaseSettings Supabase { get; set; } = new();
    public PrintSettings Print { get; set; } = new();
    public UpdateSettings Update { get; set; } = new();
    public CertificateFormSettings CertificateForm { get; set; } = new();
    public List<LampConfig> Lamps { get; set; } = new();
    public string DataFolder { get; set; } = "TempleLampSystem";

    public static string AppDataPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Instance.DataFolder);

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

                // 用戶設定檔（%APPDATA%\TempleLampSystem\appsettings.user.json）覆蓋基礎設定
                var userPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TempleLampSystem", "appsettings.user.json");
                if (File.Exists(userPath))
                {
                    try
                    {
                        var userJson = File.ReadAllText(userPath);
                        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var u = JsonSerializer.Deserialize<UserSettingsOverlay>(userJson, opts);
                        if (u != null)
                        {
                            if (!string.IsNullOrEmpty(u.TempleName))    _instance.Print.TempleName    = u.TempleName;
                            if (!string.IsNullOrEmpty(u.TempleAddress)) _instance.Print.TempleAddress = u.TempleAddress;
                            if (!string.IsNullOrEmpty(u.TemplePhone))   _instance.Print.TemplePhone   = u.TemplePhone;
                            if (!string.IsNullOrEmpty(u.SupabaseUrl))   _instance.Supabase.Url        = u.SupabaseUrl;
                            if (!string.IsNullOrEmpty(u.SupabaseAnonKey)) _instance.Supabase.AnonKey  = u.SupabaseAnonKey;
                        }
                    }
                    catch { }
                }
            }
            return _instance;
        }
    }

    public static void Reload() => _instance = null;

    private sealed class UserSettingsOverlay
    {
        public string? TempleName { get; set; }
        public string? TempleAddress { get; set; }
        public string? TemplePhone { get; set; }
        public string? SupabaseUrl { get; set; }
        public string? SupabaseAnonKey { get; set; }
    }
}
