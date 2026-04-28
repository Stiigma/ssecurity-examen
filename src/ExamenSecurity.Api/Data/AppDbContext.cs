using ExamenSecurity.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExamenSecurity.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<StudentRecord> StudentRecords => Set<StudentRecord>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<ConfigurationChange> ConfigurationChanges => Set<ConfigurationChange>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.Email).HasMaxLength(256).IsRequired();
            entity.Property(user => user.FullName).HasMaxLength(180).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(40).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(user => user.Department).HasMaxLength(120).IsRequired();
            entity.Property(user => user.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<StudentRecord>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.HasIndex(record => record.StudentUserId).IsUnique();
            entity.Property(record => record.EnrollmentNumber).HasMaxLength(40).IsRequired();
            entity.Property(record => record.Career).HasMaxLength(160).IsRequired();
            entity.Property(record => record.GradePointAverage).HasPrecision(4, 2);
            entity.Property(record => record.AcademicStatus).HasMaxLength(80).IsRequired();
            entity.Property(record => record.ScholarshipStatus).HasMaxLength(120).IsRequired();
            entity.Property(record => record.AdvisorPrivateNotes).HasMaxLength(1000).IsRequired();
            entity.HasOne(record => record.StudentUser)
                .WithOne(user => user.StudentRecord)
                .HasForeignKey<StudentRecord>(record => record.StudentUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasKey(ticket => ticket.Id);
            entity.Property(ticket => ticket.Subject).HasMaxLength(160).IsRequired();
            entity.Property(ticket => ticket.Description).HasMaxLength(1500).IsRequired();
            entity.Property(ticket => ticket.Status).HasMaxLength(40).IsRequired();
            entity.HasOne(ticket => ticket.User)
                .WithMany(user => user.SupportTickets)
                .HasForeignKey(ticket => ticket.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConfigurationChange>(entity =>
        {
            entity.HasKey(change => change.Id);
            entity.Property(change => change.SettingKey).HasMaxLength(180).IsRequired();
            entity.Property(change => change.PreviousValue).HasMaxLength(500).IsRequired();
            entity.Property(change => change.NewValue).HasMaxLength(500).IsRequired();
            entity.HasOne(change => change.ChangedByUser)
                .WithMany()
                .HasForeignKey(change => change.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiClient>(entity =>
        {
            entity.HasKey(client => client.Id);
            entity.Property(client => client.Name).HasMaxLength(160).IsRequired();
            entity.Property(client => client.OwnerTeam).HasMaxLength(120).IsRequired();
        });
    }
}
