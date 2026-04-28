# ExamenSecurity A09 Demo

Academic lab for **OWASP A09:2025 - Security Logging and Alerting Failures**.

This project is intentionally vulnerable on the `master` branch. It shows a common security mistake: the API rejects bad requests, but it does not produce useful security evidence or alerts. In other words, the application can say "no" to an attacker, but the defensive team stays blind.

> Do not deploy this project to production. The credentials, JWT key, database password, and vulnerable behavior are for a local class demo only.

## What We Are Demonstrating

OWASP A09 is about failures in security logging, monitoring, and alerting. A system is affected when important security events are missing, incomplete, stored unsafely, not monitored, or never escalated into alerts.

In this project, the vulnerable version demonstrates that:

- Failed logins return `401`, but are not persisted as security events.
- Forbidden access returns `403`, but the attempt is not audited.
- Student record probing is blocked, but there is no trace for investigation.
- Admin actions change data, but do not create a security audit trail.
- Security-related support tickets do not generate alerts.
- `/api/security/events` and `/api/security/alerts` return empty arrays.

The lesson is:

```text
Blocking protects one request. Logging and alerting protect the response to an incident.
```

## Project Structure

```text
src/ExamenSecurity.Api/       ASP.NET Core API used for the demo
security-docs/                Built Docusaurus documentation artifacts
http/demo-vulnerable.http     Request collection for the vulnerable demo
Dockerfile                    API container build
ExamenSecurity.sln            .NET solution
```

## Branches

| Branch | Purpose |
|---|---|
| `master` | Vulnerable A09 demo. The app rejects suspicious actions but does not log or alert properly. |
| `origin/fixed` | Remediated demo. Adds security events, alerts, correlation IDs, and investigation endpoints. |

To compare both versions:

```bash
git switch master
# run vulnerable demo

git switch -c fixed --track origin/fixed
# run fixed demo
```

## Demo Users

The database seeder creates these users:

| Role | Email | Password |
|---|---|---|
| Admin | `admin@demo.local` | `Admin123!` |
| Auditor | `auditor@demo.local` | `Auditor123!` |
| Student | `student1@demo.local` | `Student123!` |
| Student | `student2@demo.local` | `Student123!` |
| Disabled student | `disabled@demo.local` | `Disabled123!` |

Useful seeded IDs:

```text
student1 user id: 20000000-0000-0000-0000-000000000001
student2 user id: 20000000-0000-0000-0000-000000000002
```

## Requirements

- .NET SDK 10 or compatible SDK for the target framework in `ExamenSecurity.Api.csproj`
- SQL Server available on `localhost:1433`
- An HTTP client such as VS Code REST Client, JetBrains HTTP Client, Postman, Insomnia, or `curl`

The default connection string is in:

```text
src/ExamenSecurity.Api/appsettings.json
```

It expects:

```text
Server=localhost,1433
Database=ExamenSecurityDb
User Id=sa
Password=Your_strong_password123
```

One local SQL Server option is:

```bash
docker run --name examen-security-sql \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD=Your_strong_password123 \
  -p 1433:1433 \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

## Run The API

```bash
dotnet restore
dotnet run --project src/ExamenSecurity.Api
```

Default local URLs from `launchSettings.json`:

```text
http://localhost:5000
https://localhost:5001
```

Health check:

```http
GET http://localhost:5000/api/demo/health
```

Expected vulnerable response includes:

```json
{
  "status": "ok",
  "version": "main vulnerable"
}
```

## Run The Vulnerable A09 Demo

Use [http/demo-vulnerable.http](http/demo-vulnerable.http) or send equivalent requests manually.

Recommended class flow:

1. Call `GET /api/demo/scenarios`.
2. Send five failed login attempts to `POST /api/auth/login`.
3. Login as `admin@demo.local`.
4. Call `GET /api/security/events`.
5. Call `GET /api/security/alerts`.
6. Explain that the attack created `401` responses, but no durable security events or alerts.
7. Login as `student1@demo.local`.
8. Try `GET /api/admin/users` as the student.
9. Try `GET /api/student-records/by-user/20000000-0000-0000-0000-000000000002`.
10. Show again that the system denied access, but did not preserve investigation evidence.

The important result is not the `401` or `403`. The important result is the empty evidence:

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

## Where The Vulnerability Is In The Code

| File | What it demonstrates |
|---|---|
| `AuthService.cs` | Failed logins and password reset requests are not audited. |
| `Program.cs` | JWT failures are rejected but not persisted as security events. |
| `StudentRecordService.cs` | Access to another student's record is denied but not logged. |
| `AdminService.cs` | Sensitive admin actions do not create a security audit trail. |
| `SupportTicketService.cs` | Security-relevant tickets do not trigger alerts. |
| `SecurityController.cs` | Security event and alert endpoints return empty collections. |
| `DemoScenarioService.cs` | Lists the attack scenarios for the presentation. |

## What The Fixed Version Should Add

A professional fix should add:

- Structured security events.
- A durable event store.
- Actor, IP address, endpoint, target resource, result, timestamp, and severity.
- Correlation IDs for investigations.
- Sanitized metadata that does not store passwords, tokens, or secrets.
- Alert rules for repeated suspicious behavior.
- Protected investigation endpoints for admins and auditors.
- A process for reviewing and acknowledging alerts.

The `origin/fixed` branch demonstrates this direction with `SecurityEvent`, `SecurityAlert`, `SecurityAuditService`, and `SecurityAlertService`.

## Canva Presentation Outline

Use this structure for the presentation:

| Slide | Title | Main point |
|---:|---|---|
| 1 | A09: Security Logging and Alerting Failures | Introduce the OWASP category. |
| 2 | Blocking vs Observing | A `401`/`403` is not the same as detection. |
| 3 | Impact | No logs means weak detection, investigation, and response. |
| 4 | Demo System | Explain users, roles, and student records. |
| 5 | Red Team Demo | Failed logins and forbidden access. |
| 6 | Evidence Gap | Events and alerts are empty. |
| 7 | Root Cause | No security audit pipeline. |
| 8 | What Good Looks Like | Events, correlation, severity, alert thresholds. |
| 9 | Fixed Branch | Show the corrected architecture if time allows. |
| 10 | Conclusion | A secure app must protect resources and produce evidence. |

Short conclusion:

```text
A09 teaches that security is not only prevention. A system must also create evidence so incidents can be detected, investigated, and answered.
```

## References

- OWASP Top 10:2025: https://owasp.org/Top10/2025/
- OWASP A09:2025 Security Logging and Alerting Failures: https://owasp.org/Top10/2025/A09_2025-Security_Logging_and_Alerting_Failures/
- OWASP Logging Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html
