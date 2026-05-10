# Presentacion Canva - OWASP A09:2025 Security Logging and Alerting Failures

Documento fuente para construir una presentacion en Canva sobre la vulnerabilidad A09 trabajada en este backend.

## Resumen ejecutivo

**Tesis de la exposicion:** en la rama `master/main`, la aplicacion rechaza algunas acciones maliciosas con `401` o `403`, pero no deja evidencia defensiva suficiente. Eso es una falla OWASP A09: el sistema esta protegido parcialmente, pero esta ciego para detectar, investigar y responder.

**Contramedida en `fixed`:** se agrego un pipeline de observabilidad de seguridad:

- Eventos persistentes en `SecurityEvents`.
- Alertas persistentes en `SecurityAlerts`.
- `X-Correlation-ID` por request.
- Auditoria de autenticacion, autorizacion, expedientes, administracion, tickets de seguridad y errores.
- Reglas de alertamiento por umbrales.
- Rate limiting por IP.
- Account lockout temporal para usuario/IP.
- Endpoints protegidos para investigacion.
- Pruebas de integracion para rate limiting y lockout.

**Resultado verificable:** `dotnet test ExamenSecurity.sln` pasa con `14/14` pruebas.

## Referencias OWASP usadas

- OWASP Top 10:2025 lista A09 como **Security Logging and Alerting Failures**: https://owasp.org/Top10/2025/
- OWASP A09:2025 explica que sin logging, monitoreo y alertas los ataques no se detectan, y recomienda registrar fallos de login, fallos de control de acceso, transacciones de alto valor, alertas por actividad sospechosa, proteccion de integridad y honeytokens: https://owasp.org/Top10/2025/A09_2025-Security_Logging_and_Alerting_Failures/
- OWASP Logging Cheat Sheet recomienda logs de aplicacion consistentes, con eventos de seguridad, atributos de investigacion y campos de "when, where, who, what": https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html
- OWASP ASVS 5.0 V16 pide metadata investigable, eventos de autenticacion, fallos de autorizacion, eventos definidos por la aplicacion, errores inesperados y proteccion de logs: https://cornucopia.owasp.org/taxonomy/asvs-5.0/16-security-logging-and-error-handling/03-security-events

## Estado real de la implementacion

| Area | Estado | Evidencia |
|---|---|---|
| Auditoria A09 base | Implementado | `SecurityAuditService`, `SecurityEvent`, `SecurityAlert` |
| Alertas internas | Implementado | `SecurityAlertService` |
| Correlation ID | Implementado | `CorrelationIdMiddleware` |
| JWT failure logging | Implementado | `JwtBearerEvents` en `Program.cs` |
| Access denied logging | Implementado | `OnForbidden` en `Program.cs` |
| Login failed/success/disabled | Implementado | `AuthService` |
| Expediente ajeno | Implementado | `StudentRecordService` |
| Acciones admin sensibles | Implementado | `AdminService` |
| Ticket de seguridad | Implementado | `SupportTicketService` |
| Dashboard defensivo | Implementado | `SecurityController` |
| Rate limiting | Implementado | `RateLimitingMiddleware` + pruebas |
| Account lockout | Implementado | `AccountLockoutService` + pruebas |
| Desbloqueo solo Admin | Implementado en esta revision | `SecurityController` + prueba negativa |
| Integridad criptografica de logs | Pendiente | `docs/improvements/03-log-integrity.md` |
| Forwarding externo append-only/SIEM | Pendiente | `docs/improvements/04-external-log-forwarding.md` |
| Honeytoken endpoints | Pendiente | `docs/improvements/05-honeytoken-endpoint.md` |
| Impossible travel | Pendiente | `docs/improvements/06-impossible-travel.md` |
| Script Red Team automatizado | Pendiente | `docs/improvements/08-red-team-scenarios.md` |

Nota de exactitud: algunos docs antiguos mencionan SQL Server, pero el codigo actual usa PostgreSQL con Npgsql y `postgres:16` en Docker Compose.

## Acto 1 - El Concepto

### Mensaje principal

A09 no significa "no tener logs tecnicos". Significa no tener evidencia de seguridad util para responder:

- Quien hizo la accion.
- Desde donde.
- Contra que recurso.
- Que resultado obtuvo.
- Si fue una repeticion sospechosa.
- Si debe escalarse como alerta.
- Si la evidencia puede protegerse de manipulacion.

### Como funciona la vulnerabilidad

En la rama vulnerable, la API puede hacer esto:

```text
Ataque -> decision de seguridad -> 401/403 -> fin de la request
```

Pero deberia hacer esto:

```text
Ataque -> decision de seguridad -> evento -> correlacion -> alerta -> investigacion -> respuesta
```

### Impacto real

Sin A09:

- Password spraying puede quedar invisible.
- Un alumno puede sondear expedientes ajenos sin trazabilidad.
- Un usuario comprometido puede hacer cambios administrativos sin auditoria util.
- Un ticket de seguridad puede quedar como ticket normal.
- Un incidente puede descubrirse por terceros, no por el sistema.
- El equipo no puede reconstruir una linea de tiempo confiable.

### Mapeo OWASP

| Requisito | Como se refleja en este proyecto |
|---|---|
| A09:2025 logging + alerting | La rama `master` no produce eventos/alertas utiles; `fixed` si |
| ASVS V16.2 metadata | `SecurityEvent` captura usuario, IP, metodo, path, status, recurso, outcome, correlationId |
| ASVS V16.3 eventos de seguridad | Login, JWT, access denied, expediente ajeno, admin changes, errores |
| ASVS V16.4 proteccion de logs | Pendiente: hash chain y forwarding externo |
| ASVS V16.5 errores seguros | `GlobalExceptionMiddleware` evita filtrar stack traces |

## Acto 2 - El Ataque en Vivo: Red Team

### Preparacion

Usar rama vulnerable:

```bash
git switch master
docker compose up --build
```

API esperada:

```text
http://localhost:8080
```

Usuarios:

| Usuario | Password | Rol |
|---|---|---|
| admin@demo.local | Admin123! | Admin |
| auditor@demo.local | Auditor123! | Auditor |
| student1@demo.local | Student123! | Student |
| student2@demo.local | Student123! | Student |
| disabled@demo.local | Disabled123! | Student deshabilitado |

IDs demo:

```text
student1 userId: 20000000-0000-0000-0000-000000000001
student2 userId: 20000000-0000-0000-0000-000000000002
disabled userId: 20000000-0000-0000-0000-000000000003
```

### Ataque 1: password spraying

Enviar 5 veces:

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "admin@demo.local",
  "password": "PasswordIncorrecto123!"
}
```

Resultado visible:

```text
401 Unauthorized
```

Resultado defensivo esperado en vulnerable:

```text
No hay LoginFailed persistido.
No hay conteo por usuario/IP.
No hay alerta MultipleFailedLogins.
```

### Ataque 2: alumno sondea endpoint administrativo

1. Login como `student1@demo.local`.
2. Usar su token:

```http
GET /api/admin/users
Authorization: Bearer <studentToken>
```

Resultado visible:

```text
403 Forbidden
```

Resultado defensivo vulnerable:

```text
No hay evento AccessDenied.
No hay alerta AdminEndpointProbing.
```

### Ataque 3: alumno intenta expediente ajeno

```http
GET /api/student-records/by-user/20000000-0000-0000-0000-000000000002
Authorization: Bearer <studentToken>
```

Resultado visible:

```text
403 Forbidden
```

Resultado defensivo vulnerable:

```text
No hay StudentRecordAccessDenied.
No hay trazabilidad de recurso objetivo.
```

### Ataque 4: ticket marcado como seguridad

```http
POST /api/support-tickets
Authorization: Bearer <studentToken>
Content-Type: application/json

{
  "subject": "Actividad sospechosa en mi cuenta",
  "description": "Vi intentos de acceso que no reconozco.",
  "isSecurityRelevant": true
}
```

Resultado vulnerable:

```text
El ticket se crea, pero no se eleva como evento ni alerta.
```

### Verificacion final en vulnerable

Como admin o auditor:

```http
GET /api/security/events
GET /api/security/alerts
Authorization: Bearer <adminToken>
```

Resultado vulnerable:

```json
{
  "events": [],
  "a09": "Version main vulnerable: no existe almacenamiento real de eventos de seguridad."
}
```

```json
{
  "alerts": [],
  "a09": "Version main vulnerable: no existe motor de alertas ni umbrales de comportamiento sospechoso."
}
```

Frase para cerrar Acto 2:

```text
El atacante no logro entrar en todos los casos, pero el defensor tampoco logro enterarse.
```

## Acto 3 - La Causa Raiz: Analisis de Codigo en `master/main`

### Causa raiz resumida

La rama vulnerable confunde control de acceso con monitoreo. Rechazar una request no equivale a detectar un ataque.

### Lineas inseguras especificas

| Archivo en `master` | Lineas | Problema |
|---|---:|---|
| `src/ExamenSecurity.Api/Services/AuthService.cs` | 17-37 | Usuario inexistente, cuenta deshabilitada y password incorrecto devuelven `null`, pero no se auditan |
| `src/ExamenSecurity.Api/Services/AuthService.cs` | 61-70 | Password reset responde genericamente, pero no registra abuso repetido |
| `src/ExamenSecurity.Api/Controllers/SecurityController.cs` | 12-29 | Endpoints de eventos y alertas devuelven arreglos vacios |
| `src/ExamenSecurity.Api/Services/StudentRecordService.cs` | 21-27 | El acceso a expediente ajeno se bloquea, pero no queda evidencia |
| `src/ExamenSecurity.Api/Services/AdminService.cs` | 42-63 | Crear/deshabilitar usuario no deja auditoria de actor, target, razon |
| `src/ExamenSecurity.Api/Services/AdminService.cs` | 89-116 | Deshabilitar API client y cambiar configuracion no generan evento/alerta |
| `src/ExamenSecurity.Api/Services/SupportTicketService.cs` | 25-28 | Ticket de seguridad no genera alerta |
| `src/ExamenSecurity.Api/Program.cs` | 51-54 | Fallos JWT se rechazan sin persistir evento ni correlacion |

### Como mostrarlo en vivo

Usar:

```bash
git show master:src/ExamenSecurity.Api/Services/AuthService.cs
git show master:src/ExamenSecurity.Api/Controllers/SecurityController.cs
git show master:src/ExamenSecurity.Api/Program.cs
```

Explicacion:

```text
El problema no es que falte un if. El problema es que no existe un pipeline de auditoria:
no hay entidad de evento, no hay servicio central, no hay enrichment de request,
no hay severidad, no hay correlation ID, no hay reglas de alerta.
```

## Acto 4 - La Solucion: Blue Team en `fixed`

### Preparacion

Usar rama defensiva:

```bash
git switch fixed
docker compose up --build
```

Para no mezclar defensas durante la demo:

- Si quieres demostrar A09 puro, usa una IP whitelisted o desactiva temporalmente rate limiting.
- Si quieres demostrar rate limiting, usa una IP simulada no whitelisted con `X-Forwarded-For: 203.0.113.10`.
- Si quieres demostrar lockout, puedes desactivar `RateLimiting:Enabled=false` para que el sexto login muestre `423 Locked` en vez de `429`.

### Arquitectura defensiva

```text
Request
  -> CorrelationIdMiddleware
  -> RateLimitingMiddleware
  -> Authentication/Authorization
  -> Servicio de dominio
  -> SecurityAuditService
  -> SecurityEvents
  -> SecurityAlertService
  -> SecurityAlerts
  -> AccountLockoutService cuando aplica
```

### Codigo corregido clave

| Archivo en `fixed` | Lineas | Defensa |
|---|---:|---|
| `src/ExamenSecurity.Api/Services/SecurityAuditService.cs` | 42-66 | Construye `SecurityEvent`, enriquece con request, persiste y evalua alertas |
| `src/ExamenSecurity.Api/Services/SecurityAuditService.cs` | 17-25, 123-140 | Redacta metadata sensible como password/token/secret |
| `src/ExamenSecurity.Api/Program.cs` | 74-120 | Registra JWT invalido, request no autenticada y `403 Forbidden` |
| `src/ExamenSecurity.Api/Services/AuthService.cs` | 47-55 | Login de usuario inexistente genera `LoginFailed` |
| `src/ExamenSecurity.Api/Services/AuthService.cs` | 78-107 | Cuenta deshabilitada o password incorrecto generan eventos |
| `src/ExamenSecurity.Api/Services/AuthService.cs` | 116-126 | Login exitoso genera `LoginSucceeded` |
| `src/ExamenSecurity.Api/Services/StudentRecordService.cs` | 41-59 | Expediente ajeno genera `StudentRecordAccessDenied` |
| `src/ExamenSecurity.Api/Services/AdminService.cs` | 42-57, 73-88, 114-168 | Acciones admin generan eventos de seguridad |
| `src/ExamenSecurity.Api/Services/SupportTicketService.cs` | 25-42 | Ticket de seguridad genera `SecurityTicketCreated` |
| `src/ExamenSecurity.Api/Services/SecurityAlertService.cs` | 17-58 | Enruta eventos hacia reglas de alerta |
| `src/ExamenSecurity.Api/Services/SecurityAlertService.cs` | 77-107 | 5 login fallidos generan `MultipleFailedLogins` |
| `src/ExamenSecurity.Api/Services/SecurityAlertService.cs` | 140-197 | Admin probing y record probing generan alertas |
| `src/ExamenSecurity.Api/Services/SecurityAlertService.cs` | 267-328 | Alertas de login disparan lockout temporal |
| `src/ExamenSecurity.Api/Middleware/RateLimitingMiddleware.cs` | 82-104 | Exceso de requests devuelve `429` |
| `src/ExamenSecurity.Api/Middleware/RateLimitingMiddleware.cs` | 133-159 | Exceso de rate limit queda auditado |
| `src/ExamenSecurity.Api/Services/AccountLockoutService.cs` | 95-110 | Bloqueo queda auditado como `AccountLocked` |
| `src/ExamenSecurity.Api/Services/AccountLockoutService.cs` | 158-173 | Desbloqueo queda auditado como `AccountUnlocked` |
| `src/ExamenSecurity.Api/Controllers/SecurityController.cs` | 21-178 | Consulta paginada y filtrada de eventos/alertas |
| `src/ExamenSecurity.Api/Controllers/SecurityController.cs` | 230-271 | Dashboard y readiness defensivo |
| `src/ExamenSecurity.Api/Controllers/SecurityController.cs` | 293-303 | Unlock de lockout solo para `Admin` |

### Repeticion del ataque en `fixed`

#### Password spraying

Enviar 5 login fallidos.

Resultado esperado:

```text
401 Unauthorized
SecurityEvents: 5 x LoginFailed
SecurityAlerts: 1 x MultipleFailedLogins
AccountLockouts: usuario/IP bloqueados si lockout esta habilitado
```

Verificar:

```http
GET /api/security/events?eventType=LoginFailed
Authorization: Bearer <adminToken>
```

```http
GET /api/security/alerts?onlyUnacknowledged=true
Authorization: Bearer <adminToken>
```

#### Alumno contra admin

Repetir 3 veces:

```http
GET /api/admin/users
Authorization: Bearer <studentToken>
```

Resultado esperado:

```text
403 Forbidden
SecurityEvents: AccessDenied
SecurityAlerts: AdminEndpointProbing si llega al umbral
```

#### Expediente ajeno

Repetir 2 veces:

```http
GET /api/student-records/by-user/20000000-0000-0000-0000-000000000002
Authorization: Bearer <studentToken>
```

Resultado esperado:

```text
403 Forbidden
SecurityEvents: StudentRecordAccessDenied
SecurityAlerts: StudentRecordProbing
```

#### Ticket de seguridad

```http
POST /api/support-tickets
Authorization: Bearer <studentToken>
Content-Type: application/json

{
  "subject": "Actividad sospechosa en mi cuenta",
  "description": "Vi intentos de acceso que no reconozco.",
  "isSecurityRelevant": true
}
```

Resultado esperado:

```text
SecurityEvent: SecurityTicketCreated
SecurityAlert: SecurityTicketRequiresReview
```

### Evidencia que debe mostrarse

Campos importantes de `SecurityEvent`:

```text
eventType, severity, userId, username, role, ipAddress, userAgent,
httpMethod, path, statusCode, resourceType, resourceId,
outcome, message, metadataJson, correlationId, createdAtUtc
```

Campos importantes de `SecurityAlert`:

```text
alertType, severity, title, description, relatedUserId,
relatedUsername, relatedIpAddress, eventCount, isAcknowledged, createdAtUtc
```

Dashboard:

```http
GET /api/security/dashboard-summary
Authorization: Bearer <auditorToken>
```

Readiness:

```http
GET /api/security/observability-readiness
Authorization: Bearer <auditorToken>
```

## Mejoras implementadas ademas del A09 base

### Mejora 01: Rate limiting

Implementado:

- `RateLimitingMiddleware`.
- Reglas por Auth/Admin/General.
- `429 Too Many Requests`.
- Headers `Retry-After`, `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`.
- Evento `RateLimitExceeded`.
- Configuracion en `appsettings.json`.
- Pruebas de integracion.

Limitacion honesta:

```text
El middleware confia en X-Forwarded-For para la demo. En produccion solo debe confiarse en ese header detras de un reverse proxy configurado con KnownProxies/KnownNetworks.
```

### Mejora 02: Account lockout

Implementado:

- Entidad `AccountLockout`.
- Servicio `IAccountLockoutService`.
- Bloqueo por usuario/IP.
- Duracion configurable.
- Backoff exponencial con maximo de 8 horas.
- `423 Locked`.
- Endpoints `GET /api/security/lockouts` y `POST /api/security/lockouts/{id}/unlock`.
- Desbloqueo manual restringido a `Admin`.
- Eventos `AccountLocked`, `AccountUnlocked`, `LockoutAttemptDuringLock`.
- Pruebas de integracion.

### Mejora 07: pruebas

Implementado parcialmente:

- Proyecto `tests/ExamenSecurity.Api.Tests`.
- `WebApplicationFactory<Program>`.
- DB InMemory por prueba.
- 14 pruebas actuales.
- Cobertura actual: rate limiting, headers, whitelist, configuracion, lockout, desbloqueo, intento durante bloqueo y autorizacion de unlock.

Pendiente:

- Pruebas completas del pipeline A09 base: `LoginSucceeded`, `LoginFailed`, `AccessDenied`, `StudentRecordAccessDenied`, alertas por umbral, ticket de seguridad.
- Pruebas para integridad de logs, forwarding externo, honeytokens, impossible travel y script Red Team.

## Pendientes OWASP para una version mas fuerte

### 1. Integridad criptografica de logs

Objetivo:

```text
Evitar que un atacante con acceso a la base de datos pueda modificar o borrar eventos sin deteccion.
```

Implementar:

- `EventHash`, `PreviousHash`, `KeyVersion`.
- HMAC-SHA512, no hash simple.
- Cadena cronologica de eventos.
- `GET /api/security/integrity-check`.
- Pruebas de tampering, eliminacion e insercion fraudulenta.

Valor OWASP:

```text
Cumple mejor ASVS V16.4 y A09:2025 sobre proteccion de integridad.
```

### 2. Forwarding externo append-only

Objetivo:

```text
Separar la evidencia de la base operativa para que un compromiso de la app no borre todos los rastros.
```

Implementar:

- `IExternalLogForwarder`.
- Archivo `.jsonl` append-only.
- Stdout JSON estructurado.
- Outbox/reintentos.
- Rotacion por tamano.
- Sanitizacion consistente.

### 3. Honeytoken endpoints

Objetivo:

```text
Crear endpoints senuelo que ningun usuario legitimo deberia tocar. Cualquier acceso genera alerta critica.
```

Implementar:

- `GET /api/internal/backup`.
- `GET /api/internal/logs/debug`.
- `GET /api/admin/config/secrets`.
- Evento `HoneytokenTriggered`.
- Alerta `HoneytokenAccessed`.
- Respuesta falsa, nunca datos reales.

### 4. Impossible travel

Objetivo:

```text
Detectar credenciales comprometidas cuando un usuario inicia sesion desde ubicaciones incompatibles en poco tiempo.
```

Implementar:

- `IGeoLocationService`.
- `UserLoginLocation`.
- Calculo Haversine.
- Evento `ImpossibleTravelDetected`.
- Alerta `ImpossibleTravel`.

### 5. Script Red Team

Objetivo:

```text
Automatizar la demo y producir reporte Markdown.
```

Implementar:

- `redteam/main.py`.
- Modos `fixed` y `vulnerable`.
- Escenarios: password spraying, admin probing, record probing, rate limit, lockout, honeytoken, integrity, security ticket, impossible travel.
- Reporte terminal + Markdown.

### 6. Hardening de produccion

Implementar antes de considerar esto listo para produccion:

- Secrets fuera de `appsettings.json`.
- `X-Forwarded-For` solo desde proxies confiables.
- Rate limiting distribuido con Redis si hay multiples instancias.
- Migraciones EF Core en vez de `EnsureCreated`.
- Retencion y control de acceso formal para logs.
- SIEM o agregador externo.
- Playbooks de respuesta.
- Politica de minimizacion de PII en logs.

## Riesgos residuales actuales

| Riesgo | Severidad | Explicacion | Recomendacion |
|---|---|---|---|
| `X-Forwarded-For` confiado directamente | Alta en produccion | Un cliente podria spoofear IP para evadir rate limiting/lockout | Usar Forwarded Headers solo con proxies confiables |
| Logs solo en DB operativa | Alta | Si comprometen DB, pueden alterar evidencia | Hash chain + forwarding externo |
| Sin integridad HMAC | Alta | No se detecta tampering de eventos | Implementar mejora 03 |
| Sin SIEM/notificacion externa | Media | Las alertas existen, pero alguien debe consultarlas | Forwarding + playbooks |
| Rate limiting en memoria | Media | No funciona distribuido en multiples replicas | Redis/distributed cache |
| Secretos demo en config | Media | Aceptable para laboratorio, no para produccion | Variables de entorno/secret manager |
| Docs antiguos mencionan SQL Server | Baja | El codigo actual usa PostgreSQL | Alinear documentacion |

## Plan de slides para Canva

| Slide | Titulo | Objetivo | Visual recomendado |
|---:|---|---|---|
| 1 | OWASP A09: de app funcional a app observable | Presentar tema y tesis | Fondo de consola/API + alerta |
| 2 | La tesis | Explicar que `401/403` no es deteccion | Diagrama "bloquear vs observar" |
| 3 | Que dice OWASP A09:2025 | Marco teorico | 3 tarjetas: logging, alerting, response |
| 4 | Impacto real | Mostrar riesgo de negocio | Timeline de incidente invisible |
| 5 | Sistema de laboratorio | Contexto UABC/FIAD, roles y datos demo | Mapa API: auth, admin, records, support |
| 6 | Red Team: password spraying | Primer ataque | Request -> 401 -> sin evento |
| 7 | Red Team: admin probing | Segundo ataque | Student token -> `/api/admin/users` -> 403 |
| 8 | Red Team: expediente ajeno | Tercer ataque | Student1 -> Student2 record -> 403 |
| 9 | Red Team: evidencia vacia | Remate del ataque | `events: []`, `alerts: []` |
| 10 | Causa raiz | Explicar ausencia de pipeline | Diagrama con hueco entre decision y evidencia |
| 11 | Codigo vulnerable | Lineas de `AuthService`, `SecurityController`, `Program` | Screenshot de IDE con lineas resaltadas |
| 12 | Blue Team: diseno defensivo | Mostrar arquitectura fixed | Pipeline request -> event -> alert |
| 13 | Codigo corregido | `SecurityAuditService`, `Program`, `SecurityAlertService` | Screenshot con lineas clave |
| 14 | Repetimos el ataque | Mostrar que el ataque ya no es invisible | Tabla main vs fixed |
| 15 | Evidencia generada | Campos de evento y alerta | JSON parcial de `SecurityEvents` |
| 16 | Defensa reactiva | Rate limiting + account lockout | 429 + 423 + lockout activo |
| 17 | Validacion | Pruebas 14/14 | Captura de `dotnet test` |
| 18 | Roadmap OWASP | Pendientes honestos | Checklist: integrity, forwarding, honeytokens, impossible travel |
| 19 | Cierre | Mensaje final | Frase: "Defender no es solo bloquear; es ver, correlacionar y responder." |

## Guion breve por acto

### Acto 1

```text
Nuestra vulnerabilidad es A09: Security Logging and Alerting Failures.
La app vulnerable no esta totalmente abierta: tiene login, roles y 403.
El problema es que cuando ocurre actividad sospechosa, no queda evidencia accionable.
OWASP lo considera critico porque sin logging y alertas no hay deteccion, investigacion ni respuesta.
```

### Acto 2

```text
Vamos a actuar como Red Team. Haremos password spraying, sondeo admin y acceso a expediente ajeno.
La API responde 401 o 403, pero al consultar eventos y alertas veremos arreglos vacios.
Ese es el punto: el atacante genera senales, pero el sistema no las convierte en evidencia.
```

### Acto 3

```text
La causa raiz aparece en el codigo. En AuthService los fallos regresan null.
En SecurityController no hay storage real: solo arrays vacios.
En Program los JWT invalidos se rechazan sin evento.
En StudentRecordService el acceso ajeno se bloquea, pero no se audita.
No falta un endpoint: falta una arquitectura de observabilidad de seguridad.
```

### Acto 4

```text
En fixed agregamos SecurityAuditService y SecurityAlertService.
Ahora los mismos ataques generan eventos con usuario, IP, endpoint, status, recurso, severidad y correlation ID.
Las reglas crean alertas por patrones: 5 logins fallidos, 3 accesos admin denegados, 2 expedientes ajenos.
Ademas, se agrego rate limiting y lockout temporal para pasar de observar a reaccionar.
Repetimos el ataque y ya no es invisible.
```

## Tabla comparativa final

| Prueba | `master/main` vulnerable | `fixed` defensivo |
|---|---|---|
| Login fallido x5 | Solo `401` | `LoginFailed` + `MultipleFailedLogins` |
| Cuenta deshabilitada | Solo `401` | `DisabledAccountLoginAttempt` + posible alerta |
| Student contra admin | Solo `403` | `AccessDenied` + `AdminEndpointProbing` |
| Student contra expediente ajeno | Solo `403` | `StudentRecordAccessDenied` + `StudentRecordProbing` |
| Ticket de seguridad | Ticket normal | `SecurityTicketCreated` + alerta |
| JWT invalido | Solo rechazo | `TokenAuthenticationFailed` |
| Error no controlado | Error tecnico | `UnhandledException` sin stack trace al cliente |
| Flood de requests | Sin limite | `429` + `RateLimitExceeded` |
| Ataque repetido de login | Sigue intentando | `423 Locked` por usuario/IP |

## Respuestas para preguntas tecnicas

**Por que no basta con `401` y `403`?**  
Porque protegen el recurso inmediato, pero no dejan evidencia para correlacionar abuso, investigar ni responder.

**Que hace diferente la rama `fixed`?**  
Centraliza la auditoria, agrega metadata de request, persiste eventos, evalua reglas, genera alertas y expone endpoints de investigacion protegidos.

**Por que no se guardan passwords o tokens?**  
Porque los logs son datos sensibles. La implementacion redacts keys como `password`, `token`, `authorization`, `secret`, `signingkey` y `credential`.

**Esto ya cumple todo OWASP A09?**  
Cumple la parte central de logging y alerting de aplicacion para la demo. Para produccion faltan integridad de logs, forwarding externo, SIEM/playbooks, control formal de retencion y proteccion frente a tampering.

**Cual es el hallazgo mas importante pendiente?**  
La evidencia aun vive en la base operativa. Si la base se compromete, el atacante podria modificarla. Por eso la siguiente mejora fuerte es hash chain con HMAC y forwarding externo append-only.

**Por que usar PostgreSQL si docs antiguos dicen SQL Server?**  
El codigo actual usa `Npgsql.EntityFrameworkCore.PostgreSQL` y Docker Compose levanta `postgres:16`. La mencion a SQL Server es deuda documental antigua.

## Cierre recomendado

```text
La rama vulnerable demuestra una aplicacion que bloquea algunas acciones, pero no aprende de ellas.
La rama fixed convierte esas senales en evidencia, alertas y reaccion.
La leccion OWASP es directa: defender no es solo impedir; defender es poder ver, correlacionar y responder.
```
