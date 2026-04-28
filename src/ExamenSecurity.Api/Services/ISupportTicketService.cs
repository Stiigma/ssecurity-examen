using ExamenSecurity.Api.DTOs;

namespace ExamenSecurity.Api.Services;

public interface ISupportTicketService
{
    Task<SupportTicketResponse> CreateAsync(Guid userId, CreateSupportTicketRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<SupportTicketResponse>> GetMineAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SupportTicketResponse>> GetAllAsync(CancellationToken cancellationToken);
}
