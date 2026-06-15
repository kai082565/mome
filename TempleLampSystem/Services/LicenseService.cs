using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TempleLampSystem.Services;

public static class LicenseService
{
    private static readonly string LicensePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TempleLampSystem", "license.dat");

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetVolumeInformation(
        string lpRootPathName,
        System.Text.StringBuilder? lpVolumeNameBuffer, int nVolumeNameSize,
        out uint lpVolumeSerialNumber, out uint lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        System.Text.StringBuilder? lpFileSystemNameBuffer, int nFileSystemNameSize);

    private static byte[] GetSecretKey()
    {
        var parts = new[] { "鎰翔", "科技", "點燈", "授權", "2025" };
        return SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("-", parts) + "-SYS-KEY"));
    }

    public static string GetMachineId()
    {
        var components = new List<string>();

        try
        {
            uint serial = 0, mcl = 0, flags = 0;
            GetVolumeInformation("C:\\", null, 0, out serial, out mcl, out flags, null, 0);
            if (serial != 0) components.Add(serial.ToString("X8"));
        }
        catch { }

        try
        {
            var mac = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                         && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                         && n.OperationalStatus == OperationalStatus.Up)
                .Select(n => n.GetPhysicalAddress().ToString())
                .FirstOrDefault(m => !string.IsNullOrEmpty(m) && m != "000000000000");
            if (mac != null) components.Add(mac);
        }
        catch { }

        if (components.Count == 0)
            components.Add(Environment.MachineName);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", components)));
        var hex = Convert.ToHexString(hash)[..16].ToUpper();
        return $"{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
    }

    public static string GenerateLicenseKey(string machineId)
    {
        var normalized = machineId.Replace("-", "").ToUpper();
        var hmac = HMACSHA256.HashData(GetSecretKey(), Encoding.UTF8.GetBytes(normalized));
        var hex = Convert.ToHexString(hmac)[..16].ToUpper();
        return $"{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
    }

    public static bool Validate(string machineId, string licenseKey)
    {
        var expected = GenerateLicenseKey(machineId).Replace("-", "");
        var input = licenseKey.Trim().Replace("-", "").ToUpper();
        return string.Equals(expected, input, StringComparison.Ordinal);
    }

    public static bool IsLicensed()
    {
        try
        {
            if (!File.Exists(LicensePath)) return false;
            var data = JsonSerializer.Deserialize<LicenseData>(File.ReadAllText(LicensePath));
            if (data == null || string.IsNullOrEmpty(data.MachineId)) return false;
            var current = GetMachineId();
            return data.MachineId == current && Validate(current, data.LicenseKey);
        }
        catch { return false; }
    }

    public static void SaveLicense(string machineId, string licenseKey)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LicensePath)!);
        var data = new LicenseData { MachineId = machineId, LicenseKey = licenseKey };
        File.WriteAllText(LicensePath, JsonSerializer.Serialize(data));
    }

    private sealed class LicenseData
    {
        public string MachineId { get; set; } = "";
        public string LicenseKey { get; set; } = "";
    }
}
