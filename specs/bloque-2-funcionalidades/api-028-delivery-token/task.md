# API-028 — Emitir token de entrega · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. No marcar Delivered ni guardar secreto en claro.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: fase 04 y política de tokens/concurrencia pendientes.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción posterior; resultado esperado; prueba/comando y evidencia de finalización | Estado |
|---|---|---|---|
| DEV-API028-01 · DOCUMENTAL decidir token | RF-ORD-23/24, TOK-01..04; `spec.md`, `plan.md`, `task.md` tras acta | TASK-ORD-001..004, D-ORD-03/06/10: ratificar entropía, hash, TTL, replay/reemplazo, fallo auditoría y HTTP; cotejar Excel. **Hecho**: acta identificable y validador individual 0 errores. | BLOCKED |
| DEV-API028-02 · IMPLEMENTACION emisión protegida | RF-ORD-23/24, TOK-01/03; `Modules/Orders/Controllers/OrdersController.cs`, `Contracts/DeliveryTokenResponse.cs`, `Application/IssueDeliveryTokenService.cs`, `src/UTTTGO.Api/Program.cs` | Tras DEV-API028-01 y modelo API-023/027: validar buyer/Ready, generar secreto seguro sin registrar, configurar DI. **Hecho**: `IssueDeliveryTokenServiceTests.cs` prueba token/hash/estado; build Release verde. | BLOCKED |
| DEV-API028-03 · IMPLEMENTACION reemplazo atómico | RF-ORD-24/32, TOK-01/02/04; `Modules/Orders/Infrastructure/MongoOrderRepository.cs`, `MongoOrderAuditRepository.cs`, `Application/IssueDeliveryTokenService.cs` | Tras DEV-API028-01/02 y D-ORD-06: guardar solo hash/expiry con filtro Ready/version, invalidar anterior y auditar sin secreto; resolver falla parcial según acta. **Hecho**: `DeliveryTokenEndpointTests.cs` verifica reemplazo, carrera API-029 y ausencia de texto plano en Mongo. | BLOCKED |
| DEV-API028-04 · QA_TECNICA privacidad/fallos | T-ORD-12/13, TOK-01..04; `tests/UtttGo.UnitTests/IssueDeliveryTokenServiceTests.cs`, `tests/UtttGo.IntegrationTests/DeliveryTokenEndpointTests.cs`, `tests/UtttGo.SecurityTests/DeliveryTokenSecurityTests.cs` | Tras DEV-API028-02/03: cubrir tercero/vendedor/revocado, rate limit Redis caído, auditoría fallida, errores/logs sanitizados. Hecho: evidencia fechada build/formato/tres suites Release verdes sin producción ni secretos. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), [guía](../../../docs/sdd-guia-api-atomica.md); comprobar acta, no publicar token en evidencia.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API028-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: TASK-ORD-001..004, D-ORD-03/06/10 | - | PENDIENTE |
| DEV-API028-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: API-023/027 y acta token | DEV-API028-01 | PENDIENTE |
| DEV-API028-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: CAS y semántica reemplazo/confirmación | DEV-API028-02 | PENDIENTE |
| DEV-API028-04 | QA_TECNICA | BLOCKED | SI | Pendiente: permiso entorno aislado y fase 04 | DEV-API028-02/03 | PENDIENTE |
