# API-042 — Reportes semanales administrativos · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Reporte semanal agregado consumiendo contratos de los módulos dueños; sin PII ni colecciones cruzadas. Los IDs DEV no renumeran TASK históricos.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: el alcance de métricas, la ventana de semana y la autorización de reportes siguen por definir.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado esperado; prueba/comando y evidencia | Estado |
|---|---|---|---|
| DEV-API042-01 · DOCUMENTAL definir alcance del reporte | API-042, RPTW-01/03/05; `spec.md`, `plan.md`, `task.md` | Tras fuente aprobada: fijar catálogo de métricas, unidades, ventana horaria, parámetros de semana y códigos de error, agregando sin PII. **Hecho**: acta identificable y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-042-weekly-reports` sin errores. | BLOCKED |
| DEV-API042-02 · IMPLEMENTACION contratos de métricas y servicio | API-042, RPTW-01/04/05; `src/UTTTGO.Api/Modules/Orders/…`, `src/UTTTGO.Api/Modules/Reviews/…`, `src/UTTTGO.Api/Modules/Sellers/…`, `src/UTTTGO.Api/Modules/Admin/Application/WeeklyReportService.cs` | Depende de DEV-API042-01: cada dueño publica su interfaz de métricas; Admin agrega sin colecciones cruzadas ni parciales incompletos. **Hecho**: `WeeklyReportTests.cs` cubre agregación y degradación con build Release verde. | BLOCKED |
| DEV-API042-03 · IMPLEMENTACION autorización y sin-PII | API-042, RPTW-02/05; `src/UTTTGO.Api/Modules/Admin/Controllers/AdminWeeklyReportController.cs`, `src/UTTTGO.Api/Program.cs` | Depende de DEV-API042-02: rol local `Admin`, tasa distribuida y respuesta con solo agregados; middleware intacto. **Hecho**: `WeeklyReportEndpointTests.cs` cubre `200`/`403` y búsqueda de PII con build Release verde. | BLOCKED |
| DEV-API042-04 · QA_TECNICA escenarios y fallos | API-042, RPTW-01..05; `tests/UtttGo.UnitTests/WeeklyReportTests.cs`, `tests/UtttGo.IntegrationTests/WeeklyReportEndpointTests.cs`, `tests/UtttGo.SecurityTests/WeeklyReportSecurityTests.cs` | Depende de DEV-API042-02/03: JWT revocado, semana inválida, Redis/Mongo caídos, rate limit y ausencia de PII. **Hecho**: evidencia fechada de build, formato y tres suites en verde sin usar producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), tareas y [guía](../../../docs/sdd-guia-api-atomica.md); comprobar Git, elegir solo tarea cuyas dependencias y autorización estén cumplidas; no inferir aprobación por código o dobles.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API042-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: catálogo de métricas y ventana de semana | - | PENDIENTE |
| DEV-API042-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: contratos de métricas de los módulos dueños | DEV-API042-01 | PENDIENTE |
| DEV-API042-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: política de autorización de reportes | DEV-API042-02 | PENDIENTE |
| DEV-API042-04 | QA_TECNICA | BLOCKED | SI | Pendiente: entorno aislado y alcance ratificado | DEV-API042-02/03 | PENDIENTE |
