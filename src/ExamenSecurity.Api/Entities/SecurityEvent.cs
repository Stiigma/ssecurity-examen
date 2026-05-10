namespace ExamenSecurity.Api.Entities;

public sealed class SecurityEvent
{
    public Guid Id { get; set; }
    public SecurityEventType EventType { get; set; }
    public SecuritySeverity Severity { get; set; }
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }
    public string? Username { get; set; }
    public string? Role { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? HttpMethod { get; set; }
    public string? Path { get; set; }
    public int? StatusCode { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? EventHash { get; set; }
    public string? PreviousHash { get; set; }
}
