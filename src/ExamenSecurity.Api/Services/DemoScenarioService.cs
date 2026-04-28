using ExamenSecurity.Api.DTOs;

namespace ExamenSecurity.Api.Services;

public sealed class DemoScenarioService : IDemoScenarioService
{
    public IReadOnlyList<DemoScenarioResponse> GetScenarios()
    {
        return
        [
            new DemoScenarioResponse(
                "A09-LOGIN-FAILURES",
                "Intentos repetidos de login auditados y alertados",
                "A09:2025 - Security Logging and Alerting Failures",
                "Enviar 5 o mas POST /api/auth/login con password incorrecto para admin@demo.local.",
                "La API devuelve 401, persiste eventos LoginFailed y genera alerta MultipleFailedLogins.",
                "Un password spraying ya deja evidencia accionable para investigacion."),
            new DemoScenarioResponse(
                "A09-ACCESS-DENIED",
                "Acceso denegado a funcion administrativa con evidencia",
                "A09:2025 - Security Logging and Alerting Failures",
                "Iniciar sesion como student1@demo.local y llamar GET /api/admin/users.",
                "La API devuelve 403 y persiste AccessDenied con usuario, endpoint, IP y correlationId.",
                "Rechazar una peticion ahora tambien produce visibilidad defensiva."),
            new DemoScenarioResponse(
                "A09-RECORD-PROBING",
                "Sondeo de expedientes academicos con trazabilidad",
                "A09:2025 - Security Logging and Alerting Failures",
                "Como student1, llamar GET /api/student-records/by-user/20000000-0000-0000-0000-000000000002.",
                "La API niega el acceso y persiste StudentRecordAccessDenied. Dos intentos generan alerta StudentRecordProbing.",
                "La institucion pierde visibilidad sobre abuso contra datos academicos sensibles."),
            new DemoScenarioResponse(
                "A09-ADMIN-CHANGES",
                "Acciones administrativas con auditoria de seguridad",
                "A09:2025 - Security Logging and Alerting Failures",
                "Como admin, deshabilitar un usuario o cambiar una configuracion sensible.",
                "La accion se ejecuta y queda auditada con actor, target, IP, user-agent y correlationId.",
                "Ante una investigacion, existe evidencia minima para responder quien hizo que y cuando."),
            new DemoScenarioResponse(
                "A09-SECURITY-TICKET",
                "Reporte de seguridad con alerta operativa interna",
                "A09:2025 - Security Logging and Alerting Failures",
                "Crear un ticket con isSecurityRelevant=true.",
                "El ticket crea un evento SecurityTicketCreated y una alerta SecurityTicketRequiresReview.",
                "Una senal humana de incidente ya no queda perdida dentro del ruido operacional.")
        ];
    }

    public ObservabilitySummaryResponse GetObservabilitySummary()
    {
        return new ObservabilitySummaryResponse(
            "fixed - laboratorio local",
            PersistedSecurityEvents: 1,
            GeneratedSecurityAlerts: 1,
            LoginFailuresAreAudited: true,
            AccessDeniedIsAudited: true,
            AdminActionsAreAudited: true,
            SuspiciousPatternsCreateAlerts: true,
            "La aplicacion registra eventos de seguridad en SQL Server y genera alertas internas accionables.");
    }
}
