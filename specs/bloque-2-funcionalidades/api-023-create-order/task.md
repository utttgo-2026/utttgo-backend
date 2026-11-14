# API-023 — Crear pedido directo · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Fase 04 v0.1 no aprobada; toda implementación BLOCKED hasta acta y Catalog Sprint 2.

## Tareas ejecutables sin cambiar contrato

Ninguna tarea técnica READY: D-ORD-01/02/04/06 y contratos stock sin aprobación.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API023-01 · DOCUMENTAL ratificar compra | BR-001/002/003/009/010; ORD-01/02/03/04/05; solo `spec.md`, `plan.md`, `task.md` tras acta | TASK-ORD-001..004: propietario/Catalog/Orders aprueban D-ORD-01/02/04/06, payload, reserva/retry/precio atómico, HTTP y cotejo Excel; D-ORD-09 separado. **Hecho**: acta identificable y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-023-create-order` sin errores. | BLOCKED |
| DEV-API023-02 · IMPLEMENTACION POST y DTO | RF-ORD-10/14, ORD-01/05; `Modules/Orders/Controllers/OrdersController.cs`, `Contracts/CreateOrderRequest.cs`, `Contracts/OrderResponse.cs`, `Validators/CreateOrderRequestValidator.cs`, `Application/CreateOrderService.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API023-01, perfil Identity/reader Catalog y Seller aprobados: derivar actor/importe decimal Cash server-side, rechazar/ignorar extras solo conforme acta. **Hecho**: `CreateOrderServiceTests.cs` verifica cálculo y whitelist, build Release verde. | BLOCKED |
| DEV-API023-03 · IMPLEMENTACION reserva y persistencia | RF-ORD-10/13/32, ORD-01/02/04; `Modules/Orders/Infrastructure/OrderDocument.cs`, `MongoOrderRepository.cs`, `OrderMongoIndexes.cs`, `MongoOrderAuditRepository.cs`, `Application/CreateOrderService.cs` | Tras DEV-API023-01/02 y comandos Catalog disponibles: reservar una vez con ID, insertar Pending/registro de auditoría, compensar/reconciliar fallos según D-ORD-02; no escribir products. **Hecho**: integración `CreateOrderEndpointTests.cs` con Mongo local + Catalog controlado demuestra stock insuficiente, éxito y fallo de insert sin reserva huérfana. | BLOCKED |
| DEV-API023-04 · IMPLEMENTACION idempotencia aprobada | RF-ORD-28, ORD-03; `Modules/Orders/Infrastructure/MongoOrderRepository.cs`, `OrderMongoIndexes.cs`, `Application/CreateOrderService.cs`, `tests/UtttGo.IntegrationTests/CreateOrderEndpointTests.cs` | Tras DEV-API023-01/03 y D-ORD-04: si se aprueba clave, aplicar índice parcial/replay bajo carrera sin segunda reserva; si se rechaza, conciliar tarea con decisión, sin inventar header. **Hecho**: dos POST simultáneos con misma clave producen un pedido/reserva y respuesta según HTTP aprobado. | BLOCKED |
| DEV-API023-05 · QA_TECNICA fallos y seguridad | RF-ORD-27/32, ORD-02/04/05; `tests/UtttGo.IntegrationTests/CreateOrderEndpointTests.cs`, `tests/UtttGo.SecurityTests/CreateOrderSecurityTests.cs` | Tras DEV-API023-02/03/04 y permisos locales: probar JWT revocado/cuenta no activa, body precio/sellerId, Redis/Mongo/Catalog caídos, carrera y compensación. **Hecho**: evidencia fechada de build/formato/tres suites Release verdes, sin producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); confirmar acta D-ORD/Catalog antes de `src`, ejecutar pruebas solo aisladas; no afirmar atomicidad entre módulos sin protocolo de compensación.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API023-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: TASK-ORD-001..004, D-ORD-01/02/04/06 y contrato Catalog | - | PENDIENTE |
| DEV-API023-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: DEV-API023-01 y aprobación fase 04 | DEV-API023-01 | PENDIENTE |
| DEV-API023-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: D-ORD-02, comandos Catalog y permisos Mongo local | DEV-API023-01, DEV-API023-02 | PENDIENTE |
| DEV-API023-04 | IMPLEMENTACION | BLOCKED | SI | Pendiente: D-ORD-04 y política retry/reserva | DEV-API023-01, DEV-API023-03 | PENDIENTE |
| DEV-API023-05 | QA_TECNICA | BLOCKED | SI | Pendiente: fase 04/Catalog aprobado y entorno aislado autorizado | DEV-API023-02, DEV-API023-03, DEV-API023-04 | PENDIENTE |
