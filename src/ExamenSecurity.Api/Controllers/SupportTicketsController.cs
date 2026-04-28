using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Security;
using ExamenSecurity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamenSecurity.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/support-tickets")]
public sealed class SupportTicketsController(ISupportTicketService supportTicketService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SupportTicketResponse>> Create(
        CreateSupportTicketRequest request,
        CancellationToken cancellationToken)
    {
        var response = await supportTicketService.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetMine), new { id = response.Id }, response);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<SupportTicketResponse>>> GetMine(CancellationToken cancellationToken)
    {
        return Ok(await supportTicketService.GetMineAsync(User.GetUserId(), cancellationToken));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupportTicketResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await supportTicketService.GetAllAsync(cancellationToken));
    }
}
