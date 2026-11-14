# API-041 — Moderar reseña · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Mutación vía contrato del dueño de Reviews; sin crear/editar reseñas ni moderar imágenes. Los IDs DEV no renumeran TASK históricos.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: la fuente de estados de moderación (fase-10 o fase Reviews) no existe y la moderación de texto no está aprobada.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado esperado; prueba/comando y evidencia | Estado |
|---|---|---|---|
| DEV-API041-01 · DOCUMENTAL definir contrato de moderación | API-041, ADMR-01/02/04; `spec.md`, `plan.md`, `task.md` | Tras fuente aprobada: fijar estados y transiciones, DTO del PATCH, códigos ante inexistente/transición inválida y efecto en API-031; sin inventar payload (API-030). **Hecho**: acta identificable y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-041-moderate-review` sin errores. | BLOCKED |
| DEV-API041-02 · IMPLEMENTACION contrato de Reviews y endpoint | API-041, ADMR-01/02/04; `src/UTTTGO.Api/Modules/Reviews/…`, `src/UTTTGO.Api/Modules/Admin/Controllers/AdminReviewModerationController.cs`, `src/UTTTGO.Api/Program.cs` | Depende de DEV-API041-01: Reviews publica `IReviewModerationWriter` tras su frontera; Admin expone el PATCH sin colecciones cruzadas ni cambios de middleware. **Hecho**: `ReviewModerationEndpointTests.cs` cubre transición válida y `404` con build Release verde. | BLOCKED |
| DEV-API041-03 · IMPLEMENTACION auditoría y guardas | API-041, ADMR-03/05; `src/UTTTGO.Api/Modules/Admin/Application/ReviewModerationService.cs`, `src/UTTTGO.Api/Modules/Admin/Infrastructure/UnconfiguredAdminUsersDependencies.cs` | Depende de DEV-API041-02: rol Admin local, tasa distribuida y auditoría previa al `200` con fail-closed. **Hecho**: `ReviewModerationTests.cs` cubre guardas y auditoría fallida con build Release verde. | BLOCKED |
| DEV-API041-04 · QA_TECNICA escenarios y fallos | API-041, ADMR-01..05; `tests/UtttGo.UnitTests/ReviewModerationTests.cs`, `tests/UtttGo.IntegrationTests/ReviewModerationEndpointTests.cs`, `tests/UtttGo.SecurityTests/ReviewModerationSecurityTests.cs` | Depende de DEV-API041-02/03: JWT revocado, rol forjado, reseña ajena, transición inválida, Redis/Mongo caídos y rate limit. **Hecho**: evidencia fechada de build, formato y tres suites en verde sin usar producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), tareas y [guía](../../../docs/sdd-guia-api-atomica.md); comprobar Git, elegir solo tarea cuyas dependencias y autorización estén cumplidas; no inferir aprobación por código o dobles.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API041-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: fuente de estados de moderación (fase-10) | - | PENDIENTE |
| DEV-API041-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: acta de contrato y módulo Reviews | DEV-API041-01 | PENDIENTE |
| DEV-API041-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: auditoría real y tasa de mutación | DEV-API041-02 | PENDIENTE |
| DEV-API041-04 | QA_TECNICA | BLOCKED | SI | Pendiente: entorno aislado y estados ratificados | DEV-API041-02/03 | PENDIENTE |
