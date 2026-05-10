# Mejora 07: Integration Tests Suite (Suite de Pruebas de Integracion)

## Alcance

Implementar una suite completa de pruebas de integracion que validen el pipeline completo de seguridad del backend. Estas pruebas deben ejecutarse contra la aplicacion real levantada en memoria (TestServer / WebApplicationFactory) con una base de datos real (SQL Server LocalDB, SQLite in-memory, o TestContainers SQL Server).

El objetivo es garantizar que:
- Los eventos de seguridad se generan correctamente.
- Las alertas se disparan con los umbrales correctos.
- Los bloqueos (rate limiting, account lockout) funcionan segun las reglas.
- La integridad de logs (hash chain) es verificable.
- Los endpoints senoelo (honeytokens) generan alertas criticas.
- Todo el flujo defensivo es reproducible y automatizable.

## Lo que se tiene que implementar

1. **Proyecto de pruebas nuevo**
   - Crear `tests/ExamenSecurity.Api.IntegrationTests/ExamenSecurity.Api.IntegrationTests.csproj`.
   - Referenciar el proyecto `ExamenSecurity.Api`.
   - Dependencias:
     - `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory).
     - `Microsoft.EntityFrameworkCore.InMemory` O `Testcontainers.MsSql` para base de datos real.
     - `xunit`.
     - `FluentAssertions`.
     - `Bogus` (opcional, para generar datos de prueba).
   - **Recomendacion**: Usar `Testcontainers.MsSql` para tener SQL Server real en Docker durante las pruebas. Esto permite probar indices, queries complejas, y migraciones reales.

2. **Clase base `IntegrationTestBase`**
   - Configurar `WebApplicationFactory<Program>` con:
     - Override de `ConnectionStrings:DefaultConnection` apuntando al contenedor SQL Server de Testcontainers.
     - Override de `Jwt:SigningKey` con una clave fija para pruebas.
     - Override de `RateLimiting` para ventanas cortas (ej. 5 segundos en vez de 60) para que las pruebas sean rapidas.
     - Seed de datos de prueba: al menos un admin, un student, un auditor, y un ApiClient.
   - Metodos helper:
     - `GetAdminTokenAsync()`, `GetStudentTokenAsync()`, `GetAuditorTokenAsync()`: loguean y retornan JWT.
     - `GetDbContext()`: retorna `AppDbContext` para hacer asserts directos en la base de datos.
     - `ResetDatabaseAsync()`: limpia SecurityEvents, SecurityAlerts, AccountLockouts entre pruebas para evitar contaminacion.
     - `WaitForRateLimitResetAsync()`: espera a que expire la ventana de rate limiting.

3. **Casos de prueba obligatorios**

   ### 3.1 Pipeline de autenticacion y eventos
   - `LoginSuccess_CreatesSecurityEvent`: login valido -> evento `LoginSucceeded` en DB.
   - `LoginFailure_CreatesSecurityEvent`: login invalido -> evento `LoginFailed` en DB.
   - `LoginDisabledAccount_CreatesDisabledAccountEvent`: login a cuenta deshabilitada -> evento `DisabledAccountLoginAttempt`.
   - `JwtTampered_Returns401_AndCreatesTokenAuthenticationFailed`: token con firma invalida -> `TokenAuthenticationFailed`.
   - `UnauthorizedRequest_CreatesEvent`: request a endpoint protegido sin token -> `UnauthorizedRequest`.
   - `ForbiddenRequest_CreatesEvent`: request a endpoint admin con token de student -> `AccessDenied`.

   ### 3.2 Umbrales de alertas
   - `FiveLoginFailures_CreatesAlert_MultipleFailedLogins`: 5 POST /api/auth/login fallidos -> alerta `MultipleFailedLogins`.
   - `TwoDisabledAccountAttempts_CreatesAlert_DisabledAccountTargeted`: 2 logins a cuenta deshabilitada -> alerta `DisabledAccountTargeted`.
   - `ThreeAdminAccessDenied_CreatesAlert_AdminEndpointProbing`: 3 GET /api/admin/users con token de student -> alerta `AdminEndpointProbing`.
   - `TwoStudentRecordAccessDenied_CreatesAlert_StudentRecordProbing`: 2 GET /api/student-records/by-user/{otroId} con token de student -> alerta `StudentRecordProbing`.
   - `SecurityTicket_CreatesAlert_SecurityTicketRequiresReview`: POST /api/support-tickets con `isSecurityRelevant=true` -> alerta.

   ### 3.3 Rate limiting (Mejora 01)
   - `SixLoginAttempts_Returns429`: 6 logins fallidos en menos de 1 minuto -> el sexto devuelve `429`.
   - `RateLimitHeaders_Present`: respuesta exitosa incluye `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`.
   - `HealthCheck_ExemptFromRateLimit`: GET /api/demo/health no se bloquea incluso despues de muchos requests.

   ### 3.4 Account lockout (Mejora 02)
   - `FiveLoginFailures_LocksAccount`: 5 logins fallidos -> cuenta bloqueada -> sexto intento devuelve `423 Locked`.
   - `LockedAccount_AuditEvent`: intento de login estando bloqueado -> evento `LockoutAttemptDuringLock`.
   - `AdminCanUnlockAccount`: admin llama POST /api/security/lockouts/{id}/unlock -> cuenta desbloqueada.

   ### 3.5 Log integrity (Mejora 03)
   - `IntegrityCheck_ValidChain`: despues de N eventos, `GET /api/security/integrity-check` devuelve `isValid: true`.
   - `IntegrityCheck_DetectsTampering`: modificar un evento directamente en DB (via raw SQL en la prueba) -> integrity check devuelve `isValid: false`.
   - `IntegrityCheck_DetectsMissingEvent`: eliminar un evento de la DB -> integrity check detecta gap.

   ### 3.6 External log forwarding (Mejura 04)
   - `EventForwardedToFile`: despues de un evento, existe una linea JSON valida en el archivo `.jsonl`.
   - `EventForwardedToStdout`: (opcional, si se puede capturar console output en prueba).
   - `Sanitization_Applied`: evento con metadata que contiene "password" -> en el archivo aparece `***redacted***`.

   ### 3.7 Honeytoken (Mejora 05)
   - `HoneytokenRequest_CreatesCriticalEvent`: GET /api/internal/backup -> evento `HoneytokenTriggered` con severidad `Critical`.
   - `HoneytokenRequest_CreatesCriticalAlert`: GET /api/internal/backup -> alerta `HoneytokenAccessed` con severidad `Critical`.
   - `HoneytokenResponse_NoRealData`: la respuesta NO contiene connection strings, passwords reales, ni datos de usuarios.

   ### 3.8 Impossible travel (Mejora 06)
   - `ImpossibleTravel_CreatesAlert`: simular login desde Mexico y 5 minutos despues desde Argentina -> alerta `ImpossibleTravel`.
   - `NormalTravel_NoAlert`: simular login desde Mexico y 24 horas despues desde Espana -> NO hay alerta.
   - `LocalIp_Ignored`: login desde `127.0.0.1` dos veces -> NO se evalua impossible travel.

4. **Pipeline CI / script de ejecucion**
   - Script `run-integration-tests.sh` (Linux/Mac) o `run-integration-tests.ps1` (Windows) que:
     1. Levanta SQL Server en Docker (si no se usa Testcontainers).
     2. Ejecuta `dotnet test`.
     3. Genera reporte en formato TRX o HTML.
     4. Apaga el contenedor.
   - Archivo `Makefile` o tarea en `package.json` (si aplica) para facilitar ejecucion.

5. **Datos de prueba y seeding**
   - Usar `DbSeeder` existente pero con bandera `IsTestEnvironment = true`.
   - O crear `TestDataSeeder` que:
     - Crea admin@demo.local / Admin123!
     - Crea student1@demo.local / Student123!
     - Crea auditor@demo.local / Auditor123!
     - Crea un registro de estudiante vinculado a student1.
     - Crea un ApiClient habilitado.

## Que queda fuera

- **Pruebas unitarias puras**: Esta suite es de integracion. Pruebas unitarias de servicios individuales quedan fuera (pueden agregarse en otro proyecto).
- **Pruebas de carga / performance**: No se incluiran benchmarks con k6, JMeter, o Bombardier. Solo pruebas funcionales.
- **Pruebas de seguridad dinamica (DAST)**: No se incluyen scanners como OWASP ZAP automatizados. Las pruebas son assertions explicitos.
- **Pruebas del frontend**: Solo backend API.
- **Pruebas de infraestructura (Kubernetes, Docker Compose)**: Solo la aplicacion .NET + DB.

## Criterio de aceptacion

- [ ] El proyecto `ExamenSecurity.Api.IntegrationTests` compila sin errores.
- [ ] Todas las pruebas listadas en la seccion 3 ejecutan exitosamente (verde).
- [ ] Las pruebas usan una base de datos aislada (no la base de datos de desarrollo).
- [ ] Las pruebas son deterministas: ejecutarlas 10 veces produce el mismo resultado.
- [ ] El tiempo total de ejecucion de la suite es menor a 5 minutos.
- [ ] Hay un script o comando simple para ejecutar todas las pruebas (`dotnet test` o script de shell).
- [ ] El reporte de pruebas indica claramente que paso y que fallo.
- [ ] Si una prueba falla, el mensaje de error es descriptivo (ej. "Expected alert MultipleFailedLogins but found 0 alerts in database").
- [ ] La suite se ejecuta exitosamente en el entorno de la clase (Docker disponible o SQLite in-memory como fallback).
