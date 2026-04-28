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
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<SecurityAlert> SecurityAlerts => Set<SecurityAlert>();

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

        modelBuilder.Entity<SecurityEvent>(entity =>
        {
            entity.HasKey(securityEvent => securityEvent.Id);
            entity.HasIndex(securityEvent => securityEvent.CreatedAtUtc);
            entity.HasIndex(securityEvent => securityEvent.EventType);
            entity.HasIndex(securityEvent => securityEvent.Severity);
            entity.HasIndex(securityEvent => securityEvent.Username);
            entity.HasIndex(securityEvent => securityEvent.IpAddress);
            entity.Property(securityEvent => securityEvent.EventType).HasConversion<string>().HasMaxLength(80).IsRequired();
            entity.Property(securityEvent => securityEvent.Severity).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(securityEvent => securityEvent.Username).HasMaxLength(256);
            entity.Property(securityEvent => securityEvent.Role).HasMaxLength(40);
            entity.Property(securityEvent => securityEvent.IpAddress).HasMaxLength(64);
            entity.Property(securityEvent => securityEvent.UserAgent).HasMaxLength(512);
            entity.Property(securityEvent => securityEvent.HttpMethod).HasMaxLength(16);
            entity.Property(securityEvent => securityEvent.Path).HasMaxLength(512);
            entity.Property(securityEvent => securityEvent.ResourceType).HasMaxLength(120);
            entity.Property(securityEvent => securityEvent.ResourceId).HasMaxLength(120);
            entity.Property(securityEvent => securityEvent.Outcome).HasMaxLength(80).IsRequired();
            entity.Property(securityEvent => securityEvent.Message).HasMaxLength(1000).IsRequired();
            entity.Property(securityEvent => securityEvent.MetadataJson).HasMaxLength(4000);
            entity.Property(securityEvent => securityEvent.CorrelationId).HasMaxLength(80).IsRequired();
            entity.HasOne(securityEvent => securityEvent.User)
                .WithMany()
                .HasForeignKey(securityEvent => securityEvent.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SecurityAlert>(entity =>
        {
            entity.HasKey(alert => alert.Id);
            entity.HasIndex(alert => alert.CreatedAtUtc);
            entity.HasIndex(alert => alert.AlertType);
            entity.HasIndex(alert => alert.IsAcknowledged);
            entity.Property(alert => alert.AlertType).HasConversion<string>().HasMaxLength(80).IsRequired();
            entity.Property(alert => alert.Severity).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(alert => alert.Title).HasMaxLength(200).IsRequired();
            entity.Property(alert => alert.Description).HasMaxLength(1000).IsRequired();
            entity.Property(alert => alert.RelatedUsername).HasMaxLength(256);
            entity.Property(alert => alert.RelatedIpAddress).HasMaxLength(64);
            entity.HasOne(alert => alert.RelatedUser)
                .WithMany()
                .HasForeignKey(alert => alert.RelatedUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(alert => alert.AcknowledgedByUser)
                .WithMany()
                .HasForeignKey(alert => alert.AcknowledgedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
