using ExamenSecurity.Api.Entities;

namespace ExamenSecurity.Api.Services;

public sealed record SecurityAuditRequest(
    SecurityEventType EventType,
    SecuritySeverity Severity,
    string Outcome,
    string Message,
    Guid? UserId = null,
    string? Username = null,
    string? Role = null,
    int? StatusCode = null,
    string? ResourceType = null,
    string? ResourceId = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);
