using ExamenSecurity.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamenSecurity.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{Roles.Admin},{Roles.Auditor}")]
[Route("api/security")]
public sealed class SecurityController : ControllerBase
{
    [HttpGet("events")]
    public IActionResult GetEvents()
    {
        return Ok(new
        {
            events = Array.Empty<object>(),
            a09 = "Version main vulnerable: no existe almacenamiento real de eventos de seguridad."
        });
    }

    [HttpGet("alerts")]
    public IActionResult GetAlerts()
    {
        return Ok(new
        {
            alerts = Array.Empty<object>(),
            a09 = "Version main vulnerable: no existe motor de alertas ni umbrales de comportamiento sospechoso."
        });
    }
}
