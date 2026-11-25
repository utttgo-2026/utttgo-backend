# API-037 — Historial de mensajes del pedido · task

Estado de aprobación de la ficha: APROBADA explícitamente para API-037

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Solo lectura del historial; el envío pertenece a [API-038](../api-038-send-order-message/spec.md) y la purga obedece a DEC-005. Los IDs DEV no renumeran TASK históricos.

## Tareas ejecutables sin cambiar contrato

La implementación REST está READY para verificación CI; la purga posterior y DEC-003 quedan fuera de esta entrega.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado esperado; prueba/comando y evidencia | Estado |
|---|---|---|---|
| DEV-API037-01 · DOCUMENTAL ratificar contrato de lectura | API-037, MSGL-01..03/05; `spec.md`, `plan.md`, `task.md` | Contrato ratificado: paginación, DTO, `404` ante tercero y almacenamiento Mongo; purga posterior separada. | DONE |
| DEV-API037-02 · IMPLEMENTACION modelo y repositorio | API-037, MSGL-01/03/05; `Modules/Chat/Infrastructure/MongoOrderMessageRepository.cs`, `Modules/Chat/Application/ChatService.cs` | Almacén `orderMessages`, índices de orden e idempotencia y contrato de Orders publicados. | DONE |
| DEV-API037-03 · IMPLEMENTACION endpoint protegido | API-037, MSGL-01/02/04; `Modules/Chat/Controllers/OrderMessagesController.cs`, `src/UTTTGO.Api/Program.cs` | GET con participación verificada y respuesta paginada sin campos ajenos. | DONE |
| DEV-API037-04 · QA_TECNICA escenarios y fallos | API-037, MSGL-01..05; suites Unit/Integration/Security | Build Release, format y suites Unit/Integration/Security ejecutadas con Docker activo: 407/308/172 pruebas superadas. | DONE |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), tareas y [guía](../../../docs/sdd-guia-api-atomica.md); comprobar Git, elegir solo tarea cuyas dependencias y autorización estén cumplidas; no inferir aprobación por código o dobles.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API037-01 | DOCUMENTAL | DONE | NO | Contrato ratificado | - | Esta ficha/plan/task |
| DEV-API037-02 | IMPLEMENTACION | DONE | SI | Contrato Orders publicado | DEV-API037-01 | Código y tests añadidos |
| DEV-API037-03 | IMPLEMENTACION | DONE | SI | Participación por Orders | DEV-API037-02 | Endpoint y DI añadidos |
| DEV-API037-04 | QA_TECNICA | DONE | SI | Docker activo | DEV-API037-02/03 | 409 unit, 308 integration, 172 security; build/format verdes |
