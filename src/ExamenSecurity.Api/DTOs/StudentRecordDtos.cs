namespace ExamenSecurity.Api.DTOs;

public sealed record StudentRecordResponse(
    Guid Id,
    Guid StudentUserId,
    string StudentName,
    string StudentEmail,
    string EnrollmentNumber,
    string Career,
    int Semester,
    decimal GradePointAverage,
    string AcademicStatus,
    string ScholarshipStatus,
    string AdvisorPrivateNotes,
    DateTimeOffset UpdatedAtUtc);
