using System.IO;
using System.Text.Json;
using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public class SessionService
{
    private static readonly string SessionPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TempleLampSystem",
        "session.json");

    public Staff? CurrentStaff { get; private set; }
    public bool IsLoggedIn => CurrentStaff != null;
    public bool IsAdmin => CurrentStaff?.Role == StaffRole.Admin;

    public void Login(Staff staff)
    {
        CurrentStaff = staff;
        SaveSession(staff);
    }

    public void Logout()
    {
        CurrentStaff = null;
        DeleteSession();
    }

    /// <summary>
    /// 嘗試從已儲存的 session.json 自動登入，回傳儲存的 StaffId（需外部驗證帳號仍有效）
    /// </summary>
    public string? TryLoadSavedStaffId()
    {
        try
        {
            if (!File.Exists(SessionPath)) return null;
            var json = File.ReadAllText(SessionPath);
            var data = JsonSerializer.Deserialize<SessionData>(json);
            return data?.StaffId;
        }
        catch
        {
            return null;
        }
    }

    private void SaveSession(Staff staff)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)!);
            var data = new SessionData { StaffId = staff.Id, StaffName = staff.Name };
            File.WriteAllText(SessionPath, JsonSerializer.Serialize(data));
        }
        catch
        {
            // 儲存 session 失敗不影響登入
        }
    }

    private void DeleteSession()
    {
        try
        {
            if (File.Exists(SessionPath))
                File.Delete(SessionPath);
        }
        catch { }
    }

    private class SessionData
    {
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
    }
}
