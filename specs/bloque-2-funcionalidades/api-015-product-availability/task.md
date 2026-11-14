# API-015 — Cambiar disponibilidad de producto · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Pausa no equivale a stock cero o borrado; ninguna tarea técnica READY antes del Sprint 2.

## Tareas ejecutables sin cambiar contrato

No hay tarea técnica READY: estados/transiciones y contrato Orders requieren aprobación.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API015-01 · DOCUMENTAL aprobar disponibilidad | RF-05/09/10, AVL-01/02/03/04; solo `spec.md`, `plan.md`, `task.md` tras acta | TASK-CATALOG-002: producto/API/Catalog/Orders definen pausa, reactivación, stock cero vs SoldOut, Deleted, idempotencia/HTTP, CAS e invalidación; cotejar Excel. **Hecho**: acta e interfaz elegibilidad ratificada, validador individual 0 errores. | BLOCKED |
| DEV-API015-02 · IMPLEMENTACION pausa propia | RF-05/09, AVL-01/03; `Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/ChangeProductAvailabilityRequest.cs`, `Validators/ChangeProductAvailabilityValidator.cs`, `Application/ChangeProductAvailabilityService.cs`, `Infrastructure/MongoProductRepository.cs`, registro `src/UTTTGO.Api/Program.cs` | Tras DEV-API015-01 y modelo API-013: validar propietario y transición condicional sin cambiar precio/stock; no habilitar borrado. **Hecho**: `ChangeProductAvailabilityServiceTests.cs` y `ChangeProductAvailabilityEndpointTests.cs` prueban pausa propia/ajena y stock intacto, build Release verde. | BLOCKED |
| DEV-API015-03 · IMPLEMENTACION reactivar y cache coherente | RF-05/09, AVL-02/04; `Application/ChangeProductAvailabilityService.cs`, `Infrastructure/MongoProductRepository.cs`, Redis read gate Catalog, `tests/UtttGo.IntegrationTests/ChangeProductAvailabilityEndpointTests.cs` | Tras DEV-API015-01/02 y contrato API-011/Orders: implementar reactivación solo si aprobada, sin reactivar Deleted ni confundir stock cero; invalidar cache o aplicar mecanismo ratificado. **Hecho**: integración local cubre stock cero/categoría oculta y cache tras cambio con resultados fechados. | BLOCKED |
| DEV-API015-04 · QA_TECNICA concurrencia y seguridad | BR-009, AVL-01/03/04; `tests/UtttGo.IntegrationTests/ChangeProductAvailabilityEndpointTests.cs`, `tests/UtttGo.SecurityTests/ChangeProductAvailabilitySecurityTests.cs` | Tras DEV-API015-02/03 y permiso Mongo/Redis local: probar JWT revocado/Buyer/ajeno/Deleted, Redis caído, 429 y carrera pedido con interfaz Orders controlada. **Hecho**: evidencia fechada con build/formato y tres suites Release verdes, sin servicio productivo. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); exigir acta Sprint 2 e interfaz Orders. No introducir horario, DELETE ni enum inventado.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API015-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: TASK-CATALOG-002 y política de disponibilidad/Orders | - | PENDIENTE |
| DEV-API015-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: aprobación Sprint 2 y modelo API-013 | DEV-API015-01 | PENDIENTE |
| DEV-API015-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: transición de reactivación e invalidación aprobadas | DEV-API015-01, DEV-API015-02 | PENDIENTE |
| DEV-API015-04 | QA_TECNICA | BLOCKED | SI | Pendiente: contrato Orders y permiso Mongo/Redis local | DEV-API015-02, DEV-API015-03 | PENDIENTE |
