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
                "Intentos repetidos de login sin auditoria",
                "A09:2025 - Security Logging and Alerting Failures",
                "Enviar 5 o mas POST /api/auth/login con password incorrecto para admin@demo.local.",
                "La API devuelve 401, pero no persiste eventos, no correlaciona IP/usuario y no genera alerta.",
                "Un password spraying o fuerza bruta lenta puede pasar desapercibida hasta que sea tarde."),
            new DemoScenarioResponse(
                "A09-ACCESS-DENIED",
                "Acceso denegado a funcion administrativa sin evidencia",
                "A09:2025 - Security Logging and Alerting Failures",
                "Iniciar sesion como student1@demo.local y llamar GET /api/admin/users.",
                "La API devuelve 403 por autorizacion, pero no registra el intento de acceso a un recurso administrativo.",
                "Rechazar una peticion no equivale a detectar un incidente."),
            new DemoScenarioResponse(
                "A09-RECORD-PROBING",
                "Sondeo de expedientes academicos sin trazabilidad",
                "A09:2025 - Security Logging and Alerting Failures",
                "Como student1, llamar GET /api/student-records/by-user/20000000-0000-0000-0000-000000000002.",
                "La API niega el acceso, pero no deja evidencia de que un alumno intento consultar el expediente de otro.",
                "La institucion pierde visibilidad sobre abuso contra datos academicos sensibles."),
            new DemoScenarioResponse(
                "A09-ADMIN-CHANGES",
                "Acciones administrativas sin auditoria de seguridad",
                "A09:2025 - Security Logging and Alerting Failures",
                "Como admin, deshabilitar un usuario o cambiar una configuracion sensible.",
                "La accion se ejecuta, pero no existe auditoria con actor, target, IP, user-agent y motivo verificable.",
                "Ante una investigacion, no hay evidencia suficiente para responder quien hizo que y cuando."),
            new DemoScenarioResponse(
                "A09-SECURITY-TICKET",
                "Reporte de seguridad sin alerta operativa",
                "A09:2025 - Security Logging and Alerting Failures",
                "Crear un ticket con isSecurityRelevant=true.",
                "El ticket queda como soporte normal. No se genera alerta, prioridad ni evento de seguridad.",
                "Una senal humana de incidente puede perderse dentro del ruido operacional.")
        ];
    }

    public VulnerableObservabilitySummaryResponse GetVulnerableSummary()
    {
        return new VulnerableObservabilitySummaryResponse(
            "main vulnerable - laboratorio local",
            PersistedSecurityEvents: 0,
            GeneratedSecurityAlerts: 0,
            LoginFailuresAreAudited: false,
            AccessDeniedIsAudited: false,
            AdminActionsAreAudited: false,
            SuspiciousPatternsCreateAlerts: false,
            "La aplicacion toma decisiones de seguridad, pero no produce evidencia accionable ni alertas.");
    }
}
