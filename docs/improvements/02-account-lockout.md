# Mejora 02: Account Lockout (Bloqueo Temporal de Cuentas e IPs)

## Alcance

Implementar un mecanismo de bloqueo temporal que se active automaticamente cuando se detecten patrones de ataque identificados por el sistema de alertas. La idea es pasar de "solo observar" a "reaccionar": cuando `SecurityAlertService` genera una alerta, el sistema debe poder tomar acciones defensivas automaticas.

Esta mejora cubre:
- Bloqueo temporal de una cuenta de usuario despues de N intentos fallidos de login.
- Bloqueo temporal de una direccion IP despues de generar alertas criticas.
- Integracion con el sistema de alertas existente (`SecurityAlertService`) para disparar bloqueos.
- Endpoints para que administradores puedan ver bloqueos activos y desbloquear manualmente.

## Lo que se tiene que implementar

1. **Entidad `AccountLockout`**
   - Campos: `Id`, `TargetType` (User / IpAddress), `TargetValue` (email o IP), `Reason` (alerta que disparo el bloqueo), `LockedUntilUtc`, `CreatedAtUtc`, `IsActive`, `AlertId` (referencia opcional).
   - Tabla en SQL Server con indices en `TargetValue` y `LockedUntilUtc`.

2. **Servicio `IAccountLockoutService` / `AccountLockoutService`**
   - `LockAsync(targetType, targetValue, duration, reason, alertId)`: crea un bloqueo.
   - `IsLockedAsync(targetValue)`: verifica si un usuario o IP esta bloqueado.
   - `UnlockAsync(lockoutId, adminUserId)`: desbloqueo manual por admin.
   - `GetActiveLockoutsAsync()`: lista bloqueos activos.
   - Debe ejecutarse automaticamente cuando `SecurityAlertService` cree ciertas alertas (ej. `MultipleFailedLogins`).

3. **Integracion con `AuthService` y `SecurityAlertService`**
   - En `AuthService.LoginAsync`, antes de validar credenciales, verificar si la cuenta o la IP estan bloqueadas. Si lo estan, devolver `423 Locked` o `429 Too Many Requests` con mensaje apropiado.
   - En `SecurityAlertService`, despues de crear una alerta `MultipleFailedLogins` o `DisabledAccountTargeted`, disparar `AccountLockoutService.LockAsync` automaticamente para la IP y/o usuario involucrado.
   - Duracion sugerida: 15 minutos para el primer bloqueo, escalando exponencialmente.

4. **Endpoint para administracion de bloqueos**
   - `GET /api/security/lockouts`: lista bloqueos activos (solo Admin/Auditor).
   - `POST /api/security/lockouts/{id}/unlock`: desbloqueo manual (Admin). Debe auditarse como evento de seguridad.

5. **Evento de seguridad nuevo**
   - `AccountLocked`: cuando se bloquea una cuenta/IP.
   - `AccountUnlocked`: cuando un admin desbloquea manualmente.
   - `LockoutAttemptDuringLock`: si alguien intenta login estando bloqueado (severidad Warning).

## Que queda fuera

- **Bloqueo permanente**: Todos los bloqueos son temporales con expiracion automatica. Banear permanentemente requiere intervencion manual.
- **Desbloqueo automatico por email/SMS**: No se enviaran notificaciones externas para desbloquear. El usuario debe esperar o contactar al admin.
- **CAPTCHA**: No se implementara un desafio visual. Solo bloqueo temporal puro.
- **Bloqueo por dispositivo/browser fingerprint**: Solo se bloquea por cuenta de usuario o por IP.

## Criterio de aceptacion

- [ ] Despues de 5 logins fallidos, la cuenta queda bloqueada por 15 minutos. Cualquier intento de login posterior devuelve `423 Locked` con mensaje "Cuenta bloqueada temporalmente por seguridad".
- [ ] La IP desde la cual ocurrieron los 5 logins fallidos tambien queda bloqueada por 15 minutos para el endpoint de login.
- [ ] Un administrador puede ver la lista de bloqueos activos en `GET /api/security/lockouts`.
- [ ] Un administrador puede desbloquear una cuenta/IP manualmente y la accion queda auditada como evento `AccountUnlocked`.
- [ ] Si un atacante intenta login estando bloqueado, se genera un evento `LockoutAttemptDuringLock`.
- [ ] Los bloqueos expiran automaticamente (no requieren cron job, se evalua en cada request).
- [ ] La duracion del bloqueo es configurable en `appsettings.json`.
- [ ] El bloqueo no afecta endpoints que no sean de autenticacion (ej. health checks).
- [ ] Existen pruebas de integracion que validan el flujo completo: login fallido x5 -> alerta -> bloqueo -> 423 -> desbloqueo manual.
