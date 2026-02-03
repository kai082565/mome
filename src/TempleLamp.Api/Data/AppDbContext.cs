using Microsoft.EntityFrameworkCore;
using TempleLamp.Api.Models;

namespace TempleLamp.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Lamp> Lamps { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 設定 Lamp 資料表
        modelBuilder.Entity<Lamp>(entity =>
        {
            entity.ToTable("Lamps");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        });
    }
}
