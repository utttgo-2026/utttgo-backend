# API-024 — Listar mis pedidos · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Sin fase 04 aprobada ni orders no hay código READY; el vendedor no tiene filtro aprobado.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: D-ORD-05 y gates fase 04/Catalog pendientes.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API024-01 · DOCUMENTAL ratificar alcance | RF-ORD-30, MINE-01/02/03/04; solo `spec.md`, `plan.md`, `task.md` tras acta | TASK-ORD-001..004: propietario/API decide D-ORD-05 (comprador o vendedor por `role`), pageSize, status, orden, DTO/HTTP y coteja Excel; D-ORD-09 separado. **Hecho**: acta específica y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-024-list-my-orders` sin errores. | BLOCKED |
| DEV-API024-02 · IMPLEMENTACION GET comprador | RF-ORD-08/30/31, MINE-01/03; `Modules/Orders/Controllers/OrdersController.cs`, `Contracts/OrdersMineQuery.cs`, `Contracts/OrdersPageResponse.cs`, `Validators/OrdersMineQueryValidator.cs`, `Application/ListOwnOrdersService.cs`, `Infrastructure/MongoOrderRepository.cs`, `OrderMongoIndexes.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API024-01 y modelo API-023: filtrar `buyerUserId` desde Identity, paginar/ordenar en Mongo, proyectar solo propios; registrar DI. **Hecho**: `ListOwnOrdersServiceTests.cs` y `ListOwnOrdersEndpointTests.cs` muestran página 2 de 25 sin terceros, build Release verde. | BLOCKED |
| DEV-API024-03 · INTEGRACION participante Seller condicional | RF-ORD-30, MINE-02; `Application/ListOwnOrdersService.cs`, `Infrastructure/MongoOrderRepository.cs`, `tests/UtttGo.IntegrationTests/ListOwnOrdersEndpointTests.cs` | Tras DEV-API024-01/02 y D-ORD-05 aprobada: si extensión elegida, resolver sellerId vía interfaz Sellers y filtrar por `sellerId` sin aceptar otro del query; si rechazada, no implementar y documentar exclusión. **Hecho**: prueba local vendedor propio vs ajeno (o decisión formal de no aplicar). | BLOCKED |
| DEV-API024-04 · QA_TECNICA aislamiento y degradación | RF-ORD-27/32, MINE-01/03/04; `tests/UtttGo.IntegrationTests/ListOwnOrdersEndpointTests.cs`, `tests/UtttGo.SecurityTests/ListOwnOrdersSecurityTests.cs` | Tras DEV-API024-02/03 y permisos Mongo/Redis: JWT revocado, paginación inválida, rate limit caído y ausencia de PII; D-ORD-09 si se aprueba se prueba en tarea de borrado de su dueño. **Hecho**: evidencia fechada build/formato/tres suites Release verdes sin producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); esperar acta, no añadir otra ruta de listado ni asumir decisión vendedor.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API024-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: TASK-ORD-001..004 y D-ORD-05 | - | PENDIENTE |
| DEV-API024-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: fase 04 aprobada y modelo API-023 | DEV-API024-01 | PENDIENTE |
| DEV-API024-03 | INTEGRACION | BLOCKED | SI | Pendiente: D-ORD-05 y contrato Sellers, o exclusión formal | DEV-API024-01, DEV-API024-02 | PENDIENTE |
| DEV-API024-04 | QA_TECNICA | BLOCKED | SI | Pendiente: permiso Mongo/Redis local y fase 04 aprobada | DEV-API024-02, DEV-API024-03 | PENDIENTE |
