using MediVault.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MediVault.Api.Data;

public sealed class MediVaultDbContext(DbContextOptions<MediVaultDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<IdentityDocument> Documents => Set<IdentityDocument>();

    public DbSet<MedicalRecord> Records => Set<MedicalRecord>();

    public DbSet<OpinionRequest> Opinions => Set<OpinionRequest>();

    public DbSet<OpinionRequestFile> OpinionFiles => Set<OpinionRequestFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<UserAccount>();
        user.HasIndex(u => u.Email).IsUnique();
        user.Property(u => u.FullName).HasMaxLength(120);
        user.Property(u => u.Email).HasMaxLength(200);
        user.Property(u => u.Phone).HasMaxLength(30);
        user.Property(u => u.Address).HasMaxLength(300);
        user.Property(u => u.Hospital).HasMaxLength(160);
        user.Property(u => u.PayPalEmail).HasMaxLength(200);
        user.Property(u => u.PasswordHash).HasMaxLength(500);
        user.HasMany(u => u.Documents).WithOne(d => d.User).HasForeignKey(d => d.UserId);

        var document = modelBuilder.Entity<IdentityDocument>();
        document.Property(d => d.FileName).HasMaxLength(260);
        document.Property(d => d.ContentType).HasMaxLength(100);
        document.Property(d => d.ObjectKey).HasMaxLength(400);
        user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        user.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);
        user.Property(u => u.SecondOpinionFee).HasPrecision(10, 2);

        var record = modelBuilder.Entity<MedicalRecord>();
        record.Property(r => r.FileName).HasMaxLength(260);
        record.Property(r => r.ContentType).HasMaxLength(100);
        record.Property(r => r.Description).HasMaxLength(400);
        record.Property(r => r.ObjectKey).HasMaxLength(400);
        record.HasIndex(r => r.PatientId);
        record.HasOne(r => r.Patient).WithMany().HasForeignKey(r => r.PatientId).OnDelete(DeleteBehavior.Cascade);

        var opinion = modelBuilder.Entity<OpinionRequest>();
        opinion.Property(o => o.Message).HasMaxLength(2000);
        opinion.Property(o => o.Intake).HasColumnType("text");
        opinion.Property(o => o.ClinicalAid).HasColumnType("text");
        opinion.Property(o => o.Opinion).HasMaxLength(4000);
        opinion.Property(o => o.Fee).HasPrecision(10, 2);
        opinion.Property(o => o.DoctorAmount).HasPrecision(10, 2);
        opinion.Property(o => o.PlatformAmount).HasPrecision(10, 2);
        opinion.Property(o => o.PayPalOrderId).HasMaxLength(40);
        opinion.Property(o => o.PayPalCaptureId).HasMaxLength(40);
        opinion.Property(o => o.PayPalPayoutId).HasMaxLength(40);
        opinion.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        opinion.Property(o => o.PatientSeenStatus).HasMaxLength(20);
        opinion.Property(o => o.DoctorSeenStatus).HasMaxLength(20);
        opinion.Property(o => o.RefundCause).HasConversion<string>().HasMaxLength(20);
        opinion.HasIndex(o => o.PatientId);
        opinion.HasIndex(o => o.DoctorId);
        opinion.HasOne(o => o.Patient).WithMany().HasForeignKey(o => o.PatientId).OnDelete(DeleteBehavior.Restrict);
        opinion.HasOne(o => o.Doctor).WithMany().HasForeignKey(o => o.DoctorId).OnDelete(DeleteBehavior.Restrict);

        var link = modelBuilder.Entity<OpinionRequestFile>();
        link.HasKey(f => new { f.RequestId, f.RecordId });
        link.HasOne(f => f.Request).WithMany(o => o.Files).HasForeignKey(f => f.RequestId).OnDelete(DeleteBehavior.Cascade);
        link.HasOne(f => f.Record).WithMany(r => r.RequestFiles).HasForeignKey(f => f.RecordId).OnDelete(DeleteBehavior.Restrict);
    }
}
