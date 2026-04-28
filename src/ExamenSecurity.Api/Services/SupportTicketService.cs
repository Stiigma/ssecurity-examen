using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExamenSecurity.Api.Services;

public sealed class SupportTicketService(AppDbContext dbContext, ISecurityAuditService securityAuditService) : ISupportTicketService
{
    public async Task<SupportTicketResponse> CreateAsync(Guid userId, CreateSupportTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Subject = request.Subject.Trim(),
            Description = request.Description.Trim(),
            IsSecurityRelevant = request.IsSecurityRelevant,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.SupportTickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (ticket.IsSecurityRelevant)
        {
            await securityAuditService.AuditAsync(
                new SecurityAuditRequest(
                    SecurityEventType.SecurityTicketCreated,
                    SecuritySeverity.Medium,
                    "Accepted",
                    "Usuario creo un ticket marcado como relevante para seguridad.",
                    UserId: userId,
                    StatusCode: StatusCodes.Status201Created,
                    ResourceType: "SupportTicket",
                    ResourceId: ticket.Id.ToString(),
                    Metadata: new Dictionary<string, object?>
                    {
                        ["subject"] = ticket.Subject
                    }),
                cancellationToken);
        }

        return Map(ticket);
    }

    public async Task<IReadOnlyList<SupportTicketResponse>> GetMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.SupportTickets
            .AsNoTracking()
            .Where(ticket => ticket.UserId == userId)
            .OrderByDescending(ticket => ticket.CreatedAtUtc)
            .Select(ticket => new SupportTicketResponse(
                ticket.Id,
                ticket.UserId,
                ticket.Subject,
                ticket.Description,
                ticket.Status,
                ticket.IsSecurityRelevant,
                ticket.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SupportTicketResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await dbContext.SupportTickets
            .AsNoTracking()
            .OrderByDescending(ticket => ticket.CreatedAtUtc)
            .Select(ticket => new SupportTicketResponse(
                ticket.Id,
                ticket.UserId,
                ticket.Subject,
                ticket.Description,
                ticket.Status,
                ticket.IsSecurityRelevant,
                ticket.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private static SupportTicketResponse Map(SupportTicket ticket)
    {
        return new SupportTicketResponse(
            ticket.Id,
            ticket.UserId,
            ticket.Subject,
            ticket.Description,
            ticket.Status,
            ticket.IsSecurityRelevant,
            ticket.CreatedAtUtc);
    }
}
