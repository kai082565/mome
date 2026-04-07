using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public class StaffService : IStaffService
{
    private readonly AppDbContext _context;

    public StaffService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Staff>> GetAllAsync()
    {
        return await _context.Staff.OrderBy(s => s.CreatedAt).ToListAsync();
    }

    public async Task<Staff?> GetByIdAsync(string staffId)
    {
        return await _context.Staff.FirstOrDefaultAsync(s => s.Id == staffId);
    }

    public async Task<Staff?> AuthenticateAsync(string name, string password)
    {
        var staff = await _context.Staff
            .FirstOrDefaultAsync(s => s.Name == name && s.IsActive);

        if (staff == null) return null;

        if (!VerifyHash(staff.Salt, password, staff.PasswordHash))
            return null;

        // 自動升級舊版 SHA256 → PBKDF2
        if (!staff.PasswordHash.StartsWith("v2:"))
        {
            var newSalt = GenerateSalt();
            staff.PasswordHash = ComputeHash(newSalt, password);
            staff.Salt = newSalt;
            await _context.SaveChangesAsync();
        }

        return staff;
    }

    public async Task<Staff> CreateStaffAsync(string name, string password, StaffRole role)
    {
        var salt = GenerateSalt();
        var hash = ComputeHash(salt, password);

        var staff = new Staff
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            PasswordHash = hash,
            Salt = salt,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        _context.Staff.Add(staff);
        await _context.SaveChangesAsync();
        return staff;
    }

    public async Task UpdatePasswordAsync(string staffId, string newPassword)
    {
        var staff = await _context.Staff.FindAsync(staffId);
        if (staff == null) throw new InvalidOperationException("找不到工作人員");

        var salt = GenerateSalt();
        staff.PasswordHash = ComputeHash(salt, newPassword);
        staff.Salt = salt;
        await _context.SaveChangesAsync();
    }

    public async Task SetActiveAsync(string staffId, bool isActive)
    {
        var staff = await _context.Staff.FindAsync(staffId);
        if (staff == null) throw new InvalidOperationException("找不到工作人員");

        staff.IsActive = isActive;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string staffId)
    {
        var staff = await _context.Staff.FindAsync(staffId);
        if (staff == null) throw new InvalidOperationException("找不到工作人員");
        _context.Staff.Remove(staff);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasAnyStaffAsync()
    {
        return await _context.Staff.AnyAsync();
    }

    private static string GenerateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// 使用 PBKDF2（100,000 次迭代）產生雜湊，格式為 "v2:" 前綴
    /// </summary>
    private static string ComputeHash(string salt, string password)
    {
        var saltBytes = Convert.FromHexString(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
        return "v2:" + Convert.ToHexString(hash);
    }

    /// <summary>
    /// 驗證密碼，同時相容舊版 SHA256（無前綴）與新版 PBKDF2（v2: 前綴）
    /// </summary>
    private static bool VerifyHash(string salt, string password, string storedHash)
    {
        if (storedHash.StartsWith("v2:"))
        {
            return ComputeHash(salt, password) == storedHash;
        }
        else
        {
            // 舊版 SHA256（向後相容，驗證後會自動升級）
            var input = Encoding.UTF8.GetBytes(salt + password);
            var hash = SHA256.HashData(input);
            return Convert.ToHexString(hash) == storedHash;
        }
    }
}
