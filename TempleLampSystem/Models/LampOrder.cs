namespace TempleLampSystem.Models;

public class LampOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public int LampId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Year { get; set; }              // 點燈年度
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 導航屬性
    public Customer Customer { get; set; } = null!;
    public Lamp Lamp { get; set; } = null!;
}
