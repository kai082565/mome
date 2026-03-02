namespace TempleLampSystem.Models;

public enum StaffRole
{
    Staff = 0,
    Admin = 1
}

public class Staff
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public StaffRole Role { get; set; } = StaffRole.Staff;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
