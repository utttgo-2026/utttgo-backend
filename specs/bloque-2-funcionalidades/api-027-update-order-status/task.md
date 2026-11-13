# API-027 — Cambiar estado de pedido · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. No habilitar Delivered ni cancelación por este PATCH.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: fase 04 y D-ORD-02/06 siguen pendientes.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API027-01 · DOCUMENTAL ratificar efectos | §13, ST-01..05; `spec.md`, `plan.md`, `task.md` solo tras acta | TASK-ORD-001..004, D-ORD-02/06/08/10: decidir versión, HTTP, actor, liberación/recovery, notificación y cotejar Excel. **Hecho**: acta expresa referenciada y validador individual 0 errores. | BLOCKED |
| DEV-API027-02 · IMPLEMENTACION transición autorizada | RF-ORD-21/22, ST-01..04; `Modules/Orders/Controllers/OrdersController.cs`, `Contracts/UpdateOrderStatusRequest.cs`, `Validators/UpdateOrderStatusRequestValidator.cs`, `Application/UpdateOrderStatusService.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API027-01 y API-023: validar Seller propietario/estado, denegar Delivered/Cancelled, registrar DI sin tocar middleware. **Hecho**: `UpdateOrderStatusServiceTests.cs` cubre aristas y `dotnet build UTTTGO.Backend.slnx -c Release --no-restore` verde. | BLOCKED |
| DEV-API027-03 · IMPLEMENTACION CAS y efectos | RF-ORD-26/29/32, ST-01/02/05; `Modules/Orders/Infrastructure/MongoOrderRepository.cs`, `MongoOrderAuditRepository.cs`, `Application/UpdateOrderStatusService.cs` | Tras DEV-API027-02, Catalog/Notifications y D-ORD-02/06: CAS, auditoría, liberar Rejected exactamente una vez y notificar vía interfaz, recuperación fallo parcial. **Hecho**: `UpdateOrderStatusEndpointTests.cs` verifica carrera y fallo Catalog sin doble liberación. | BLOCKED |
| DEV-API027-04 · QA_TECNICA seguridad/concurrencia | T-ORD-07/09/10/11/20, ST-01..05; `tests/UtttGo.UnitTests/UpdateOrderStatusServiceTests.cs`, `tests/UtttGo.IntegrationTests/UpdateOrderStatusEndpointTests.cs`, `tests/UtttGo.SecurityTests/UpdateOrderStatusSecurityTests.cs` | Tras DEV-API027-02/03 y entorno local: probar vendedor ajeno, JWT revocado, Redis caído, rechazo, version racing y notificación mock. Hecho: evidencia fechada build/formato/tres suites Release verdes, sin producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md) y [guía](../../../docs/sdd-guia-api-atomica.md); comprobar acta y código existente antes de modificarlo.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API027-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: TASK-ORD-001..004 y D-ORD-02/06/08/10 | - | PENDIENTE |
| DEV-API027-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: acta fase 04 y API-023 | DEV-API027-01 | PENDIENTE |
| DEV-API027-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: protocolo Catalog y notificaciones | DEV-API027-02 | PENDIENTE |
| DEV-API027-04 | QA_TECNICA | BLOCKED | SI | Pendiente: entorno aislado autorizado | DEV-API027-02/03 | PENDIENTE |
