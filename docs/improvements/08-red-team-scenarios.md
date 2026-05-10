# Mejora 08: Red Team Scenarios Script (Script de Escenarios Red Team en Python)

## Alcance

Implementar un script ejecutable en Python que funcione como una herramienta de Red Team para demostrar y validar las defensas del backend. El script no solo ejecuta ataques simulados, sino que tambien verifica que el sistema defensivo (eventos, alertas, bloqueos, integridad) respondio correctamente a cada ataque.

Este script es el componente principal de la **explotacion en clase**: el estudiante o profesor lo ejecuta desde la terminal y obtiene un reporte visual del antes (rama `master`) vs despues (rama `fixed`).

El script cubre:
- Ataques automatizados contra endpoints sensibles.
- Verificacion post-ataque de que la defensa genero evidencia (eventos, alertas).
- Comparacion entre rama vulnerable y rama defensiva.
- Generacion de reporte en consola (colorido) y archivo Markdown para entrega.

## Lo que se tiene que implementar

1. **Estructura del proyecto Python**
   ```
   redteam/
   ├── main.py                    # Entry point: ejecuta todos los escenarios
   ├── config.py                  # URLs, credenciales de demo, timeouts
   ├── api_client.py              # Cliente HTTP con manejo de JWT y retry basico
   ├── scenarios/
   │   ├── __init__.py
   │   ├── base_scenario.py       # Clase abstracta con run() y verify()
   │   ├── password_spraying.py   # 5 logins fallidos + verifica alerta
   │   ├── admin_probing.py       # Student intenta admin 3x + verifica eventos
   │   ├── record_probing.py      # Expediente ajeno 2x + verifica alerta
   │   ├── honeytoken.py          # Toca endpoint senoelo + verifica CRITICAL
   │   ├── rate_limit.py          # 6 logins rapidos + verifica 429
   │   ├── account_lockout.py     # 5 fallidos + verifica 423 en sexto
   │   ├── log_integrity.py       # Verifica integrity-check endpoint
   │   ├── security_ticket.py     # Crea ticket de seguridad + verifica alerta
   │   └── impossible_travel.py   # Simula 2 logins desde distintos paises
   └── reporters/
       ├── console_reporter.py    # Tablas y colores con rich
       └── markdown_reporter.py   # Genera reporte.md con resultados
   ```

2. **Paqueteria de Python**
   - `requests` (~2.31.0): HTTP cliente.
   - `rich` (~13.7.0): Tablas, colores, progress bars, paneles en terminal.
   - `typer` (~0.12.0): CLI opcional con argumentos (ej. `--target fixed`, `--output reporte.md`).
   - `pydantic` (~2.7.0): Validacion de respuestas JSON de la API.
   - Paquetes estandar: `json`, `datetime`, `time`, `dataclasses`, `typing`.

3. **Flujo de cada escenario**
   Cada escenario hereda de `BaseScenario` y implementa:
   - `run_attack()`: Ejecuta el ataque (requests HTTP).
   - `verify_defense()`: Consulta endpoints de seguridad (`/api/security/events`, `/api/security/alerts`) y valida que la defensa respondio.
   - `report()`: Retorna un dict con: nombre, descripcion, ataque, resultado, defensa_detectada, pass/fail.

4. **Escenarios detallados**

   | # | Escenario | Ataque | Verificacion |
   |---|---|---|---|
   | 1 | `PasswordSpraying` | 5x POST /api/auth/login fallidos como admin@demo.local | Alerta `MultipleFailedLogins` + 5 eventos `LoginFailed` |
   | 2 | `AdminProbing` | Student JWT -> GET /api/admin/users (3x) | 3 eventos `AccessDenied` + alerta `AdminEndpointProbing` |
   | 3 | `RecordProbing` | Student JWT -> GET /api/student-records/by-user/{otroId} (2x) | 2 eventos `StudentRecordAccessDenied` + alerta `StudentRecordProbing` |
   | 4 | `Honeytoken` | GET /api/internal/backup sin autenticacion | Evento `HoneytokenTriggered` CRITICAL + alerta `HoneytokenAccessed` |
   | 5 | `RateLimit` | 6x POST /api/auth/login en < 60 segundos | Sexto devuelve `429` + header `Retry-After` |
   | 6 | `AccountLockout` | 5x login fallido -> intentar sexto | Sexto devuelve `423 Locked` |
   | 7 | `LogIntegrity` | GET /api/security/integrity-check | `isValid: true` |
   | 8 | `SecurityTicket` | POST /api/support-tickets con `isSecurityRelevant=true` | Evento `SecurityTicketCreated` + alerta `SecurityTicketRequiresReview` |
   | 9 | `ImpossibleTravel` | 2 logins con headers `X-Forwarded-For` simulando distintos paises | Alerta `ImpossibleTravel` |

5. **Modos de ejecucion**
   - **Modo FIXED (default)**: Corre todos los escenarios y espera que la defensa funcione. Ideal para demostrar la rama `fixed`.
   - **Modo VULNERABLE**: Corre los escenarios pero solo verifica que NO existan eventos/alertas. Ideal para demostrar la rama `master` ciega.
   - **Flag `--mode {fixed|vulnerable}`**: Determina si las verificaciones esperan defensa o ausencia de defensa.
   - **Flag `--target http://localhost:5000`**: URL base de la API.

6. **Salida en consola (rich)**
   - Panel de titulo con arte ASCII: "RED TEAM SCENARIOS".
   - Tabla resumen con columnas: #, Escenario, Estado (✅/❌), Tiempo, Defensa Detectada.
   - Paneles expandibles con detalles del request/response para debug.
   - Barra de progreso total.
   - Resumen final: "X/9 escenarios pasaron en MODO fixed".

7. **Reporte Markdown**
   - Archivo `reporte-red-team-{fecha}.md` con:
     - Titulo y descripcion.
     - Tabla comparativa: Ataque vs Respuesta Defensiva.
     - Screenshots de texto (bloques de JSON con los eventos y alertas generados).
     - Conclusion: "La rama fixed demuestra evidencia durable frente a cada ataque simulado".

## Que queda fuera

- **Frameworks de testing de Python (pytest, unittest)**: Este es un script de ejecucion, no una suite de testing tradicional. No usa asserts ni runners de test.
- **Generacion de trafico masivo / DoS real**: Los ataques son funcionales y controlados (5-6 requests). No se busca saturar la red.
- **Persistencia de estado entre ejecuciones**: Cada ejecucion es independiente. No guarda en SQLite ni archivos de estado.
- **Interfaz grafica (GUI)**: Solo terminal y Markdown.
- **Integracion con exploits reales (Metasploit, Cobalt Strike)**: Son ataques HTTP simples, no payloads complejos.
- **Notificaciones (email, Slack)**: El script solo reporta localmente.

## Criterio de aceptacion

- [ ] El script `python redteam/main.py --mode fixed` ejecuta los 9 escenarios sin errores de Python.
- [ ] En modo `fixed`, todos los escenarios reportan `PASS` (la defensa genero eventos/alertas como se esperaba).
- [ ] En modo `vulnerable`, todos los escenarios reportan `PASS` (la defensa NO genero eventos/alertas, demostrando la ceguera del sistema).
- [ ] La salida en consola usa colores y tablas (via `rich`) y es legible en una terminal de 80x24.
- [ ] Se genera un archivo `reporte-red-team-YYYY-MM-DD.md` con la misma informacion.
- [ ] El script maneja errores de red (API no disponible) con mensajes claros sin stack trace crudo.
- [ ] Las credenciales de demo estan centralizadas en `config.py` y no hardcodeadas en cada escenario.
- [ ] Cada escenario limpia sus propios datos de prueba si es necesario (ej. desbloquear cuenta si quedo bloqueada para no contaminar el siguiente escenario).
- [ ] El tiempo total de ejecucion es menor a 30 segundos (incluyendo delays de rate limiting).
