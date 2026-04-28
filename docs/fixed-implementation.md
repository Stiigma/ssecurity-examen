# Rama fixed - Implementacion defensiva A09

Esta rama corrige la vulnerabilidad demostrada en `main`: la aplicacion ya no se limita a responder `401` o `403`, sino que registra eventos de seguridad persistentes en SQL Server y genera alertas internas cuando detecta patrones sospechosos.

## Decisiones de diseno

- La evidencia vive en SQL Server mediante `SecurityEvents`.
- Las alertas viven en SQL Server mediante `SecurityAlerts`.
- Cada request tiene `X-Correlation-ID`.
- Los errores no controlados se capturan sin exponer stack traces al cliente.
- No se implementaron notificaciones externas para mantener una demo reproducible sin correo, Slack, Teams o webhooks.
- No se guardan passwords, tokens completos ni secretos en metadata.

## Eventos auditados

| Evento | Causa | Severidad |
|---|---|---|
| LoginSucceeded | Login correcto | Info |
| LoginFailed | Password incorrecto o usuario inexistente | Warning |
| DisabledAccountLoginAttempt | Login contra cuenta deshabilitada | High |
| PasswordResetRequested | Solicitud de recuperacion | Medium |
| TokenAuthenticationFailed | JWT invalido o expirado | Warning |
| UnauthorizedRequest | Recurso protegido sin token valido | Warning |
| AccessDenied | Usuario autenticado sin permisos | Warning |
| StudentRecordAccessed | Consulta autorizada de expediente | Info |
| StudentRecordAccessDenied | Intento de expediente ajeno | High |
| AdminUserCreated | Creacion de usuario | Medium/High |
| AdminUserDisabled | Deshabilitacion de usuario | High |
| ApiClientDisabled | Deshabilitacion de cliente API | High |
| ConfigurationChanged | Cambio sensible de configuracion | High |
| SecurityTicketCreated | Ticket marcado como seguridad | Medium |
| AlertAcknowledged | Reconocimiento de alerta | Info |
| UnhandledException | Error no controlado | Error |

## Alertas internas

| Alerta | Regla |
|---|---|
| MultipleFailedLogins | 5 login fallidos en 10 minutos por usuario o IP |
| DisabledAccountTargeted | 2 intentos contra cuenta deshabilitada |
| AdminEndpointProbing | 3 accesos denegados a `/api/admin` |
| StudentRecordProbing | 2 intentos de consultar expediente ajeno |
| SensitiveAdminChange | Accion administrativa sensible |
| SecurityTicketRequiresReview | Ticket marcado como seguridad |
| RepeatedUnhandledErrors | 3 errores no controlados en el mismo endpoint |

## Endpoints nuevos o corregidos

```http
GET  /api/security/events
GET  /api/security/events/{id}
GET  /api/security/alerts
GET  /api/security/alerts/{id}
POST /api/security/alerts/{id}/acknowledge
GET  /api/security/dashboard-summary
GET  /api/security/observability-readiness
```

Solo `Admin` y `Auditor` pueden consultar eventos y alertas.

## Mensaje para exposicion

En `main`, el sistema estaba parcialmente protegido, pero era ciego. En `fixed`, la defensa agrega evidencia durable, correlacion, severidad y alertas internas. Esto permite detectar abuso, investigar incidentes y responder de forma profesional.
