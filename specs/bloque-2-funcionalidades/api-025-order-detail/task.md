# API-025 — Consultar detalle de pedido · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Solo participante lee; borrado compartido es API-005, no esta ficha.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: fase 04/DTO por actor pendiente y Orders aún sin código.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado y prueba/evidencia | Estado |
|---|---|---|---|
| DEV-API025-01 · DOCUMENTAL ratificar detalle | RF-ORD-09/15/31, DETAIL-01/02/03/04; solo `spec.md`, `plan.md`, `task.md` tras acta | TASK-ORD-001..004: producto/seguridad/Orders deciden DTO por participante, ID, 404 ante tercero, price snapshot, HTTP y cotejo Excel; DEC-003 sigue fuera. **Hecho**: acta identificable y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-025-order-detail` 0 errores. | BLOCKED |
| DEV-API025-02 · IMPLEMENTACION GET con guard | RF-ORD-09/15, DETAIL-01/03; `Modules/Orders/Controllers/OrdersController.cs`, `Contracts/OrderDetailResponse.cs`, `Validators/OrderIdValidator.cs`, `Application/GetOrderDetailService.cs`, `Infrastructure/MongoOrderRepository.cs`, DI `src/UTTTGO.Api/Program.cs` | Tras DEV-API025-01 y API-023: resolver sujeto de Identity y vendedor de Sellers por interfaz, filtrar participante antes de proyectar, ausente/tercero mismo error según acta. **Hecho**: `GetOrderDetailServiceTests.cs` y `GetOrderDetailEndpointTests.cs` cubren comprador y tercero, build Release verde. | BLOCKED |
| DEV-API025-03 · IMPLEMENTACION proyección vendedor segura | RF-ORD-09/31, DETAIL-02; `Modules/Orders/Contracts/OrderDetailResponse.cs`, `Application/GetOrderDetailService.cs`, `tests/UtttGo.IntegrationTests/GetOrderDetailEndpointTests.cs` | Tras DEV-API025-01/02 y contrato Seller: exponer solo campos aprobados al vendedor, sin `deliveryTokenHash`/reserva/PII de otro. **Hecho**: pruebas locales propietario vendedor y ajeno comparan DTO permitido y denegación. | BLOCKED |
| DEV-API025-04 · QA_TECNICA privacidad/fallos | RF-ORD-27/32, DETAIL-03/04; `tests/UtttGo.IntegrationTests/GetOrderDetailEndpointTests.cs`, `tests/UtttGo.SecurityTests/GetOrderDetailSecurityTests.cs` | Tras DEV-API025-02/03 y permiso Mongo/Redis local: verificar JWT revocado, Redis/Mongo caídos, rate limit y no participante indistinguible. **Hecho**: evidencia fechada de build/formato/tres suites Release verdes, sin datos personales en logs ni producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md); esperar fase 04 y modelo Orders, no usar documentos Mongo directos.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API025-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: TASK-ORD-001..004 y DTO/error por actor | - | PENDIENTE |
| DEV-API025-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: modelo API-023, fase 04 aprobada | DEV-API025-01 | PENDIENTE |
| DEV-API025-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: campos vendedor y contrato Sellers aprobados | DEV-API025-01, DEV-API025-02 | PENDIENTE |
| DEV-API025-04 | QA_TECNICA | BLOCKED | SI | Pendiente: permiso Mongo/Redis local y fase 04 | DEV-API025-02, DEV-API025-03 | PENDIENTE |
