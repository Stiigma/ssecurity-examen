using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ExamenSecurity.Api.Services;

public sealed class StudentRecordService(AppDbContext dbContext) : IStudentRecordService
{
    public async Task<StudentRecordResponse?> GetOwnRecordAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await ProjectRecord()
            .FirstOrDefaultAsync(record => record.StudentUserId == userId, cancellationToken);
    }

    public async Task<StudentRecordResponse?> GetRecordForUserAsync(
        Guid requestedStudentUserId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        if (!isAdmin && requestedStudentUserId != currentUserId)
        {
            // Vulnerable A09 demo:
            // The access attempt is correctly denied, but the system does not record who tried
            // to access another student's record, which endpoint was used, or how often it happened.
            return null;
        }

        return await ProjectRecord()
            .FirstOrDefaultAsync(record => record.StudentUserId == requestedStudentUserId, cancellationToken);
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
