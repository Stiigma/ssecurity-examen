namespace ExamenSecurity.Api.Entities;

public sealed class StudentRecord
{
    public Guid Id { get; set; }
    public Guid StudentUserId { get; set; }
    public AppUser StudentUser { get; set; } = null!;
    public string EnrollmentNumber { get; set; } = string.Empty;
    public string Career { get; set; } = string.Empty;
    public int Semester { get; set; }
    public decimal GradePointAverage { get; set; }
    public string AcademicStatus { get; set; } = string.Empty;
    public string ScholarshipStatus { get; set; } = string.Empty;
    public string AdvisorPrivateNotes { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
