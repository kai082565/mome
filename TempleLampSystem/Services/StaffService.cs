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

        var hash = ComputeHash(staff.Salt, password);
        return hash == staff.PasswordHash ? staff : null;
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

    public async Task<bool> HasAnyStaffAsync()
    {
        return await _context.Staff.AnyAsync();
    }

    private static string GenerateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToHexString(bytes);
    }

    private static string ComputeHash(string salt, string password)
    {
        var input = Encoding.UTF8.GetBytes(salt + password);
        var hash = SHA256.HashData(input);
        return Convert.ToHexString(hash);
    }
}
