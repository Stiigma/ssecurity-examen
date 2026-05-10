# Mejora 04: External Log Forwarding (Reenvio de Logs Externos)

## Alcance

Implementar un mecanismo de reenvio de eventos de seguridad (`SecurityEvent`) hacia destinos externos inmutables, separando fisicamente la evidencia defensiva de la base de datos operativa de la aplicacion. El objetivo es que, incluso si un atacante compromete la aplicacion o la base de datos, los registros de seguridad sigan existiendo en un medio de solo-adjuncion (append-only) que el atacante no pueda modificar o eliminar desde la aplicacion.

Esta mejora cubre:
- Escritura de cada evento de seguridad a un archivo de texto en formato JSON Lines (`.jsonl`) de forma append-only.
- Reenvio de eventos de seguridad a `stdout` / `stderr` en formato JSON estructurado para que un agente externo de log aggregation (ej. Fluentd, Loki, Datadog) los consuma.
- Un servicio `IExternalLogForwarder` que funcione como outbox: si el destino externo falla, el evento no se pierde (se reintenta o queda en la base de datos como fallback).
- Garantia de que los archivos de log de seguridad no pueden ser truncados o sobrescritos por la propia aplicacion.

## Lo que se tiene que implementar

1. **Servicio `IExternalLogForwarder` / `ExternalLogForwarder`**
   - `ForwardAsync(SecurityEvent securityEvent)`: recibe un evento ya persistido en la base de datos y lo reenvia a todos los destinos configurados.
   - Debe ser invocado desde `SecurityAuditService.AuditAsync` **despues** de que el evento se guarde exitosamente en la base de datos (garantiza que el fallback local existe).
   - Debe ser fire-and-forget desde el punto de vista del request HTTP: no debe bloquear la respuesta al usuario si el archivo o stdout estan lentos. Usar `Task.Run` o un `Channel<T>` en memoria con un background consumer.

2. **Destino: Archivo append-only JSON Lines**
   - Ruta configurable en `appsettings.json` (ej. `/var/log/examen-security/security-events.jsonl` en Linux, o `logs/security-events.jsonl` en desarrollo).
   - Formato: cada linea es un objeto JSON valido con TODOS los campos del `SecurityEvent`, incluyendo `EventHash` y `PreviousHash` de la mejora 03.
   - Apertura de archivo en modo `FileMode.Append`, `FileAccess.Write`, `FileShare.Read`.
   - Rotacion por tamano: cuando el archivo supere un tamano configurable (ej. 10 MB), se renombra a `security-events-YYYY-MM-DD-HH-mm-ss.jsonl` y se crea uno nuevo.
   - Manejo de excepciones: si no se puede escribir al archivo, se loguea un error en `ILogger` pero NO se lanza excepcion hacia arriba (no debe romper la request).

3. **Destino: Stdout en formato JSON**
   - Usar `ILogger` con un provider estructurado (o escribir directamente a `Console.Out` con JSON).
   - Formato: `{"timestamp":"2026-05-08T...","level":"Security","event":{...}}`.
   - Facilita integracion con Docker logs, Kubernetes, o cualquier sistema que capture stdout.
   - Debe estar separado del log tecnico normal de ASP.NET Core (usar un logger con categoria fija `ExamenSecurity.SecurityEvent`).

4. **Configuracion en `appsettings.json`**
   ```json
   {
     "SecurityLogForwarding": {
       "Enabled": true,
       "File": {
         "Enabled": true,
         "Path": "logs/security-events.jsonl",
         "MaxFileSizeMb": 10,
         "RetainFiles": 30
       },
       "Stdout": {
         "Enabled": true
       }
     }
   }
   ```

5. **Sanitizacion consistente**
   - Los datos que se reenvian al archivo/stdout deben pasar por la MISMA sanitizacion que la base de datos (redaccion de passwords, tokens, secrets).
   - No debe haber divergencia entre lo que se ve en la DB y lo que se ve en el archivo externo.

6. **Outbox / Fallback**
   - Si el archivo no se puede escribir temporalmente (disco lleno, permisos), el evento sigue existiendo en SQL Server.
   - Se puede agregar una tabla `ForwardedLogStatus` para trackear que eventos fueron exitosamente reenviados y cuales no, permitiendo un reintento posterior. Esto es opcional pero recomendado.

## Que queda fuera

- **Sistemas de terceros reales (Splunk, Datadog, ELK)**: No se integraran con sistemas comerciales. Solo se escribe a archivo local y stdout.
- **Encriptacion del archivo de logs**: Los archivos `.jsonl` se guardan en texto plano. La confidencialidad de los archivos en disco queda a cargo del sistema operativo (permisos de archivo). Encriptar el contenido queda fuera.
- **Redundancia geografica / replicacion remota**: No se replicaran los logs a servidores remotos, cloud storage (S3, Azure Blob), o sistemas offsite.
- **Compresion de archivos historicos**: Los archivos rotados quedan sin comprimir. Gzip/Bzip queda fuera.
- **Filtrado de eventos**: Se reenvian TODOS los eventos de seguridad. No se permitira filtrar por severidad o tipo en esta iteracion.

## Criterio de aceptacion

- [ ] Cada vez que `SecurityAuditService` persiste un evento, el mismo evento (en formato JSON) se adjunta a un archivo `.jsonl` en disco.
- [ ] El archivo se abre en modo append-only; la aplicacion no puede truncarlo ni sobrescribir lineas existentes.
- [ ] Cada linea del archivo es un JSON valido y contiene los mismos campos que el registro en la base de datos.
- [ ] Si el archivo supera el tamano maximo configurado, se rota automaticamente (se renombra y se crea uno nuevo).
- [ ] Los eventos tambien aparecen en stdout en formato JSON estructurado, separados del log tecnico de ASP.NET Core.
- [ ] Si el archivo no puede escribirse (disco lleno, permisos denegados), la aplicacion sigue funcionando y el evento queda al menos en la base de datos.
- [ ] Los campos sensibles (password, token, secret) estan redactados en el archivo/stdout de la misma forma que en la base de datos.
- [ ] La configuracion de rutas, tamano maximo, y habilitacion de destinos es configurable desde `appsettings.json` sin recompilar.
- [ ] Existen pruebas de integracion que verifican: evento persistido -> linea en archivo -> linea en stdout -> contenido sanitizado.
