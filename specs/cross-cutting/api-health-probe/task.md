# task.md — Sonda operativa de salud `GET /health` · Backend UTTT-GO

> Versión documental **1.0** · 2026-11-07 · Derivado de `plan.md` v1.0. Estados: `[x]` completada con evidencia, `[ ]` pendiente.

## 1. Tareas

| ID | Tarea | Requisitos | Responsable | Estado | Evidencia |
|---|---|---|---|---|---|
| HP-T01 | Redactar `spec.md`, `plan.md` y `task.md` de la sonda | HP-01..HP-10 | AGENT-BE-01 | `[x]` | `specs/cross-cutting/api-health-probe/{spec,plan,task}.md` v1.0 |
| HP-T02 | Trasladar `ApiError` a `Shared/Contracts` y actualizar los `using` de Identity, Admin, Sellers y `Program.cs` | D-HP-02 | AGENT-BE-01 | `[x]` | `src/UTTTGO.Api/Shared/Contracts/ApiError.cs`; sin referencias a `Modules.Identity.Contracts.ApiError` |
| HP-T03 | Implementar los contratos y el servicio de sonda con clasificación de fallos y tiempos de espera acotados | HP-01, HP-02, HP-03, HP-06 | AGENT-BE-01 | `[x]` | `Shared/Health/HealthContracts.cs`, `HealthService.cs`, `DependencyProbes.cs` |
| HP-T04 | Exponer `GET /health` con `200`/`503`/`429` y contrato de error único | HP-01, HP-02, HP-04, HP-05 | AGENT-BE-01 | `[x]` | `Shared/Health/HealthController.cs` |
| HP-T05 | Registrar la política de rate limit distribuido `health` | HP-05 | AGENT-BE-01 | `[x]` | `RedisOptions.cs` (`RateLimitingOptions.Health`), `RedisRateLimiter`, `appsettings.json` |
| HP-T06 | Cambiar el healthcheck de Nginx a `/health` y mantener la liveness TCP en la API | HP-07, HP-08 | AGENT-BE-01 | `[x]` | `docker-compose.yml` (`nginx` healthcheck `http://127.0.0.1/health`; `api` healthcheck TCP) |
| HP-T07 | Registrar el resultado de `GET /health` en la evidencia de CD-1 | HP-09 | AGENT-BE-01 | `[x]` | `.github/workflows/cd-deploy.yml` (health HTTP + `ci-evidence/cd-deploy.txt`) |
| HP-T08 | Pruebas unitarias de clasificación de fallos y tiempos de espera | HP-03, HP-06 | AGENT-BE-05 | `[x]` | `tests/UtttGo.UnitTests/HealthServiceTests.cs` |
| HP-T09 | Pruebas de integración de la sonda (200, 503 por dependencia, 429, sin configurar) | HP-AC-01..HP-AC-06 | AGENT-BE-05 | `[x]` | `tests/UtttGo.IntegrationTests/HealthEndpointTests.cs` |
| HP-T10 | Pruebas de seguridad (sin fuga de secretos ni de detalle del proveedor) | HP-AC-04 | AGENT-BE-04 / AGENT-BE-05 | `[x]` | `tests/UtttGo.SecurityTests/HealthSecurityTests.cs` |
| HP-T11 | Verificación estática y suites completas | HP-AC-10 | AGENT-BE-05 | `[x]` | Build Release 0 errores; `dotnet format` sin diferencias; unitarias, integración y seguridad en verde |
| HP-T12 | Verificar `/health` en el stack local (`200` con Mongo y Redis; `503` al detener Mongo) | HP-AC-07, HP-AC-08 | AGENT-BE-05 | `[x]` | `docker compose config` válido; prueba local con perfil `local` |
| HP-T13 | Actualizar `memoria.md`, `docs/traceability.md`, `PROJECT_STATUS.md`, `README.md` y `AGENTS.md` | D-HP-08 | AGENT-BE-06 | `[x]` | Entradas de la sonda y del healthcheck en la documentación viva |

## 2. Criterios de cierre

- [x] `spec.md` aprobado y tareas trazadas.
- [x] Build Release sin errores y `dotnet format` sin diferencias.
- [x] Suites unitaria, de integración y de seguridad en verde.
- [x] Sin secretos ni URIs de conexión en el código, los logs ni el diff.
- [x] `memoria.md` y `docs/traceability.md` actualizados.
- [x] Healthcheck de Nginx sobre ruta real verificado en el stack local.
