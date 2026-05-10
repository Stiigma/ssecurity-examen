# Mejora 05: Honeytoken Endpoint (Endpoint Senoelo)

## Alcance

Implementar uno o mas endpoints "honeytoken" (senoelos) que aparentan ser funcionalidades internas, administrativas o de alto valor, pero que en realidad no tienen ningun proposito legitimo para usuarios normales. Cualquier request (GET, POST, PUT, DELETE) contra estos endpoints debe disparar una alerta de seguridad de severidad **CRITICA** de forma inmediata, sin umbral de conteo.

El objetivo es detectar:
- Sondeo automatizado de endpoints (scanners como OWASP ZAP, Burp Suite, Nmap scripts).
- Atacantes que intentan descubrir funcionalidades ocultas o administrativas.
- Insiders curiosos que intentan acceder a funcionalidades no autorizadas.

Esta mejora cubre:
- Creacion de endpoints senoelo que parecen reales.
- Generacion inmediata de alerta `HoneytokenTriggered` al recibir cualquier request.
- Respuesta convincente (pero segura) que no revele que es un senoelo.
- Opcionalmente, respuestas lentas o con datos falsos para entretener al atacante.

## Lo que se tiene que implementar

1. **Controlador `HoneytokenController`**
   - Ubicacion: `src/ExamenSecurity.Api/Controllers/HoneytokenController.cs`.
   - Rutas que simulan funcionalidades internas atractivas para atacantes:
     - `GET /api/internal/backup` - Simula descarga de backup de base de datos.
     - `POST /api/internal/backup` - Simula trigger de backup manual.
     - `GET /api/internal/logs/debug` - Simula logs de debug con informacion "sensible".
     - `GET /api/admin/config/secrets` - Simula acceso a configuracion con secretos.
     - `GET /api/v1/users/export` - Simula exportacion masiva de usuarios.
   - Todos los endpoints deben estar marcados como `[AllowAnonymous]` intencionalmente, para que incluso un atacante no autenticado los pueda tocar.
   - Cada endpoint, al recibir cualquier request, debe:
     1. Registrar un evento de seguridad `HoneytokenTriggered` con severidad `Critical`.
     2. Generar una alerta `HoneytokenAccessed` de severidad `Critical` inmediatamente (sin ventana de tiempo ni conteo).
     3. Devolver una respuesta HTTP convincente pero inofensiva:
        - `GET /api/internal/backup`: devolver `200 OK` con un body JSON falso de progreso de backup (ej. `{"status": "queued", "jobId": "a1b2c3..."}`).
        - `GET /api/admin/config/secrets`: devolver `200 OK` con valores FALSOS y obvios (ej. `{"dbPassword": "[REDACTED_HONEYTOKEN]", "apiKey": "[HONEYTOKEN_FAKE_KEY]"}`).
        - NUNCA devolver datos reales de la aplicacion.

2. **Nuevos tipos de evento y alerta**
   - `SecurityEventType.HoneytokenTriggered = 17` (siguiente disponible).
   - `SecurityAlertType.HoneytokenAccessed = 8` (siguiente disponible).
   - El evento debe capturar: IP, User-Agent, metodo HTTP, path completo, query string, body truncado (si es POST/PUT), y correlation ID.
   - Si el request trae un JWT invalido o expirado, igual debe registrarse (el atacante esta intentando usar credenciales).

3. **Integracion con `SecurityAlertService`**
   - En el metodo `EvaluateAsync`, agregar case para `HoneytokenTriggered`.
   - A diferencia de otras alertas, esta NO debe usar `CreateAlertIfMissingAsync` con ventana de tiempo. Cada request a un honeytoken debe generar una alerta nueva y distinta.
   - Esto permite ver la frecuencia exacta del sondeo.

4. **Respuestas con delay tactico (opcional pero recomendado)**
   - Agregar un delay artificial de 2-5 segundos antes de responder. Esto ralentiza a los scanners automatizados sin afectar a usuarios legitimos (que nunca deberian tocar estos endpoints).
   - El delay debe ser aleatorio dentro de un rango configurable.

5. **Registro en `DemoScenarioService`**
   - Agregar un escenario de demo:
     - Id: `A09-HONEYTOKEN`
     - Titulo: "Deteccion de sondeo con endpoint senoelo"
     - Descripcion: Llamar `GET /api/internal/backup` desde un scanner o curl anonimo.
     - Resultado: Alerta CRITICA `HoneytokenAccessed` generada inmediatamente con todos los metadatos del request.

## Que queda fuera

- **Honeypot de sistema completo**: No se implementara un servidor o servicio separado. Solo endpoints HTTP dentro de la API existente.
- **Honeypot de datos (fake database records)**: No se crearan registros falsos en la base de datos que se monitorean. Los honeytokens son puramente endpoints.
- **Respuestas dinamicas complejas**: Las respuestas seran estaticas o pseudo-aleatorias simples. No se implementara un sistema de "engagement" que cambie comportamiento segun el atacante.
- **Bloqueo automatico tras honeytoken**: No se bloqueara automaticamente la IP que toca un honeytoken. Solo se genera alerta CRITICA. El bloqueo manual queda a criterio del admin.
- **Honeypot de archivos (documentos con tracking pixels, canary tokens)**: Solo endpoints HTTP, no archivos.

## Criterio de aceptacion

- [ ] El endpoint `GET /api/internal/backup` esta accesible sin autenticacion y devuelve HTTP 200 con un body JSON falso convincente.
- [ ] Al llamar cualquier endpoint honeytoken, se crea un `SecurityEvent` de tipo `HoneytokenTriggered` con severidad `Critical`.
- [ ] Al llamar cualquier endpoint honeytoken, se crea una `SecurityAlert` de tipo `HoneytokenAccessed` con severidad `Critical` inmediatamente (sin esperar umbral ni ventana de tiempo).
- [ ] El evento contiene: IP, User-Agent, metodo HTTP, path, query string, y correlation ID.
- [ ] La respuesta del honeytoken NUNCA contiene datos reales de la aplicacion (passwords, connection strings, datos de usuarios reales).
- [ ] Un administrador puede ver las alertas `HoneytokenAccessed` en `GET /api/security/alerts` ordenadas por severidad.
- [ ] Los endpoints honeytoken aparecen en la lista de escenarios de demo.
- [ ] (Opcional) La respuesta incluye un delay artificial de 2-5 segundos configurable.
- [ ] Existen pruebas de integracion que validan: request anonimo a honeytoken -> evento CRITICO -> alerta CRITICA -> respuesta 200 con datos falsos.
