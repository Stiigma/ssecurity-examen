# Mejora 06: Impossible Travel (Deteccion de Viajes Imposibles)

## Alcance

Implementar un mecanismo de deteccion de "viajes imposibles" (impossible travel) que alerte cuando un usuario inicie sesion desde dos ubicaciones geograficas distintas en un intervalo de tiempo demasiado corto como para ser fisicamente posible. El objetivo es detectar:
- Credenciales comprometidas compartidas o vendidas.
- Ataques donde el atacante esta en un pais diferente al usuario legitimo.
- Uso de VPNs o proxies que cambian de ubicacion abruptamente.

Esta mejora cubre:
- Geolocalizacion aproximada por direccion IP (sin dependencias externas de pago).
- Almacenamiento del ultimo login exitoso de cada usuario (ubicacion + timestamp).
- Calculo de distancia y tiempo entre logins consecutivos.
- Generacion de alerta `ImpossibleTravel` cuando la velocidad calculada supere un umbral humanamente posible.

## Lo que se tiene que implementar

1. **Servicio de geolocalizacion mock / basico**
   - `IGeoLocationService` / `GeoLocationService`.
   - Metodo: `GetLocationAsync(string ipAddress)`.
   - Retorna: `CountryCode` (ej. "MX", "US"), `CountryName` (ej. "Mexico"), `Region`, `City`, `Latitude`, `Longitude`, `IsVpnOrProxy` (booleano, basico).
   - Implementacion sin APIs externas de pago:
     - Usar una base de datos local ligera como `IP2Location LITE` (CSV gratuito) o `GeoLite2` (requiere cuenta gratuita de MaxMind).
     - Para la demo/laboratorio, se puede usar un mapeo mock basado en rangos de IP privadas:
       - `127.0.0.1` -> localhost (se ignora para impossible travel).
       - `192.168.x.x` -> red interna (se ignora).
       - Cualquier otra IP -> asignar aleatoriamente un pais de una lista predefinida (solo para efectos de demostracion en clase), O implementar una tabla `IpCountryRanges` con rangos CIDR y paises.
     - **Recomendacion para el examen**: Implementar una tabla `IpCountryRanges` con ~50 rangos de IPs publicas reales y sus paises. Es suficiente para la demo y demuestra el concepto sin depender de internet.
   - Si la IP es privada o localhost, retornar `IsLocal = true` y no evaluar impossible travel.

2. **Entidad `UserLoginLocation`** (nueva tabla)
   - Campos:
     - `Id` (GUID)
     - `UserId` (FK a AppUser)
     - `IpAddress` (string)
     - `CountryCode` (string, 2 chars)
     - `CountryName` (string)
     - `Latitude` (decimal)
     - `Longitude` (decimal)
     - `LoginAtUtc` (DateTimeOffset)
   - Relacion: Uno-a-muchos con `AppUser`.
   - Indice en `UserId` + `LoginAtUtc` descendente.

3. **Integracion con `AuthService`**
   - En `LoginAsync`, despues de un login exitoso (`LoginSucceeded`):
     1. Llamar a `GeoLocationService.GetLocationAsync(ipAddress)`.
     2. Guardar el resultado en `UserLoginLocation`.
     3. Buscar el login anterior exitoso del mismo usuario (el mas reciente antes de este).
     4. Si existe un login anterior:
        - Calcular distancia en kilometros entre las dos coordenadas (formula de Haversine).
        - Calcular tiempo transcurrido en horas.
        - Calcular velocidad = distancia / tiempo.
        - Si velocidad > umbral configurable (por defecto 900 km/h, velocidad de un avion comercial):
          - Generar evento `ImpossibleTravelDetected` (SecurityEventType nuevo).
          - Generar alerta `ImpossibleTravel` (SecurityAlertType nuevo) de severidad `High`.
          - Incluir en metadata: pais anterior, pais nuevo, distancia km, tiempo transcurrido, velocidad calculada.

4. **Nuevos tipos de evento y alerta**
   - `SecurityEventType.ImpossibleTravelDetected = 18`.
   - `SecurityAlertType.ImpossibleTravel = 9`.

5. **Configuracion**
   - En `appsettings.json`:
     ```json
     {
       "ImpossibleTravel": {
         "Enabled": true,
         "MaxSpeedKmh": 900,
         "IgnoreLocalIps": true,
         "IgnoredCountryCodes": ["VPN", "TOR"]
       }
     }
     ```
   - `MaxSpeedKmh`: velocidad maxima humana posible. Default 900 km/h (avion). Un usuario mas conservador podria poner 500 km/h.
   - `IgnoreLocalIps`: si true, ignora 127.0.0.1 y rangos privados.

6. **Demo y casos de prueba**
   - Caso 1: Usuario login desde Mexico City, 10 minutos despues desde Buenos Aires.
     - Distancia: ~7,400 km. Tiempo: 0.16 h. Velocidad: ~44,000 km/h. -> Alerta.
   - Caso 2: Usuario login desde Mexico City, 24 horas despues desde Madrid.
     - Distancia: ~9,000 km. Tiempo: 24 h. Velocidad: 375 km/h. -> No alerta.
   - Caso 3: Login desde localhost dos veces. -> Ignorado si `IgnoreLocalIps` es true.

## Que queda fuera

- **APIs de geolocalizacion comerciales de pago**: No se usaran servicios como MaxMind GeoIP2 Precision, IPInfo.io Pro, o ipgeolocation.io. Solo datos locales/mock.
- **Deteccion de VPN/Tor/Proxy avanzada**: No se implementaran listas negras de exit nodes de Tor ni deteccion heuristica de VPN. Solo un flag basico opcional.
- **Bloqueo automatico por impossible travel**: Solo se genera alerta. No se bloquea la cuenta automaticamente.
- **Notificacion al usuario**: No se enviara email/SMS al usuario alertandole de login sospechoso.
- **Machine learning / perfilado de comportamiento**: No se analizaran patrones historicos complejos (ej. "el usuario siempre loguea desde Mexico, ahora esta en Rusia"). Solo distancia/tiempo entre ultimos dos logins.
- **Precision a nivel de ciudad**: La geolocalizacion es a nivel de pais/region para simplificar. No es necesario precisión de metros.

## Criterio de aceptacion

- [ ] Despues de un login exitoso, se registra la ubicacion aproximada del usuario en `UserLoginLocation`.
- [ ] Si un usuario loguea desde un pais A y luego (en menos tiempo del fisicamente posible) desde un pais B a mas de `MaxSpeedKmh` km de distancia, se genera un evento `ImpossibleTravelDetected`.
- [ ] Se genera una alerta `ImpossibleTravel` de severidad `High` con detalles: pais A, pais B, distancia, tiempo, velocidad.
- [ ] Si la IP es localhost (`127.0.0.1`) o privada (`192.168.x.x`, `10.x.x.x`), y `IgnoreLocalIps` es true, no se evalua impossible travel.
- [ ] La velocidad maxima (`MaxSpeedKmh`) es configurable en `appsettings.json`.
- [ ] La formula de distancia usa Haversine y considera la curvatura de la Tierra (no distancia euclidiana simple).
- [ ] Un login unico (primera vez del usuario) no genera alerta de impossible travel.
- [ ] Las ubicaciones se almacenan correctamente incluso si no hay alerta (para futura comparacion).
- [ ] Existen pruebas de integracion que simulan logins desde distintos paises y validan: caso de alerta (velocidad imposible), caso sin alerta (velocidad normal), e IPs locales ignoradas.
