using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace ExamenSecurity.Api.Data;

public sealed class DbSeeder(AppDbContext dbContext, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseReadyAsync(cancellationToken);

        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        var admin = new AppUser
        {
            Id = DemoIds.AdminUserId,
            Email = "admin@demo.local",
            FullName = "Dra. Camila Rivera",
            Role = Roles.Admin,
            PasswordHash = PasswordHasher.Hash("Admin123!"),
            Department = "Coordinacion Academica",
            CreatedAtUtc = now
        };

        var auditor = new AppUser
        {
            Id = DemoIds.AuditorUserId,
            Email = "auditor@demo.local",
            FullName = "Ing. Diego Monitoreo",
            Role = Roles.Auditor,
            PasswordHash = PasswordHasher.Hash("Auditor123!"),
            Department = "Seguridad Institucional",
            CreatedAtUtc = now
        };

        var student1 = new AppUser
        {
            Id = DemoIds.StudentOneUserId,
            Email = "student1@demo.local",
            FullName = "Andrea Lopez",
            Role = Roles.Student,
            PasswordHash = PasswordHasher.Hash("Student123!"),
            Department = "Ingenieria de Software",
            CreatedAtUtc = now
        };

        var student2 = new AppUser
        {
            Id = DemoIds.StudentTwoUserId,
            Email = "student2@demo.local",
            FullName = "Marco Silva",
            Role = Roles.Student,
            PasswordHash = PasswordHasher.Hash("Student123!"),
            Department = "Arquitectura",
            CreatedAtUtc = now
        };

        var disabledStudent = new AppUser
        {
            Id = DemoIds.DisabledStudentUserId,
            Email = "disabled@demo.local",
            FullName = "Usuario Deshabilitado",
            Role = Roles.Student,
            PasswordHash = PasswordHasher.Hash("Disabled123!"),
            Department = "Diseno Grafico",
            CreatedAtUtc = now,
            IsEnabled = false
        };

        dbContext.Users.AddRange(admin, auditor, student1, student2, disabledStudent);

        dbContext.StudentRecords.AddRange(
            new StudentRecord
            {
                Id = DemoIds.StudentOneRecordId,
                StudentUserId = student1.Id,
                EnrollmentNumber = "01234567",
                Career = "Ingenieria en Software y Tecnologias Emergentes",
                Semester = 7,
                GradePointAverage = 9.42m,
                AcademicStatus = "Regular",
                ScholarshipStatus = "Beca institucional activa",
                AdvisorPrivateNotes = "Notas privadas de asesoria academica de Andrea.",
                UpdatedAtUtc = now
            },
            new StudentRecord
            {
                Id = DemoIds.StudentTwoRecordId,
                StudentUserId = student2.Id,
                EnrollmentNumber = "07654321",
                Career = "Arquitectura",
                Semester = 5,
                GradePointAverage = 8.77m,
                AcademicStatus = "Regular con seguimiento",
                ScholarshipStatus = "Sin beca activa",
                AdvisorPrivateNotes = "Marco requiere seguimiento por carga academica.",
                UpdatedAtUtc = now
            },
            new StudentRecord
            {
                Id = DemoIds.DisabledStudentRecordId,
                StudentUserId = disabledStudent.Id,
                EnrollmentNumber = "09990001",
                Career = "Diseno Grafico",
                Semester = 3,
                GradePointAverage = 7.95m,
                AcademicStatus = "Cuenta suspendida",
                ScholarshipStatus = "No aplica",
                AdvisorPrivateNotes = "Cuenta usada para demostrar intentos de acceso sin monitoreo.",
                UpdatedAtUtc = now
            });

        dbContext.SupportTickets.AddRange(
            new SupportTicket
            {
                Id = Guid.Parse("50111111-1111-1111-1111-111111111111"),
                UserId = student1.Id,
                Subject = "No puedo descargar mi constancia",
                Description = "La pantalla marca error al descargar la constancia.",
                IsSecurityRelevant = false,
                CreatedAtUtc = now.AddMinutes(-45)
            },
            new SupportTicket
            {
                Id = Guid.Parse("50222222-2222-2222-2222-222222222222"),
                UserId = student2.Id,
                Subject = "Alguien intento entrar a mi cuenta",
                Description = "Recibi notificaciones de correo sobre actividad inusual, pero no veo historial en el sistema.",
                IsSecurityRelevant = true,
                CreatedAtUtc = now.AddMinutes(-30)
            });

        dbContext.ApiClients.AddRange(
            new ApiClient
            {
                Id = Guid.Parse("60111111-1111-1111-1111-111111111111"),
                Name = "Portal React Local",
                OwnerTeam = "Equipo Demo",
                IsEnabled = true,
                CreatedAtUtc = now
            },
            new ApiClient
            {
                Id = Guid.Parse("60222222-2222-2222-2222-222222222222"),
                Name = "Integracion Legacy",
                OwnerTeam = "Servicios Escolares",
                IsEnabled = true,
                CreatedAtUtc = now.AddDays(-120)
            });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDatabaseReadyAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 30;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    exception,
                    "SQL Server is not ready yet. Attempt {Attempt}/{MaxAttempts}.",
                    attempt,
                    maxAttempts);

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }
}
