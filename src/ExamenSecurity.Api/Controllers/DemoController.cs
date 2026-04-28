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
            version = "fixed",
            warning = "Demo defensiva local para OWASP A09."
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
    public ActionResult<ObservabilitySummaryResponse> GetObservabilitySummary()
    {
        return Ok(demoScenarioService.GetObservabilitySummary());
    }

    [Authorize]
    [HttpGet("simulate-unhandled-error")]
    public IActionResult SimulateUnhandledError()
    {
        // Fixed A09 demo:
        // The global exception middleware turns this into a durable security event with correlation id.
        throw new InvalidOperationException("Error simulado para demostrar registro de seguridad durable.");
    }
}
