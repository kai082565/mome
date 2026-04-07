using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public interface IStaffService
{
    Task<List<Staff>> GetAllAsync();
    Task<Staff?> GetByIdAsync(string staffId);
    Task<Staff?> AuthenticateAsync(string name, string password);
    Task<Staff> CreateStaffAsync(string name, string password, StaffRole role);
    Task UpdatePasswordAsync(string staffId, string newPassword);
    Task SetActiveAsync(string staffId, bool isActive);
    Task DeleteAsync(string staffId);
    Task<bool> HasAnyStaffAsync();
}
