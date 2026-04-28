using ExamenSecurity.Api.Entities;

namespace ExamenSecurity.Api.Services;

public interface ISecurityAlertService
{
    Task EvaluateAsync(SecurityEvent securityEvent, CancellationToken cancellationToken);
    Task<bool> AcknowledgeAsync(Guid alertId, Guid acknowledgedByUserId, CancellationToken cancellationToken);
}
