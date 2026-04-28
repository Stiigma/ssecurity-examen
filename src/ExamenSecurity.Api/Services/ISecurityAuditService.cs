namespace ExamenSecurity.Api.Services;

public interface ISecurityAuditService
{
    Task AuditAsync(SecurityAuditRequest request, CancellationToken cancellationToken);
}
