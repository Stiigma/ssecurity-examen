namespace ExamenSecurity.Api.Entities;

public sealed class SecurityAlert
{
    public Guid Id { get; set; }
    public SecurityAlertType AlertType { get; set; }
    public SecuritySeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? RelatedUserId { get; set; }
    public AppUser? RelatedUser { get; set; }
    public string? RelatedUsername { get; set; }
    public string? RelatedIpAddress { get; set; }
    public int EventCount { get; set; }
    public bool IsAcknowledged { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }
    public AppUser? AcknowledgedByUser { get; set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
