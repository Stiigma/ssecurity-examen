namespace ExamenSecurity.Api.Entities;

public sealed class AppUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Student;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string Department { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastLoginAtUtc { get; set; }

    public StudentRecord? StudentRecord { get; set; }
    public ICollection<SupportTicket> SupportTickets { get; set; } = [];
    public ICollection<UserLoginLocation> LoginLocations { get; set; } = [];
}
