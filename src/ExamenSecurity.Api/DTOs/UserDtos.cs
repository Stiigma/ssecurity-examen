using System.ComponentModel.DataAnnotations;

namespace ExamenSecurity.Api.DTOs;

public sealed record UserSummaryResponse(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    string Department,
    bool IsEnabled);

public sealed record CreateUserRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(3), MaxLength(180)] string FullName,
    [Required, MaxLength(40)] string Role,
    [Required, MinLength(8), MaxLength(128)] string Password,
    [Required, MaxLength(120)] string Department);

public sealed record DisableUserRequest(
    [Required, MinLength(5), MaxLength(240)] string Reason);
