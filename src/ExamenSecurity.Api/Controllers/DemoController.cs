using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamenSecurity.Api.Controllers;

[ApiController]
[Route("api/demo")]
public sealed class DemoController(IDemoScenarioService demoScenarioService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "ok",
            version = "main vulnerable",
            warning = "Codigo vulnerable solo para laboratorio local. No usar en produccion."
        });
    }

    [AllowAnonymous]
    [HttpGet("scenarios")]
    public ActionResult<IReadOnlyList<DemoScenarioResponse>> GetScenarios()
    {
        return Ok(demoScenarioService.GetScenarios());
    }

    [Authorize]
    [HttpGet("observability-summary")]
    public ActionResult<VulnerableObservabilitySummaryResponse> GetObservabilitySummary()
    {
        return Ok(demoScenarioService.GetVulnerableSummary());
    }

    [Authorize]
    [HttpGet("simulate-unhandled-error")]
    public IActionResult SimulateUnhandledError()
    {
        // Vulnerable A09 demo:
        // The error is not transformed into a security event with correlation id, user, path and request metadata.
        throw new InvalidOperationException("Error simulado para demostrar ausencia de registro de seguridad durable.");
    }
}
