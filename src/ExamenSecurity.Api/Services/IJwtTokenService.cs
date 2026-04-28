using ExamenSecurity.Api.Entities;

namespace ExamenSecurity.Api.Services;

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAtUtc) CreateToken(AppUser user);
}
