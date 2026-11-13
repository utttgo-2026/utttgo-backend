# API-013 — Publicar producto propio · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Falta implementación; tareas técnicas BLOCKED hasta contrato Sprint 2, no TODO documental sustituto.

## Tareas ejecutables sin cambiar contrato

No hay tareas técnicas READY: JSON, estado inicial/categoría y gate Sprint 2 pendientes.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API013-01 · DOCUMENTAL decidir creación | RF-01/02/03/09/11, CRT-01/02/03/04; solo `spec.md`, `plan.md`, `task.md` tras acta | TASK-CATALOG-002: ratificar DTO, límites, HTTP, stock cero, estado inicial, categoría e imagen excluida; cotejar Excel. **Hecho**: acta identificada, interfaz Seller/categoría definida y validador ficha 0 errores. | BLOCKED |
| DEV-API013-02 · IMPLEMENTACION POST y validador | RF-01/03, CRT-01/03; `Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/CreateProductRequest.cs`, `Contracts/ProductResponse.cs`, `Validators/CreateProductRequestValidator.cs`, `Application/CreateProductService.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API013-01 y contratos Sellers/categoría: implementar JWT/rol local, whitelist, precio decimal/stock y `sellerId` server-side; ninguna imagen/oferta. **Hecho**: `CreateProductServiceTests.cs` verifica propietario y validación, build Release verde. | BLOCKED |
| DEV-API013-03 · IMPLEMENTACION persistir producto propio | RF-02/03, CRT-01/02; `Modules/Catalog/Infrastructure/ProductDocument.cs`, `MongoProductRepository.cs`, `CatalogMongoIndexes.cs`, `tests/UtttGo.IntegrationTests/CreateProductEndpointTests.cs` | Tras DEV-API013-01/02: crear colección `products`, índice solo por consultas aprobadas, insert con ID/UTC/version; dos vendedores homónimos no chocan. **Hecho**: integración Mongo local comprueba dos documentos propietarios distintos y mismo nombre/categoría; Release verde. | BLOCKED |
| DEV-API013-04 · QA_TECNICA control de publicación | RF-09/11, CRT-03/04; `tests/UtttGo.IntegrationTests/CreateProductEndpointTests.cs`, `tests/UtttGo.SecurityTests/CreateProductSecurityTests.cs` | Tras DEV-API013-02/03 y permiso Mongo/Redis local: probar Buyer, categoría inactiva, extras sellerId/roles/status, null, asset no aprobado y Redis caído; sin escritura indebida. **Hecho**: evidencia fechada, build/formato y tres suites Release verdes, sin producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); confirmar acta e interfaces dueñas, sin reintroducir ofertas ni editar infraestructura.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API013-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: TASK-CATALOG-002 y acta Sprint 2 | - | PENDIENTE |
| DEV-API013-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: DEV-API013-01 y contratos Seller/categoría | DEV-API013-01 | PENDIENTE |
| DEV-API013-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: modelo/índices aprobados y permiso Mongo aislado | DEV-API013-01, DEV-API013-02 | PENDIENTE |
| DEV-API013-04 | QA_TECNICA | BLOCKED | SI | Pendiente: Sprint 2, pruebas locales autorizadas y API-019..022 | DEV-API013-02, DEV-API013-03 | PENDIENTE |
