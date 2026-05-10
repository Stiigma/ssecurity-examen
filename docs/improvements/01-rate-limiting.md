# Mejora 01: Rate Limiting (Limitacion de Tasa)

## Alcance

Implementar un mecanismo de limitacion de tasa (rate limiting) a nivel de API para prevenir abuso de endpoints sensibles, en especial aquellos que generan eventos de seguridad. El objetivo es evitar que un atacante pueda saturar la base de datos de eventos de seguridad (`SecurityEvents`) mediante ataques de fuerza bruta automatizados o flooding de requests.

Esta mejora afecta a los siguientes endpoints y flujos:
- `POST /api/auth/login`
- `POST /api/auth/password-reset/request`
- `GET /api/admin/*`
- `GET /api/student-records/*`
- Cualquier endpoint que pueda ser sondado masivamente por un atacante.

## Lo que se tiene que implementar

1. **Middleware de Rate Limiting Global**
   - Crear un middleware `RateLimitingMiddleware` que intercepte requests entrantes y aplique reglas de limitacion basadas en IP del cliente.
   - Usar un almacenamiento en memoria (MemoryCache) para contar requests por IP + endpoint (o IP + bucket de tiempo).
   - Configurar diferentes limites por tipo de endpoint:
     - **Auth endpoints** (login, password reset): maximo 5 requests por minuto por IP.
     - **API generales**: maximo 100 requests por minuto por IP.
     - **Admin endpoints**: maximo 20 requests por minuto por IP.

2. **Respuesta estandarizada al exceder limite**
   - Cuando se exceda el limite, devolver HTTP `429 Too Many Requests`.
   - Incluir header `Retry-After` indicando los segundos restantes para poder reintentar.
   - Opcionalmente, registrar un evento de seguridad `RateLimitExceeded` con severidad `Warning` para detectar IPs que estan siendo bloqueadas frecuentemente.

3. **Configuracion externa**
   - Agregar seccion `RateLimiting` en `appsettings.json` para definir limites, ventanas de tiempo y si esta habilitado o no, sin necesidad de recompilar.

4. **Exclusiones controladas**
   - Permitir whitelist de IPs (ej. localhost, IPs internas de la institucion) mediante configuracion.
   - Health checks (`/api/demo/health`) deben estar exentos de rate limiting para evitar falsos positivos en monitoreo.

5. **Headers informativos**
   - Incluir headers de rate limit en respuestas exitosas para que clientes legítimos conozcan sus cuotas:
     - `X-RateLimit-Limit`: limite maximo.
     - `X-RateLimit-Remaining`: requests restantes en la ventana actual.
     - `X-RateLimit-Reset`: timestamp de reinicio de la ventana.

## Que queda fuera

- **Rate limiting por usuario autenticado**: En esta iteracion solo se limita por IP. Limitar por usuario (claim de JWT) queda para una mejora futura.
- **Persistencia distribuida**: Se usa memoria local (MemoryCache). Si la app se escala a multiples instancias, cada instancia tendra su propio contador. Una solucion distribuida con Redis queda fuera del alcance.
- **Rate limiting avanzado por algoritmo (token bucket, leaky bucket)**: Se implementara un contador fijo de ventana deslizante simple (fixed window counter) o ventana deslizante basica. Algoritmos mas sofisticados quedan para iteraciones posteriores.
- **UI de administracion de rate limits**: No se construira un panel para modificar limites en tiempo real; solo se usara configuracion por archivo.
- **Bloqueo permanente de IPs**: El rate limit es temporal. Bloqueos permanentes o blacklist manual quedan fuera.

## Criterio de aceptacion

- [ ] Un cliente desde la misma IP puede hacer maximo 5 requests a `POST /api/auth/login` por minuto. El sexto request debe devolver `429 Too Many Requests` con header `Retry-After`.
- [ ] Un cliente desde la misma IP puede hacer maximo 100 requests por minuto a endpoints generales. Al exceder, recibe `429`.
- [ ] El endpoint `GET /api/demo/health` NO esta sujeto a rate limiting.
- [ ] Las IPs configuradas en whitelist pueden hacer requests sin restricciones.
- [ ] Cuando se devuelve `429`, se registra un evento de seguridad `RateLimitExceeded` (opcional pero deseable) con IP, endpoint y timestamp.
- [ ] Los headers `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset` estan presentes en respuestas exitosas de endpoints protegidos.
- [ ] Los limites son configurables desde `appsettings.json` sin recompilar.
- [ ] El rate limiting no afecta negativamente el rendimiento de requests normales (benchmark: latencia p95 aumenta < 5ms).
- [ ] Existen pruebas de integracion que verifican los escenarios de limitacion y exclusion.
