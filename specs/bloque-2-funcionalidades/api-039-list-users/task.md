# API-039 — Listar usuarios y vendedores · task

Estado de aprobación de la ficha: PARCIAL

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Compila TASK-039-001/002/003/010..013/020/021 del [trío phase-02](../../phase-02-auth-users/api-039-list-users/task.md) sin sustituirlo; no implementa API-040 ni cierra AC-005 por listar.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: la implementación local con dobles ya se ejecutó (2026-11-04); quedan verificación de modelo, adaptadores reales e integración.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado esperado; prueba/comando y evidencia | Estado |
|---|---|---|---|
| DEV-API039-01 · DOCUMENTAL verificar modelo de lectura | API-039, ADMU-02/05/07; `spec.md`, `plan.md`, `task.md` | Depende de la ratificación local 2026-11-04: confirmar `Suspended`, `createdAt`, ID y normalización de `displayName` antes de índices; si hay incompatibilidad, detener solo el filtro afectado. **Hecho**: informe de verificación con resultado fechado y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-039-list-users` sin errores. | BLOCKED |
| DEV-API039-02 · IMPLEMENTACION contrato de lectura | API-039, ADMU-01/02/07; `src/UTTTGO.Api/Modules/Identity/Application/IAdminUserListReader.cs` | Tras autorización local recibida 2026-11-04: Identity publica `IAdminUserListReader` con fecha requerida y proyección mínima, separada de `FindBySubjectAsync`; sin índices Mongo (quedan en integración). **Hecho**: interfaz publicada con resultado verificado por `AdminUsersTests.cs` y evidencia en [qa-api-039](../../../docs/qa-api-039.md) 2026-11-04. | DONE |
| DEV-API039-03 · IMPLEMENTACION endpoint autorizado | API-039, ADMU-01..05/07/08; `src/UTTTGO.Api/Modules/Admin/Controllers/AdminUsersController.cs`, `src/UTTTGO.Api/Modules/Admin/Application/AdminUsersService.cs`, `src/UTTTGO.Api/Program.cs` | Depende de DEV-API039-02: ruta con query acotada, rol Admin local, rate limit y auditoría antes del `200`; middleware intacto. **Hecho**: `AdminUsersEndpointTests.cs` cubre `200/400/401/403` con resultado y evidencia en [qa-api-039](../../../docs/qa-api-039.md) 2026-11-04. | DONE |
| DEV-API039-04 · QA_TECNICA dobles locales | API-039, ADMU-01..08; `tests/UtttGo.UnitTests/AdminUsersTests.cs`, `tests/UtttGo.IntegrationTests/AdminUsersEndpointTests.cs`, `tests/UtttGo.SecurityTests/AdminUsersSecurityTests.cs` | Depende de DEV-API039-03: restore, build Release, formato y suites completas con dobles solo en tests. **Hecho**: resultado completo con cifras y fecha 2026-11-04 en [qa-api-039](../../../docs/qa-api-039.md). | DONE |
| DEV-API039-05 · QA_TECNICA auditoría y tasa reales | API-039, ADMU-06/08; `src/UTTTGO.Api/Modules/Admin/Infrastructure/UnconfiguredAdminUsersDependencies.cs`, `src/UTTTGO.Api/Modules/Admin/Infrastructure/RedisAdminUsersRateLimitGate.cs` | Depende de DEV-API039-04: sustituir `UnconfiguredAdminUsersAudit` y gate por adaptadores reales sin relajar fail-closed. **Hecho**: prueba de auditoría persistente y `429`/`503` con resultado y evidencia fechada. | BLOCKED |
| DEV-API039-06 · INTEGRACION servicios no productivos | API-039, ADMU-01..08; suites de integración/seguridad existentes | Depende de DEV-API039-01/04/05: JWT real, blacklist y rate limit Redis compartidos, Supabase, Mongo con índices y auditoría con servicios no productivos; cierra TASK-039-020/021 sin declarar API-040 ni AC-005. **Hecho**: resultado de integración con evidencia fechada en enlace verificable. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), tareas y [guía](../../../docs/sdd-guia-api-atomica.md); comprobar Git, elegir solo tarea cuyas dependencias y autorización estén cumplidas; no inferir aprobación por código o dobles. El trío [phase-02](../../phase-02-auth-users/api-039-list-users/spec.md) conserva su evidencia histórica.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API039-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: verificación del modelo real (TASK-039-001) | - | PENDIENTE |
| DEV-API039-02 | IMPLEMENTACION | DONE | SI | Autorización local recibida 2026-11-04 (gate TASK-039-003) | - | 2026-11-04, [qa-api-039](../../../docs/qa-api-039.md): resultado de contrato de lectura publicado con prueba verde |
| DEV-API039-03 | IMPLEMENTACION | DONE | SI | Autorización local recibida 2026-11-04 (gate TASK-039-003) | DEV-API039-02 | 2026-11-04, [qa-api-039](../../../docs/qa-api-039.md): resultado de endpoint validado con dobles |
| DEV-API039-04 | QA_TECNICA | DONE | SI | Entorno local aislado con dobles autorizado 2026-11-04 | DEV-API039-03 | 2026-11-04, [qa-api-039](../../../docs/qa-api-039.md): resultado de build, formato y suites en verde |
| DEV-API039-05 | QA_TECNICA | BLOCKED | SI | Pendiente: sink de auditoría real y Redis de rate limit | DEV-API039-04 | PENDIENTE |
| DEV-API039-06 | INTEGRACION | BLOCKED | SI | Pendiente: servicios no productivos y gates TASK-039-020/021 | DEV-API039-01/04/05 | PENDIENTE |
