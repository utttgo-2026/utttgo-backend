# API-038 — Envío de mensaje del pedido · task

Estado de aprobación de la ficha: APROBADA explícitamente para API-038

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → [plan](plan.md) → tareas. Solo envío de participantes del pedido en curso; la lectura pertenece a [API-037](../api-037-order-messages-list/spec.md) y la purga a DEC-005. Los IDs DEV no renumeran TASK históricos.

## Tareas ejecutables sin cambiar contrato

La implementación REST está READY para verificación CI; la purga y notificaciones quedan fuera de esta entrega.

## Tareas bloqueadas hasta decisión y gates SDD

| ID · tipo y objetivo | Requisito/escenario; archivos permitidos | Dependencias; acción exacta; resultado esperado; prueba/comando y evidencia | Estado |
|---|---|---|---|
| DEV-API038-01 · DOCUMENTAL ratificar contrato de envío | API-038, MSGS-01/02/03/05; `spec.md`, `plan.md`, `task.md` | Contrato ratificado: `{content}`, 1..1000, `Idempotency-Key`, `404` tercero y `409` cierre. | DONE |
| DEV-API038-02 · IMPLEMENTACION servicio y repositorio | API-038, MSGS-01/05; `Modules/Chat/Infrastructure/MongoOrderMessageRepository.cs`, `Modules/Chat/Application/ChatService.cs` | Persistencia con clave de idempotencia e interfaz de Orders. | DONE |
| DEV-API038-03 · IMPLEMENTACION endpoint protegido | API-038, MSGS-01/02/03; `Modules/Chat/Controllers/OrderMessagesController.cs`, `src/UTTTGO.Api/Program.cs` | POST con participación verificada, sanitización y rechazo de pedido cerrado. | DONE |
| DEV-API038-04 · QA_TECNICA escenarios, tasa y fallos | API-038, MSGS-01..05; suites Unit/Integration/Security | Build Release, format y suites Unit/Integration/Security ejecutadas con Docker activo: 407/308/172 pruebas superadas. | DONE |

## Guía de ejecución con OpenCode

Leer [constitución](../../../docs/constitution.md), [spec](spec.md), [plan](plan.md), tareas y [guía](../../../docs/sdd-guia-api-atomica.md); comprobar Git, elegir solo tarea cuyas dependencias y autorización estén cumplidas; no inferir aprobación por código o dobles.

### Control de ejecución

| ID | Tipo | Estado | Cambio técnico | Autorización | Dependencias | Evidencia de cierre |
|---|---|---|---|---|---|---|
| DEV-API038-01 | DOCUMENTAL | DONE | NO | Contrato ratificado | - | Esta ficha/plan/task |
| DEV-API038-02 | IMPLEMENTACION | DONE | SI | Contrato Orders publicado | DEV-API038-01 | Código y tests añadidos |
| DEV-API038-03 | IMPLEMENTACION | DONE | SI | Participación, sanitización y cierre | DEV-API038-02 | Endpoint y DI añadidos |
| DEV-API038-04 | QA_TECNICA | DONE | SI | Docker activo | DEV-API038-02/03 | 409 unit, 308 integration, 172 security; build/format verdes |
