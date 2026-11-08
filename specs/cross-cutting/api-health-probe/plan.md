# plan.md — Sonda operativa de salud `GET /health` · Backend UTTT-GO

> Versión documental **1.1** · 2026-11-07 · Derivado de `spec.md` v1.1 (aprobada).

## 1. Secuencia de implementación

| Paso | Contenido | Responsable | Estado |
|---|---|---|---|
| P1 | Redactar `spec.md` (HP-01..HP-10, HP-AC-01..10, contrato, riesgos) | AGENT-BE-01 | Completado |
| P2 | Redactar `plan.md` y `task.md` | AGENT-BE-01 | Completado |
| P3 | Mover el contrato de error `ApiError` de `Modules/Identity/Contracts` a `Shared/Contracts` (dejar de colgar el contrato transversal de un módulo) y actualizar los `using` | AGENT-BE-01 | Completado |
| P4 | Crear `Shared/Health/` con los contratos de sonda (`HealthResponse`, `HealthStatus`, `HealthDependency`, `HealthReportResponse`), el servicio `HealthService` y los adaptadores `MongoHealthProbe`/`RedisHealthProbe` con tiempos de espera acotados | AGENT-BE-01 | Completado |
| P5 | Exponer `GET /health` con `HealthController` (`200`/`503`/`429`), contrato de error único y `correlationId` | AGENT-BE-01 | Completado |
| P6 | Añadir la política de rate limit distribuido `health` a `RedisRateLimiter`, `RateLimitingOptions` y `appsettings.json` | AGENT-BE-01 | Completado |
| P7 | Cambiar el healthcheck de Nginx a `GET /health` y mantener el de la API como liveness TCP | AGENT-BE-01 | Completado |
| P8 | Registrar `GET /health` en la evidencia del CD-1 sin abortar el despliegue si una dependencia está caída | AGENT-BE-01 | Completado |
| P9 | Pruebas unitarias (clasificación de fallos y tiempos de espera), de integración (200/503/429/503 sin configurar) y de seguridad (sin fuga de datos sensibles) | AGENT-BE-05 | Completado |
| P10 | Verificación: build Release, `dotnet format`, tres suites, `docker compose config` y stack local con `/health` en 200 y en 503 | AGENT-BE-05 | Completado |
| P11 | Corregir el CD-1: `GITHUB_OUTPUT` no existe en la sesión SSH (es del runner), por lo que el registro de la sonda se hace en el runner y no en el script remoto | AGENT-BE-01 | Completado |
| P12 | Reforzar la sonda: comprobar el acceso del usuario a la base configurada (código `13`) además del `ping` | AGENT-BE-01 | Completado |

## 2. Decisiones de diseño

| ID | Decisión | Motivo |
|---|---|---|
| D-HP-01 | Sonda en `Shared/Health/` (capa transversal), no en un módulo de negocio | Es una capacidad operativa de toda la API, no de Identity/Sellers/Admin; evita crear un módulo nuevo |
| D-HP-02 | `ApiError` se traslada a `Shared/Contracts/ApiError.cs` | El contrato de error único es transversal (`constitution.md` § 5.1) y no debe depender del módulo Identity |
| D-HP-03 | Las sondas de dependencia se aíslan detrás de interfaces `IMongoHealthProbe`/`IRedisHealthProbe` | Permite probar la clasificación de fallos sin MongoDB/Redis reales y sin tocar las capas de datos |
| D-HP-04 | El `503` de la sonda **no** enumera en el cuerpo qué dependencia falló | Evita filtrar el estado interno de la infraestructura; el detalle queda en logs correlacionados |
| D-HP-05 | El rate limit de la sonda usa la política distribuida `health` en Redis y **falla cerrada** | Coherente con § 6.1 de la constitución: sin Redis no hay evidencia de disponibilidad |
| D-HP-06 | El healthcheck de la API sigue siendo liveness TCP; el de Nginx pasa a readiness sobre `/health` | Un corte de Atlas no debe reiniciar réplicas ni dejar a Nginx sin arrancar (`depends_on: service_healthy`) |
| D-HP-07 | Se reutiliza `MongoDB.Driver` y `StackExchange.Redis`; sin paquetes NuGet nuevos | La constitución no admite tecnologías fuera del stack sin nueva versión |
| D-HP-08 | La sonda vive fuera del prefijo `/api/v1` | Es una ruta operativa del balanceador, no un recurso de negocio; así queda definida en `AGENTS.md` § 8 |
| D-HP-09 | El estado de `/health` se registra en `GITHUB_OUTPUT` **desde el runner** (una consulta SSH adicional) y no desde el script remoto | `GITHUB_OUTPUT` es una variable del runner: dentro de la sesión SSH `set -u` aborta el despliegue con `unbound variable` |
| D-HP-10 | La sonda valida el acceso a la base configurada con `listCollections` y solo un error de autorización (código `13`) es decisivo | Un `ping` correcto no garantiza permisos sobre la base: sin ellos la API responde `503` en sus endpoints de datos y la sonda reportaría `Healthy` de forma engañosa |

## 3. Verificación

```powershell
dotnet build UTTTGO.Backend.slnx -c Release --no-restore
dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore
dotnet test tests/UtttGo.UnitTests/UtttGo.UnitTests.csproj -c Release --no-build
dotnet test tests/UtttGo.IntegrationTests/UtttGo.IntegrationTests.csproj -c Release --no-build
dotnet test tests/UtttGo.SecurityTests/UtttGo.SecurityTests.csproj -c Release --no-build
docker compose config
```

Evidencia adicional en el despliegue: `GET /health` a través de Nginx devuelve `200` con Mongo y Redis operativos, y `503` con el contrato de error cuando Mongo no responde.

## 4. Trazabilidad

| Artefacto | Ubicación |
|---|---|
| Contrato de sonda | `src/UTTTGO.Api/Shared/Health/HealthContracts.cs` |
| Servicio de sonda | `src/UTTTGO.Api/Shared/Health/HealthService.cs` |
| Sondas de dependencia | `src/UTTTGO.Api/Shared/Health/DependencyProbes.cs` |
| Controlador | `src/UTTTGO.Api/Shared/Health/HealthController.cs` |
| Contrato de error transversal | `src/UTTTGO.Api/Shared/Contracts/ApiError.cs` |
| Política de rate limit | `src/UTTTGO.Api/Shared/Infrastructure/RedisOptions.cs`, `appsettings.json` |
| Healthcheck de Nginx | `docker-compose.yml` |
| Evidencia de CD | `.github/workflows/cd-deploy.yml` |
