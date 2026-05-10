using ExamenSecurity.Api.Entities;

namespace ExamenSecurity.Api.Services;

public interface IGeoLocationService
{
    Task<GeoLocationResult> GetLocationAsync(string ipAddress, CancellationToken cancellationToken = default);
}
