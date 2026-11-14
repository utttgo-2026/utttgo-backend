# API-008 — Perfil público de vendedor · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. GET ya existe; no crear segunda implementación ni atribuir a esta ficha bloqueo de pedidos.

## Tareas ejecutables sin cambiar contrato

No hay cambios técnicos READY mientras falte acta de acceso/composición y gates. Se registran tareas de decisión y QA concretas.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API008-01 · DOCUMENTAL ratificar perfil público | RF-13, AC-013, PUB-01/02/03; `spec.md`, `plan.md`, `task.md` | TASK-AUTH-005/026: obtener decisión de acceso, ID, DTO/HTTP, rating ausente y productos vía API-011 o lista paginada; cotejar Excel autorizado. **Hecho**: acta identificada y validador ficha sin errores; no tocar código por diferencias no ratificadas. | BLOCKED |
| DEV-API008-02 · INTEGRACION lector Ratings aprobado | RF-13, PUB-04; `src/UTTTGO.Api/Modules/Sellers/Application/ISellerProfileDependencies.cs`, DI Sellers y `tests/UtttGo.UnitTests/SellerProfileServiceTests.cs` | Tras DEV-API008-01, gates 007→008→010 y contrato Reviews disponible: consumir lector por interfaz, dejar null si sin datos/fallo; no escribir Reviews. **Hecho**: tests de ratings válidos/ausentes/fallo y proyección sin PII; build Release verde. | BLOCKED |
| DEV-API008-03 · QA_TECNICA verificar lectura real | AC-013, PUB-01/02/03/04; `tests/UtttGo.IntegrationTests/SellerProfileEndpointTests.cs`, `tests/UtttGo.SecurityTests/SellerProfileSecurityTests.cs` | Tras DEV-API008-01 y permisos Mongo/Redis locales: extender tests con contenedores para Active/Inactive/Pending, ID inválido, rate limit/caída y ausencia de PII; registrar dependencia externa Orders para no declarar pedidos bloqueados. **Hecho**: evidencia fechada de integración/seguridad, build, format, tres suites Release verdes, sin acceso a producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); contrastar código vigente antes de editar. Requerir gates y entornos aislados; QA con dobles no acredita Mongo/Redis reales.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API008-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: TASK-AUTH-005/026, acta de contrato | - | PENDIENTE |
| DEV-API008-02 | INTEGRACION | BLOCKED | SI | Pendiente: contrato Reviews, TASK-AUTH-007/008/010 y decisión de composición | DEV-API008-01 | PENDIENTE |
| DEV-API008-03 | QA_TECNICA | BLOCKED | SI | Pendiente: TASK-AUTH-005/007/008/010/026 y permiso local Mongo/Redis | DEV-API008-01 | PENDIENTE |
