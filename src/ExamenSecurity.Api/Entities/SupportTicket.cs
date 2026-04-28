namespace ExamenSecurity.Api.Entities;

public sealed class SupportTicket
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public bool IsSecurityRelevant { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
}
