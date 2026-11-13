# API-014 — Editar producto propio · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Producto del vendedor; no implementar reservas de Orders ni media desde Catalog.

## Tareas ejecutables sin cambiar contrato

Ninguna tarea técnica READY: payload y concurrencia stock pendientes de acta Sprint 2.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API014-01 · DOCUMENTAL aprobar PATCH y stock | RF-04/09/10, UPD-01/02/03/04; solo `spec.md`, `plan.md`, `task.md` tras decisión | TASK-CATALOG-002: producto/API/Orders/seguridad ratifican campos/omisiones, actor Admin, HTTP/conflicto, versión y reserva de stock; cotejar Excel. **Hecho**: acta y validador ficha con 0 errores, interfaz Orders definida sin implementarla aquí. | BLOCKED |
| DEV-API014-02 · IMPLEMENTACION PATCH autorizado | RF-04, UPD-01/02/04; `Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/UpdateProductRequest.cs`, `Validators/UpdateProductRequestValidator.cs`, `Application/UpdateProductService.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API014-01, modelo API-013 y contrato Seller: validar body y propietario, modificar solo campos aprobados, no `sellerId`/status/media; DI sin reordenar middleware. **Hecho**: `UpdateProductServiceTests.cs` cubre dueño/ajeno y PATCH parcial, build Release verde. | BLOCKED |
| DEV-API014-03 · IMPLEMENTACION escritura concurrente | RF-04/10, UPD-03; `Modules/Catalog/Infrastructure/MongoProductRepository.cs`, `Application/UpdateProductService.cs`, `tests/UtttGo.IntegrationTests/UpdateProductEndpointTests.cs` | Tras DEV-API014-01/02 y política stock Orders: CAS por versión/propietario y coherencia de caché; no reservar pedidos desde PATCH. **Hecho**: prueba Mongo aislada dos actualizaciones en carrera y reserva simulada sin sobreventa/pérdida, resultado/HTTP conforme acta. | BLOCKED |
| DEV-API014-04 · QA_TECNICA seguridad PATCH | BR-009, UPD-01/02/04; `tests/UtttGo.IntegrationTests/UpdateProductEndpointTests.cs`, `tests/UtttGo.SecurityTests/UpdateProductSecurityTests.cs` | Tras DEV-API014-02/03 y permiso Mongo/Redis local: probar Buyer/ajeno, extra sellerId/imagen, precio inválido/stock negativo, JWT revocado, 429/503. **Hecho**: evidencia fechada, build/formato y tres suites Release verdes sin servicios productivos. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); confirmar acta y API-013. No confundir stock editable con total del pedido.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API014-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: TASK-CATALOG-002 y política Orders | - | PENDIENTE |
| DEV-API014-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: contrato Sprint 2, modelo API-013 y vendedor propio | DEV-API014-01 | PENDIENTE |
| DEV-API014-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: política de stock/concurrencia Orders aprobada | DEV-API014-01, DEV-API014-02 | PENDIENTE |
| DEV-API014-04 | QA_TECNICA | BLOCKED | SI | Pendiente: permiso Mongo/Redis local, Sprint 2 | DEV-API014-02, DEV-API014-03 | PENDIENTE |
