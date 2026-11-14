# API-037 — Historial de mensajes del pedido · task

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Solo lectura del historial; el envío pertenece a [API-038](../api-038-send-order-message/spec.md) y la purga obedece a DEC-005. Los IDs DEV no renumeran TASK históricos.

## Tareas ejecutables sin cambiar contrato

Ninguna técnica READY: DEC-005 técnica, gate de fase 07 y contrato de participación de Orders siguen pendientes.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado esperado; prueba/comando y evidencia | Estado |
|---|---|---|---|
| DEV-API037-01 · DOCUMENTAL ratificar contrato de lectura | API-037, MSGL-01..03/05; `spec.md`, `plan.md`, `task.md` | Tras DEC-005 y gate de fase 07: fijar paginación, DTO, código ante tercero y ventana de purga sin contradecir BR-012; cotejar fuente autorizada. **Hecho**: acta identificable y `python scripts/validate-sdd-atomic.py specs/bloque-2-funcionalidades/api-037-order-messages-list` sin errores. | BLOCKED |
| DEV-API037-02 · IMPLEMENTACION modelo y repositorio | API-037, MSGL-01/03/05; `Modules/Chat/Infrastructure/MongoOrderMessageRepository.cs`, `Modules/Chat/Application/OrderMessagesService.cs` | Depende de DEV-API037-01 y del contrato de Orders: crear almacén duradero multi-instancia conforme al acta. **Hecho**: `OrderMessagesServiceTests.cs` cubre lectura ordenada y ausencia tras purga con build Release verde. | BLOCKED |
| DEV-API037-03 · IMPLEMENTACION endpoint protegido | API-037, MSGL-01/02/04; `Modules/Chat/Controllers/OrderMessagesController.cs`, `src/UTTTGO.Api/Program.cs` | Depende de DEV-API037-02: GET con participación verificada mediante contrato y respuesta sin campos ajenos, DI sin tocar middleware. **Hecho**: `OrderMessagesEndpointTests.cs` prueba participante con `200` y tercero sin contenido. | BLOCKED |
| DEV-API037-04 · QA_TECNICA escenarios y fallos | API-037, MSGL-01..05; `tests/UtttGo.UnitTests/OrderMessagesServiceTests.cs`, `tests/UtttGo.IntegrationTests/OrderMessagesEndpointTests.cs`, `tests/UtttGo.SecurityTests/OrderMessagesSecurityTests.cs` | Depende de DEV-API037-02/03: JWT revocado, Redis/Mongo caídos, purga y DEC-003 solo si aprobada. **Hecho**: evidencia fechada de build, formato y tres suites en verde sin usar producción. | BLOCKED |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), tareas y [guía](../../../docs/sdd-guia-api-atomica.md); comprobar Git, elegir solo tarea cuyas dependencias y autorización estén cumplidas; no inferir aprobación por código o dobles.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API037-01 | DOCUMENTAL | BLOCKED | NO | Pendiente: DEC-005 técnica y gate de fase 07 | - | PENDIENTE |
| DEV-API037-02 | IMPLEMENTACION | BLOCKED | SI | Pendiente: acta DEC-005 y contrato de Orders | DEV-API037-01 | PENDIENTE |
| DEV-API037-03 | IMPLEMENTACION | BLOCKED | SI | Pendiente: contrato de participación de Orders | DEV-API037-02 | PENDIENTE |
| DEV-API037-04 | QA_TECNICA | BLOCKED | SI | Pendiente: entorno aislado y política DEC-003 | DEV-API037-02/03 | PENDIENTE |
