# API-038 — Envío de mensaje del pedido · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Solo envío de participantes del pedido en curso; la lectura pertenece a [API-037](../api-037-order-messages-list/spec.md) y la purga a DEC-005. Los IDs DEV no renumeran TASK históricos.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: DEC-005 técnica, gate de fase 07 y contratos de participación/estado de Orders siguen pendientes.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado esperado; prueba/comando y evidencia | Estado |
|---|---|---|---|
| DEV-API038-01 · DOCUMENTAL ratificar contrato de envío | API-038, MSGS-01/02/03/05; `spec.md`, `plan.md`, `task.md` | Tras DEC-005 y gate de fase 07: fijar DTO, códigos ante cierre/fallo, límites, idempotencia y purga sin contradecir BR-012; cotejar fuente autorizada. **Hecho**: acta identificable y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-038-send-order-message` sin errores. | BLOCKED |
| DEV-API038-02 · IMPLEMENTACION servicio y repositorio | API-038, MSGS-01/05; `Modules/Chat/Infrastructure/MongoOrderMessageRepository.cs`, `Modules/Chat/Application/OrderMessagesService.cs` | Depende de DEV-API038-01 y contratos de Orders: persistencia con clave de idempotencia y estado de pedido en curso. **Hecho**: `OrderMessagesServiceTests.cs` cubre validación, idempotencia y rechazo tras cierre con build Release verde. | BLOCKED |
| DEV-API038-03 · IMPLEMENTACION endpoint protegido | API-038, MSGS-01/02/03; `Modules/Chat/Controllers/OrderMessagesController.cs`, `src/UTTTGO.Api/Program.cs` | Depende de DEV-API038-02: POST con participación verificada y respuesta sin campos ajenos, DI sin tocar middleware. **Hecho**: `OrderMessagesEndpointTests.cs` prueba `201`, tercero sin escritura y pedido cerrado sin mensaje. | BLOCKED |
| DEV-API038-04 · QA_TECNICA escenarios, tasa y fallos | API-038, MSGS-01..05; `tests/UtttGo.UnitTests/OrderMessagesServiceTests.cs`, `tests/UtttGo.IntegrationTests/OrderMessagesEndpointTests.cs`, `tests/UtttGo.SecurityTests/OrderMessagesSecurityTests.cs` | Depende de DEV-API038-02/03: JWT revocado, rate limit por participante, Redis/Mongo caídos y reintento duplicado; DEC-004 solo si aprobada. **Hecho**: evidencia fechada de build, formato y tres suites en verde sin usar producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), tareas y [guía](../../../docs/sdd-guia-api-atomica.md); comprobar Git, elegir solo tarea cuyas dependencias y autorización estén cumplidas; no inferir aprobación por código o dobles.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API038-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: DEC-005 técnica y gate de fase 07 | - | PENDIENTE |
| DEV-API038-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: acta DEC-005 y contratos de Orders | DEV-API038-01 | PENDIENTE |
| DEV-API038-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: contrato de participación y estado de Orders | DEV-API038-02 | PENDIENTE |
| DEV-API038-04 | QA_TECNICA | BLOCKED | SI | Pendiente: entorno aislado y decisión DEC-004 | DEV-API038-02/03 | PENDIENTE |
