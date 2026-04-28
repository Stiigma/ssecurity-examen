using ExamenSecurity.Api.DTOs;

namespace ExamenSecurity.Api.Services;

public interface IStudentRecordService
{
    Task<StudentRecordResponse?> GetOwnRecordAsync(Guid userId, CancellationToken cancellationToken);
    Task<StudentRecordResponse?> GetRecordForUserAsync(Guid requestedStudentUserId, Guid currentUserId, bool isAdmin, CancellationToken cancellationToken);
}
