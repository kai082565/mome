using System.IO;
using Microsoft.EntityFrameworkCore;
using TempleLampSystem.Models;

namespace TempleLampSystem.Services;

public class AppDbContext : DbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lamp> Lamps => Set<Lamp>();
    public DbSet<LampOrder> LampOrders => Set<LampOrder>();
    public DbSet<SyncQueueItem> SyncQueue => Set<SyncQueueItem>();
    public DbSet<SyncConflict> SyncConflicts => Set<SyncConflict>();

    public AppDbContext() { }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, "TempleLamp.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ===== Customer =====
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .HasColumnType("TEXT")
                  .HasConversion(
                      v => v.ToString(),
                      v => Guid.Parse(v));

            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Mobile).HasMaxLength(20);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.Village).HasMaxLength(50);
            entity.Property(e => e.PostalCode).HasMaxLength(10);
            entity.Property(e => e.CustomerCode).HasMaxLength(10);
            entity.HasIndex(e => e.CustomerCode).IsUnique();
            entity.Property(e => e.BirthYear);
            entity.Property(e => e.BirthMonth);
            entity.Property(e => e.BirthDay);
            entity.Property(e => e.BirthHour).HasMaxLength(10);
            entity.Ignore(e => e.Zodiac);
            entity.Property(e => e.UpdatedAt).IsRequired();
        });

        // ===== Lamp =====
        modelBuilder.Entity<Lamp>(entity =>
        {
            entity.ToTable("Lamps");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.LampCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LampName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Temple).HasMaxLength(50);
            entity.Property(e => e.Deity).HasMaxLength(50);

            entity.HasIndex(e => e.LampCode).IsUnique();
        });

        // ===== LampOrder =====
        modelBuilder.Entity<LampOrder>(entity =>
        {
            entity.ToTable("LampOrders");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .HasColumnType("TEXT")
                  .HasConversion(
                      v => v.ToString(),
                      v => Guid.Parse(v));

            entity.Property(e => e.CustomerId)
                  .HasColumnType("TEXT")
                  .HasConversion(
                      v => v.ToString(),
                      v => Guid.Parse(v));

            entity.Property(e => e.Price).HasPrecision(10, 2);
            entity.Property(e => e.StartDate).IsRequired();
            entity.Property(e => e.EndDate).IsRequired();
            entity.Property(e => e.Year).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            entity.HasOne(e => e.Customer)
                  .WithMany(c => c.LampOrders)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Lamp)
                  .WithMany(l => l.LampOrders)
                  .HasForeignKey(e => e.LampId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.Year);
            entity.HasIndex(e => e.CustomerId);
        });

        // ===== SyncQueue =====
        modelBuilder.Entity<SyncQueueItem>(entity =>
        {
            entity.ToTable("SyncQueue");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired();
            entity.Property(e => e.EntityId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Operation).IsRequired();
            entity.Property(e => e.JsonData).HasMaxLength(4000);
            entity.HasIndex(e => e.CreatedAt);
        });

        // ===== SyncConflict =====
        modelBuilder.Entity<SyncConflict>(entity =>
        {
            entity.ToTable("SyncConflicts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired();
            entity.Property(e => e.EntityId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LocalData).HasMaxLength(4000);
            entity.Property(e => e.RemoteData).HasMaxLength(4000);
        });
    }
}
