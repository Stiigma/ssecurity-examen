using ExamenSecurity.Api.Entities;

namespace ExamenSecurity.Api.Services;

public interface IImpossibleTravelService
{
    Task EvaluateAsync(AppUser user, string ipAddress, CancellationToken cancellationToken = default);
}
