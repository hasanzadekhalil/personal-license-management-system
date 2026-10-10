using LicenseSystem.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LicenseSystem.Infrastructure.Data;

public class LicenseDbContext : DbContext
{
    public LicenseDbContext(DbContextOptions<LicenseDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<LicenseProduct> LicenseProducts => Set<LicenseProduct>();
    public DbSet<Activation> Activations => Set<Activation>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<ReactivationTransaction> ReactivationTransactions => Set<ReactivationTransaction>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Product
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        });

        // License
        modelBuilder.Entity<License>(entity =>
        {
            entity.ToTable("licenses");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.LicenseKey).IsUnique();
            entity.Property(e => e.LicenseKey).HasMaxLength(64).IsRequired();
            entity.Property(e => e.LicenseType).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.LicenseMode).HasConversion<string>().HasMaxLength(25).IsRequired();
        });

        // LicenseProduct (Many-to-Many junction)
        modelBuilder.Entity<LicenseProduct>(entity =>
        {
            entity.ToTable("license_products");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.LicenseId, e.ProductId }).IsUnique();

            entity.HasOne(e => e.License)
                .WithMany(l => l.LicenseProducts)
                .HasForeignKey(e => e.LicenseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Product)
                .WithMany(p => p.LicenseProducts)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Activation
        modelBuilder.Entity<Activation>(entity =>
        {
            entity.ToTable("activations");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.LicenseId, e.HardwareHash }).IsUnique();

            entity.Property(e => e.HardwareHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.MotherboardId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.CpuId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PrimaryDiskId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DeviceName).HasMaxLength(100);

            entity.HasOne(e => e.License)
                .WithMany(l => l.Activations)
                .HasForeignKey(e => e.LicenseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AdminUser
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("admin_users");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        });

        // ReactivationTransaction
        modelBuilder.Entity<ReactivationTransaction>(entity =>
        {
            entity.ToTable("reactivation_transactions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Nonce).IsUnique();

            entity.Property(e => e.OldHardwareHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.NewHardwareHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Nonce).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

            entity.HasOne(e => e.License)
                .WithMany(l => l.ReactivationTransactions)
                .HasForeignKey(e => e.LicenseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ResolvedByAdmin)
                .WithMany()
                .HasForeignKey(e => e.ResolvedByAdminId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // AuditEvent
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Actor).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ProductCode).HasMaxLength(50);
            entity.Property(e => e.LicenseKey).HasMaxLength(64);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
        });
    }
}
