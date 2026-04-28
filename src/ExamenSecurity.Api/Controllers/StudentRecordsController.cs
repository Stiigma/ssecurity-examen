using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Security;
using ExamenSecurity.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamenSecurity.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{Roles.Student},{Roles.Admin}")]
[Route("api/student-records")]
public sealed class StudentRecordsController(IStudentRecordService studentRecordService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<StudentRecordResponse>> GetMine(CancellationToken cancellationToken)
    {
        var response = await studentRecordService.GetOwnRecordAsync(User.GetUserId(), cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("by-user/{studentUserId:guid}")]
    public async Task<ActionResult<StudentRecordResponse>> GetByUser(Guid studentUserId, CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole(Roles.Admin);
        var response = await studentRecordService.GetRecordForUserAsync(
            studentUserId,
            User.GetUserId(),
            isAdmin,
            cancellationToken);

        if (response is null)
        {
            // Vulnerable A09 demo:
            // This may be a denied probing attempt against another student's record,
            // but no event is persisted and no alert can be generated.
            return Forbid();
        }

        return Ok(response);
    }
}
