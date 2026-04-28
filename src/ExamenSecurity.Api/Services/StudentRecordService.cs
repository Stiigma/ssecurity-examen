using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.DTOs;
using ExamenSecurity.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExamenSecurity.Api.Services;

public sealed class StudentRecordService(AppDbContext dbContext, ISecurityAuditService securityAuditService) : IStudentRecordService
{
    public async Task<StudentRecordResponse?> GetOwnRecordAsync(Guid userId, CancellationToken cancellationToken)
    {
        var record = await ProjectRecord()
            .FirstOrDefaultAsync(record => record.StudentUserId == userId, cancellationToken);

        if (record is not null)
        {
            await securityAuditService.AuditAsync(
                new SecurityAuditRequest(
                    SecurityEventType.StudentRecordAccessed,
                    SecuritySeverity.Info,
                    "Succeeded",
                    "Alumno consulto su propio expediente.",
                    UserId: userId,
                    Username: record.StudentEmail,
                    Role: Roles.Student,
                    StatusCode: StatusCodes.Status200OK,
                    ResourceType: "StudentRecord",
                    ResourceId: record.Id.ToString()),
                cancellationToken);
        }

        return record;
    }

    public async Task<StudentRecordResponse?> GetRecordForUserAsync(
        Guid requestedStudentUserId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        if (!isAdmin && requestedStudentUserId != currentUserId)
        {
            await securityAuditService.AuditAsync(
                new SecurityAuditRequest(
                    SecurityEventType.StudentRecordAccessDenied,
                    SecuritySeverity.High,
                    "Forbidden",
                    "Usuario intento consultar el expediente academico de otro alumno.",
                    UserId: currentUserId,
                    StatusCode: StatusCodes.Status403Forbidden,
                    ResourceType: "StudentUser",
                    ResourceId: requestedStudentUserId.ToString(),
                    Metadata: new Dictionary<string, object?>
                    {
                        ["requestedStudentUserId"] = requestedStudentUserId
                    }),
                cancellationToken);

            return null;
        }

        var record = await ProjectRecord()
            .FirstOrDefaultAsync(record => record.StudentUserId == requestedStudentUserId, cancellationToken);

        if (record is not null)
        {
            await securityAuditService.AuditAsync(
                new SecurityAuditRequest(
                    SecurityEventType.StudentRecordAccessed,
                    SecuritySeverity.Info,
                    "Succeeded",
                    "Expediente academico consultado por usuario autorizado.",
                    UserId: currentUserId,
                    StatusCode: StatusCodes.Status200OK,
                    ResourceType: "StudentRecord",
                    ResourceId: record.Id.ToString(),
                    Metadata: new Dictionary<string, object?>
                    {
                        ["studentUserId"] = record.StudentUserId,
                        ["accessMode"] = isAdmin ? "Admin" : "Owner"
                    }),
                cancellationToken);
        }

        return record;
    }

    private IQueryable<StudentRecordResponse> ProjectRecord()
    {
        return dbContext.StudentRecords
            .AsNoTracking()
            .Include(record => record.StudentUser)
            .Select(record => new StudentRecordResponse(
                record.Id,
                record.StudentUserId,
                record.StudentUser.FullName,
                record.StudentUser.Email,
                record.EnrollmentNumber,
                record.Career,
                record.Semester,
                record.GradePointAverage,
                record.AcademicStatus,
                record.ScholarshipStatus,
                record.AdvisorPrivateNotes,
                record.UpdatedAtUtc));
    }
}
