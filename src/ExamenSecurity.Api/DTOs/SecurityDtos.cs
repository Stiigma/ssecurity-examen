using ExamenSecurity.Api.Entities;

namespace ExamenSecurity.Api.DTOs;

public sealed record SecurityEventResponse(
    Guid Id,
    SecurityEventType EventType,
    SecuritySeverity Severity,
    Guid? UserId,
    string? Username,
    string? Role,
    string? IpAddress,
    string? UserAgent,
    string? HttpMethod,
    string? Path,
    int? StatusCode,
    string? ResourceType,
    string? ResourceId,
    string Outcome,
    string Message,
    string? MetadataJson,
    string CorrelationId,
    DateTimeOffset CreatedAtUtc);

public sealed record SecurityAlertResponse(
    Guid Id,
    SecurityAlertType AlertType,
    SecuritySeverity Severity,
    string Title,
    string Description,
    Guid? RelatedUserId,
    string? RelatedUsername,
    string? RelatedIpAddress,
    int EventCount,
    bool IsAcknowledged,
    Guid? AcknowledgedByUserId,
    DateTimeOffset? AcknowledgedAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record SecurityDashboardSummaryResponse(
    int TotalEvents,
    int HighOrCriticalEvents,
    int OpenAlerts,
    int LoginFailuresLast10Minutes,
    int AccessDeniedLast10Minutes,
    DateTimeOffset GeneratedAtUtc);

public sealed record AccountLockoutResponse(
    Guid Id,
    string TargetType,
    string TargetValue,
    string Reason,
    DateTimeOffset LockedUntilUtc,
    DateTimeOffset CreatedAtUtc,
    bool IsActive,
    Guid? AlertId,
    DateTimeOffset? UnlockedAtUtc,
    Guid? UnlockedByUserId);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record IntegrityCheckResponse(
    bool IsValid,
    int TotalEventsChecked,
    Guid? FirstBrokenEventId,
    IReadOnlyList<Guid> MissingEventIds,
    string? ComputedHashVsStoredHash,
    string Message);
