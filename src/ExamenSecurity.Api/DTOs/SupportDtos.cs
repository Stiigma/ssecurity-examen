using System.ComponentModel.DataAnnotations;

namespace ExamenSecurity.Api.DTOs;

public sealed record CreateSupportTicketRequest(
    [Required, MinLength(4), MaxLength(160)] string Subject,
    [Required, MinLength(10), MaxLength(1500)] string Description,
    bool IsSecurityRelevant);

public sealed record SupportTicketResponse(
    Guid Id,
    Guid UserId,
    string Subject,
    string Description,
    string Status,
    bool IsSecurityRelevant,
    DateTimeOffset CreatedAtUtc);
