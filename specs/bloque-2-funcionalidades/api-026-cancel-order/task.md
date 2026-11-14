# API-026 — Cancelar pedido propio · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. No reabrir Accepted sin decisión D-ORD-07 ni implementar liberación Catalog desde Orders.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: protocolo stock/CAS y gates fase 04/Sprint 2 pendientes.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API026-01 · DOCUMENTAL ratificar cancelación | RF-ORD-19/20/32, CAN-01/02/03/04/05; solo `spec.md`, `plan.md`, `task.md` tras acta | TASK-ORD-001..004: propietario/Catalog/Orders decide D-ORD-02/06/07/08, orden de liberar/CAS, idempotencia, fallo/crash/HTTP y coteja Excel. **Hecho**: acta explícita sobre Accepted y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-026-cancel-order` 0 errores. | BLOCKED |
| DEV-API026-02 · IMPLEMENTACION guard/transición | RF-ORD-19/20, CAN-01/03/04; `Modules/Orders/Controllers/OrdersController.cs`, `Contracts/CancelOrderResponse.cs`, `Validators/OrderIdValidator.cs`, `Application/CancelOrderService.cs`, `Infrastructure/MongoOrderRepository.cs`, DI `src/UTTTGO.Api/Program.cs` | Tras DEV-API026-01 y pedido API-023: comparar `buyerUserId`, CAS solo Pending por defecto, 409 en otros estados, sin doble efecto. **Hecho**: `CancelOrderServiceTests.cs` verifica comprador/tercero/Accepted y estado, build Release verde. | BLOCKED |
| DEV-API026-03 · IMPLEMENTACION liberar y auditar | RF-ORD-19/26/29, CAN-01/02/05; `Application/CancelOrderService.cs`, `Infrastructure/MongoOrderAuditRepository.cs`, `MongoOrderRepository.cs`, `tests/UtttGo.IntegrationTests/CancelOrderEndpointTests.cs` | Tras DEV-API026-01/02 y comando Catalog: coordinar liberación por reservationId idempotente, `completedAt`/auditoría y evento Notifications vía contrato solo según acta; no FCM directo. **Hecho**: prueba ledger Catalog doble reintento libera una sola vez; auditoría persistida y respuesta coherente. | BLOCKED |
| DEV-API026-04 · QA_TECNICA seguridad/concurrencia | RF-ORD-15/18/20/27, CAN-02/03/04; `tests/UtttGo.IntegrationTests/CancelOrderEndpointTests.cs`, `tests/UtttGo.SecurityTests/CancelOrderSecurityTests.cs` | Tras DEV-API026-02/03, permiso Mongo/Redis local: doble cancel simultáneo, JWT revocado, seller/tercero, Accepted/Ready, rate limit caída; sin doble stock. **Hecho**: evidencia fechada build/formato/tres suites Release verdes. | BLOCKED |
| DEV-API026-05 · QA_TECNICA recuperación fallos | RF-ORD-32, CAN-05; `tests/UtttGo.IntegrationTests/CancelOrderEndpointTests.cs`, `tests/UtttGo.UnitTests/CancelOrderServiceTests.cs` | Tras DEV-API026-03/04 y política D-ORD-02: inyectar caída Catalog tras CAS y caída Mongo/auditoría tras liberar, probar reconciliación sin estado falso/reserva colgada. **Hecho**: artefacto fechado con estados finales/stock, comando de suites Release y sin producción ni secretos. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); confirmar TASK-ORD/D-ORD y reserva API-023; probar con dobles y contenedores no productivos.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API026-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: TASK-ORD-001..004 y D-ORD-02/06/07/08 | - | PENDIENTE |
| DEV-API026-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: fase 04, API-023 y protocolo CAS | DEV-API026-01 | PENDIENTE |
| DEV-API026-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: comandos Catalog y D-ORD-02/08 | DEV-API026-01, DEV-API026-02 | PENDIENTE |
| DEV-API026-04 | QA_TECNICA | BLOCKED | SI | Pendiente: permiso Mongo/Redis aislado y fase 04 | DEV-API026-02, DEV-API026-03 | PENDIENTE |
| DEV-API026-05 | QA_TECNICA | BLOCKED | SI | Pendiente: política reconciliación D-ORD-02 y permiso local | DEV-API026-03, DEV-API026-04 | PENDIENTE |
