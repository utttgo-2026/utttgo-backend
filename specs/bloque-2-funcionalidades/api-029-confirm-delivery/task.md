# API-029 — Confirmar entrega y efectivo · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. No procesar pagos electrónicos ni escribir reseñas desde Orders.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: D-ORD-02/03/06 y contrato Reviews no aprobados.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API029-01 · DOCUMENTAL ratificar confirmación | §13, DEL-01..05; `spec.md`, `plan.md`, `task.md` tras acta | TASK-ORD-001..004, D-ORD-02/03/06/08/10: decidir token, cobro, CAS, HTTP, recovery/commit y hecho Reviews; cotejar Excel. **Hecho**: acta y validador individual 0 errores. | BLOCKED |
| DEV-API029-02 · IMPLEMENTACION autorización/consumo | RF-ORD-24/25, DEL-01..04; `Modules/Orders/Controllers/OrdersController.cs`, `Contracts/DeliveryConfirmationRequest.cs`, `Contracts/DeliveryConfirmationResponse.cs`, `Validators/DeliveryConfirmationRequestValidator.cs`, `Application/ConfirmDeliveryService.cs`, `Infrastructure/MongoOrderRepository.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API029-01, API-028 y pedido Ready: validar vendedor/token/hash/expiry, CAS estado+uso+versión en Orders. **Hecho**: `ConfirmDeliveryServiceTests.cs` cubre falsos/expirados/usados y build Release verde. | BLOCKED |
| DEV-API029-03 · IMPLEMENTACION stock y auditoría | RF-ORD-26/32, DEL-01/02/05; `Modules/Orders/Application/ConfirmDeliveryService.cs`, `Infrastructure/MongoOrderAuditRepository.cs`, `MongoOrderRepository.cs` | Tras DEV-API029-02 y Catalog: commit idempotente por reserva, fechas/auditoría/reconciliación según D-ORD-02. **Hecho**: `ConfirmDeliveryEndpointTests.cs` con ledger confirma una sola vez aun con fallos/retry. | BLOCKED |
| DEV-API029-04 · INTEGRACION hecho de entrega | RF-ORD-26/29, DEL-01/05; `Modules/Orders/Application/ConfirmDeliveryService.cs`, `tests/UtttGo.IntegrationTests/ConfirmDeliveryEndpointTests.cs` | Tras DEV-API029-01/03 y contratos publicados Reviews/Notifications: publicar hecho entregado una vez y notificar por interfaz, sin escribir colección Reviews ni enviar FCM directo; prever entrega recuperable. **Hecho**: dobles prueban hecho exactamente una vez tras retry/fallo; no crean reseña. | BLOCKED |
| DEV-API029-05 · QA_TECNICA seguridad/fallo parcial | T-ORD-14/15/16, DEL-01..05; `tests/UtttGo.UnitTests/ConfirmDeliveryServiceTests.cs`, `tests/UtttGo.IntegrationTests/ConfirmDeliveryEndpointTests.cs`, `tests/UtttGo.SecurityTests/ConfirmDeliverySecurityTests.cs` | Tras DEV-API029-02..04: token replay/carreras, rol ajeno, revocado/Redis caído y fallos Catalog/Reviews. Hecho: evidencia fechada build/formato/tres suites Release verdes sin secretos ni producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), [guía](../../../docs/sdd-guia-api-atomica.md); comprobar acta y usar solo entorno aislado.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API029-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: TASK-ORD-001..004 y D-ORD-02/03/06/08/10 | - | PENDIENTE |
| DEV-API029-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: API-028 y acta token/cobro | DEV-API029-01 | PENDIENTE |
| DEV-API029-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: Catalog y protocolo compensación | DEV-API029-02 | PENDIENTE |
| DEV-API029-04 | INTEGRACION | BLOCKED | SI | Pendiente: hecho Reviews/Notifications aprobado | DEV-API029-03 | PENDIENTE |
| DEV-API029-05 | QA_TECNICA | BLOCKED | SI | Pendiente: entorno aislado y gates anteriores | DEV-API029-02/03/04 | PENDIENTE |
