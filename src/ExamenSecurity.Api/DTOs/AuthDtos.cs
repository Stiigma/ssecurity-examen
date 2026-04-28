using System.ComponentModel.DataAnnotations;

namespace ExamenSecurity.Api.DTOs;

public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8), MaxLength(128)] string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    UserSummaryResponse User);

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    string Department,
    bool IsEnabled);

public sealed record PasswordResetRequest(
    [Required, EmailAddress, MaxLength(256)] string Email);

public sealed record PasswordResetResponse(
    string Message,
    string A09Observation);
