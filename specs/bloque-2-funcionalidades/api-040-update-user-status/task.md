# API-040 — Cambiar estado de cuenta de usuario · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Ruta única de RF-14/T-AUTH-13; sin cambio de rol, sin borrado ([API-005](../api-005-delete-users-me/spec.md)) ni listado ([API-039](../api-039-list-users/spec.md)). Los IDs DEV no renumeran TASK históricos.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: el contrato HTTP/DTO de RF-14 no está ratificado y no hay implementación previa de mutación.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado esperado; prueba/comando y evidencia | Estado |
|---|---|---|---|
| DEV-API040-01 · DOCUMENTAL ratificar contrato de mutación | API-040, ADMS-01/04/06; `spec.md`, `plan.md`, `task.md` | Tras RF-14: fijar cuerpo `accountStatus`, códigos ante inexistente/mismo estado, efecto de `Suspended` sobre sesiones y orden transaccional sin ampliar a borrado (DEC-003). **Hecho**: acta identificable y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-040-update-user-status` sin errores. | BLOCKED |
| DEV-API040-02 · IMPLEMENTACION contrato de escritura y endpoint | API-040, ADMS-01/03/04; `src/UTTTGO.Api/Modules/Identity/Application/IAdminUserStatusWriter.cs`, `src/UTTTGO.Api/Modules/Admin/Controllers/AdminUsersStatusController.cs`, `src/UTTTGO.Api/Program.cs` | Depende de DEV-API040-01: Identity publica el escritor tras su frontera; Admin expone el PATCH sin colecciones cruzadas ni cambios de middleware. **Hecho**: `AdminUserStatusEndpointTests.cs` cubre transición válida y rechazos con build Release verde. | BLOCKED |
| DEV-API040-03 · IMPLEMENTACION guardas y auditoría | API-040, ADMS-02/03/05; `src/UTTTGO.Api/Modules/Admin/Application/AdminUserStatusService.cs`, `src/UTTTGO.Api/Modules/Admin/Infrastructure/UnconfiguredAdminUsersDependencies.cs` | Depende de DEV-API040-02: bloquear rol y cuenta propia (T-AUTH-13/CL-16) y auditar la mutación previa al `200` con fail-closed. **Hecho**: `AdminUserStatusTests.cs` cubre guardas y auditoría fallida con build Release verde. | BLOCKED |
| DEV-API040-04 · QA_TECNICA escenarios y fallos | API-040, ADMS-01..06; `tests/UtttGo.UnitTests/AdminUserStatusTests.cs`, `tests/UtttGo.IntegrationTests/AdminUserStatusEndpointTests.cs`, `tests/UtttGo.SecurityTests/AdminUserStatusSecurityTests.cs` | Depende de DEV-API040-02/03: JWT revocado, tasa excedida, Redis/Mongo caídos, cuenta suspendida sin operar y reactivada recuperando operación. **Hecho**: evidencia fechada de build, formato y tres suites en verde sin usar producción. | BLOCKED |
| DEV-API040-05 · INTEGRACION servicios no productivos | API-040, ADMS-01/02/06; suites de integración/seguridad existentes | Depende de DEV-API040-01/04: verificar mutación con JWT/Redis/Mongo/Supabase reales no productivos para AC-005 y AC-009. **Hecho**: resultado de integración con evidencia fechada en enlace verificable. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), tareas y [guía](../../../docs/sdd-guia-api-atomica.md); comprobar Git, elegir solo tarea cuyas dependencias y autorización estén cumplidas; no inferir aprobación por código o dobles.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API040-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: ratificación de HTTP/DTO de RF-14 | - | PENDIENTE |
| DEV-API040-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: acta de contrato y escritor de Identity | DEV-API040-01 | PENDIENTE |
| DEV-API040-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: efecto de `Suspended` y auditoría real | DEV-API040-02 | PENDIENTE |
| DEV-API040-04 | QA_TECNICA | BLOCKED | SI | Pendiente: entorno aislado y gates T-AUTH-13/CL-16 | DEV-API040-02/03 | PENDIENTE |
| DEV-API040-05 | INTEGRACION | BLOCKED | SI | Pendiente: servicios no productivos y AC-005/AC-009 | DEV-API040-01/04 | PENDIENTE |
