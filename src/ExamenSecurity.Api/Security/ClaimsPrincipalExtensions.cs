using System.Security.Claims;

namespace ExamenSecurity.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }

    public static string GetEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    }
}
