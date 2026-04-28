using System.ComponentModel.DataAnnotations;

namespace ExamenSecurity.Api.DTOs;

public sealed record ConfigurationChangeRequest(
    [Required, MaxLength(180)] string SettingKey,
    [Required, MaxLength(500)] string NewValue);

public sealed record ConfigurationChangeResponse(
    Guid Id,
    string SettingKey,
    string PreviousValue,
    string NewValue,
    Guid ChangedByUserId,
    DateTimeOffset ChangedAtUtc);

public sealed record ApiClientResponse(
    Guid Id,
    string Name,
    string OwnerTeam,
    bool IsEnabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DisabledAtUtc);
