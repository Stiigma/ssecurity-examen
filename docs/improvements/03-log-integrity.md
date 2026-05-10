# Mejora 03: Log Integrity (Integridad de Logs de Seguridad)

## Alcance

Implementar un mecanismo que garantice la integridad e inmutabilidad de los eventos de seguridad (`SecurityEvent`) almacenados en la base de datos. El objetivo es que un atacante con acceso a la base de datos (o incluso un administrador malicioso) no pueda eliminar, modificar o insertar eventos sin dejar rastro criptografico detectable.

Esta mejora cubre:
- Agregar un hash criptografico por cada evento de seguridad que dependa del contenido del evento anterior (hash chain / cadena de hashes).
- Un endpoint de verificacion de integridad que recorra todos los eventos y valide que la cadena de hashes no esta rota.
- Deteccion de eventos faltantes (gaps en IDs secuenciales o de tiempo).
- Integracion transparente con `SecurityAuditService` para que cada evento nuevo calcule su hash automaticamente.

## Lo que se tiene que implementar

1. **Entidad `SecurityEvent` - nuevos campos**
   - `EventHash`: string de 128 caracteres (SHA-512 en hexadecimal) o 88 caracteres (Base64). Almacena el hash del evento actual.
   - `PreviousHash`: string opcional que almacena el `EventHash` del evento inmediatamente anterior en la cadena.
   - `HashInput`: campo calculado internamente con todos los campos relevantes del evento concatenados en orden fijo (para evitar que alguien modifique un campo y recalcule el hash facilmente).

2. **Servicio `ISecurityLogIntegrityService` / `SecurityLogIntegrityService`**
   - `ComputeHashAsync(securityEvent, previousEvent)`: calcula el hash SHA-512 del evento actual combinado con el hash anterior.
     - Input del hash debe incluir (en orden exacto): `EventType`, `Severity`, `UserId`, `Username`, `IpAddress`, `Path`, `StatusCode`, `Outcome`, `Message`, `MetadataJson`, `CorrelationId`, `CreatedAtUtc` (formato ISO 8601), y `PreviousHash`.
   - `VerifyChainAsync(fromUtc?, toUtc?)`: recorre todos los eventos en orden cronologico y verifica que cada `EventHash` coincida con el recalculo y que `PreviousHash` coincida con el `EventHash` del evento anterior.
     - Retorna un objeto con: `IsValid`, `TotalEventsChecked`, `FirstBrokenEventId` (si aplica), `MissingEventIds` (gaps detectados), y `ComputedHashVsStoredHash` (para debug).
   - Debe usarse una **clave secreta** (HMAC, no hash simple) para que un atacante que vea la DB no pueda recalcular hashes validos por su cuenta.

3. **Integracion con `SecurityAuditService`**
   - En el metodo `AuditAsync`, justo antes de `dbContext.SaveChangesAsync()`:
     1. Obtener el ultimo evento de la cadena (el mas reciente por `CreatedAtUtc`).
     2. Calcular `PreviousHash` = `EventHash` del ultimo evento (o string vacio si es el primero).
     3. Llamar a `SecurityLogIntegrityService.ComputeHashAsync` para generar `EventHash`.
     4. Asignar ambos valores al evento actual.
     5. Guardar.

4. **Endpoint de verificacion de integridad**
   - `GET /api/security/integrity-check` (solo Admin/Auditor).
   - Devuelve el resultado del `VerifyChainAsync` con un resumen visual.
   - Si la cadena esta rota, devolver `409 Conflict` o `200 OK` con `isValid: false` y detalles del problema.
   - Debe auditarse como evento `IntegrityCheckPerformed`.

5. **Migracion de datos existentes**
   - Si ya existen eventos sin hash en la base de datos:
     - Opcion A: marcarlos como `EventHash: "LEGACY_NO_HASH"` y `PreviousHash: null`, comenzando la cadena desde el primer evento nuevo.
     - Opcion B: migracion one-time que recorra todos los eventos historicos y calcule hashes retroactivamente (mas seguro, pero mas lento).
   - Se recomienda Opcion B para la demo, ya que la base de datos es pequena.

## Que queda fuera

- **Firmas digitales con certificados PKI/X.509**: No se usaran certificados ni infraestructura de clave publica. Solo HMAC con una clave simetrica.
- **Blockchain / ledger distribuido**: No se implementara un blockchain real. La cadena de hashes es un concepto similar pero local a la base de datos.
- **Replicacion a sistema externo WORM (Write Once Read Many)**: No se escribira a hardware o storage WORM externo. La proteccion es puramente criptografica dentro de SQL Server.
- **Encriptacion de logs en reposo**: Los eventos siguen siendo legibles en la base de datos. Solo se agrega integridad, no confidencialidad.
- **Deteccion de eventos duplicados**: El verificador detecta gaps, no duplicados exactos.

## Criterio de aceptacion

- [ ] Todo nuevo `SecurityEvent` tiene un `EventHash` y `PreviousHash` calculados automaticamente por `SecurityAuditService`.
- [ ] El hash usa HMAC-SHA512 con una clave secreta configurable en `appsettings.json` (no hardcodeada).
- [ ] El endpoint `GET /api/security/integrity-check` devuelve `isValid: true` cuando la cadena esta intacta.
- [ ] Si se modifica manualmente un campo de un evento existente en la base de datos (ej. cambiar `IpAddress` via SQL), el `integrity-check` detecta la inconsistencia y devuelve `isValid: false` con el ID del evento comprometido.
- [ ] Si se elimina un evento de la base de datos manualmente, el `integrity-check` detecta el gap y reporta `missingEventIds`.
- [ ] Si se inserta un evento manualmente con un hash invalido, el `integrity-check` lo detecta.
- [ ] La clave HMAC puede rotarse: si se cambia la clave en `appsettings.json`, los eventos NUEVOS usan la nueva clave, y el verificador usa la clave correcta segun la fecha del evento (o se almacena un `KeyVersion` por evento).
- [ ] La migracion de eventos historicos (si existen) se realiza correctamente, generando hashes retroactivos con una clave de migracion.
- [ ] Existen pruebas de integracion que validan: cadena valida, deteccion de modificacion, deteccion de eliminacion, y deteccion de insercion fraudulenta.
