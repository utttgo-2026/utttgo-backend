# API-012 — Detalle de producto · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Sin contrato Sprint 2 aprobado no hay programación READY; comparte modelo con API-013.

## Tareas ejecutables sin cambiar contrato

Ninguna tarea técnica READY: visibilidad/DTO/ID y gates sin aprobar.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API012-01 · DOCUMENTAL aprobar detalle | RF-03/07/08/09, DET-01/02/03/04; solo `spec.md`, `plan.md`, `task.md` tras acta | TASK-CATALOG-002 y Sprint 2: solicitar decisión de visibilidad, ID, ratings, ausencia Media, DTO/HTTP y cotejo Excel. **Hecho**: acta identificada y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-012-product-detail` con 0 errores. | BLOCKED |
| DEV-API012-02 · IMPLEMENTACION GET por productId | RF-03/07, DET-01/02/03; `Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/ProductDetailResponse.cs`, `Validators/ProductIdValidator.cs`, `Application/GetProductService.cs`, `Infrastructure/MongoProductRepository.cs`, registro en `src/UTTTGO.Api/Program.cs` | Tras DEV-API012-01 y modelo API-013: añadir GET y proyección sin Mongo de Sellers; estados/ID según acta, sin oferta/media inventada. **Hecho**: `GetProductServiceTests.cs` y `GetProductEndpointTests.cs` cubren encontrado/oculto/ausente, build Release verde. | BLOCKED |
| DEV-API012-03 · QA_TECNICA seguridad y dependencias | RF-08/09/13, DET-01/02/03/04; `tests/UtttGo.IntegrationTests/GetProductEndpointTests.cs`, `tests/UtttGo.SecurityTests/GetProductSecurityTests.cs` | Tras DEV-API012-02, permiso Mongo/Redis local: probar rate limit, caída, ID inválido, producto pausado, categoría oculta, ratings no disponibles y ausencia PII; Sellers/Reviews solo por interfaces. **Hecho**: evidencia fechada de tres suites Release, build/formato verdes, sin producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); verificar acta/modelo, no duplicar repositorio de productos ni programar Media/Reviews.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API012-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: TASK-CATALOG-002 y acta Sprint 2 | - | PENDIENTE |
| DEV-API012-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: contrato API-012 y modelo API-013 ratificados | DEV-API012-01 | PENDIENTE |
| DEV-API012-03 | QA_TECNICA | BLOCKED | SI | Pendiente: Sprint 2, permiso Mongo/Redis aislados y contratos dueños | DEV-API012-02 | PENDIENTE |
