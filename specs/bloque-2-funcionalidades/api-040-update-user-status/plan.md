# API-040 — Cambiar estado de cuenta de usuario · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Alcance: `PATCH /api/v1/admin/users/{userId}/status` para suspender/reactivar una cuenta; actor Admin local activo, sin auto-operación, cambio de rol, borrado ni listado. El contrato es candidato y no autoriza código.

## Arquitectura existente

`src/UTTTGO.Api/Modules/Admin/` solo implementa el lector de API-039; no existe mutación API-040 ni contrato de escritura administrativa. Identity es dueño de `users` y sesiones; Admin no accede a sus colecciones. Redis ya es la frontera de blacklist JWT y rate limit. El login debe consultar el estado local antes de emitir sesión; el diseño concreto de esa integración permanece pendiente.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| API | `Modules/Admin/Controllers/AdminUsersStatusController.cs` (previsto) | PATCH y mapeo al contrato único de error. |
| DTO/validador | `AdminUserStatusRequest` y `AdminUserStatusRequestValidator` (previstos) | Cuerpo cerrado y `Active/Suspended`. |
| Aplicación | `Modules/Admin/Application/AdminUserStatusService.cs` (previsto) | Autorizar actor, validar objetivo y orquestar resultado. |
| Identity | `IAdminUserStatusWriter` + contrato de revocación (previstos) | Escribir estado y revocar sesiones detrás del dueño de `users`. |
| Redis | Blacklist `revoked:{jti}` y rate limit compartido | TTL hasta `exp`; fallos de seguridad fail-closed. |
| Auditoría | Componente/colección del dueño por determinar | Actor, objetivo, transición, UTC y `correlationId`; sin éxito si falla. |

## Secuencia técnica

Controller → validar JSON cerrado → validar JWT/blacklist → cargar perfil local del actor y exigir `Admin` + `Active` → rechazar cuenta propia → rate limit → consultar objetivo y estado mediante contrato de Identity → validar destino permitido y mismo estado → coordinar escritura y revocación de sesiones al suspender → persistir auditoría → responder `200`. Redis, Identity, MongoDB o auditoría caídos producen `503`; rate limit excedido produce `429`; no se publica cambio parcial. Reactivar habilita login nuevo, no tokens ya revocados. Orden atómico/transaccional exacto: pendiente.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos/errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md) y [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): reutilizar fronteras, contrato único, blacklist y rate limit sin duplicar reglas ni cambiar middleware de `Program.cs`.

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| ADMS-01/04 | `Modules/Admin/Controllers/AdminUsersStatusController.cs`, DTO, validador y `AdminUserStatusService.cs` | Solo tras ratificar DTO, códigos y mismo estado. |
| ADMS-01/06/07 | `Modules/Identity/Application/IAdminUserStatusWriter.cs` y contrato de revocación/login | Solo tras definir frontera de Identity, sesiones y login suspendido. |
| ADMS-02/03/05 | Servicio Admin y gate de rate limit | Solo tras ratificar guardas, idempotencia y política de tasa. |
| ADMS-01/06 | Auditoría del dueño correspondiente | Solo tras definir dueño, consistencia, retención e idempotencia. |
| ADMS-01..07 | `tests/UtttGo.UnitTests/AdminUserStatusTests.cs`, `tests/UtttGo.IntegrationTests/AdminUserStatusEndpointTests.cs`, `tests/UtttGo.SecurityTests/AdminUserStatusSecurityTests.cs` | Pruebas nuevas después del contrato; dobles no prueban servicios externos reales. |

## Orden de implementación

DEV-API040-01 ratifica DTO, estados, códigos, sesiones, login y consistencia; DEV-API040-02 publica Identity y endpoint; DEV-API040-03 implementa guardas, revocación y auditoría; DEV-API040-04 prueba ADMS-01..07; DEV-API040-05 valida AC-005/AC-009 en servicios no productivos. Ninguna tarea de código está READY.

## Dependencias y bloqueos

| Gate transversal o decisión particular (ID original) | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| Ratificación HTTP/DTO, mismo estado y códigos | DEV-API040-01/02/04 | Acta identificable y contrato actualizado. |
| Contrato Identity para estado, sesiones y login | DEV-API040-01/02/03/04/05 | Interfaz publicada por Identity sin colecciones cruzadas. |
| Consistencia estado/Redis/auditoría | DEV-API040-01/03/04 | Diseño aprobado sin `200` parcial. |
| Auditoría y rate limit | DEV-API040-03/04 | Dueño, política y pruebas definidas. |
| AC-005/AC-009 y T-AUTH-13 | DEV-API040-04/05 | Tests específicos y evidencia fechada no productiva. |
| DEC-003/API-005 | DEV-API040-01 | Confirmación de que API-040 no elimina cuentas. |

## Estrategia de pruebas

Unitarias del validador, estados, actor Admin activo, cuenta propia, revocación y auditoría. Integración con `WebApplicationFactory` y Mongo/Redis aislados para transición, `409`, `503` sin parcialidad, login suspendido y reactivación. Seguridad para JWT inválido/revocado, rol forjado, Admin inactivo, objetivo inexistente y rate limit. Dobles o contenedores no productivos no acreditan servicios reales ni producción. No ejecutar hasta autorización; el cierre deberá identificar tests, fecha y límites.

## Seguridad, errores y observabilidad

JWT/blacklist y rate limit distribuido fail-closed; roles desde perfil local; objetivo propio prohibido; whitelist estricta; auditoría con actor/objetivo/transición/UTC/`correlationId`, sin tokens ni PII innecesaria. Contrato único constitucional para `400/401/403/404/409/429/503`; no agregar HTTP. Logs estructurados sin secretos. No cambiar middleware.

## Criterios de cierre técnico

ADMS-01..07 cubiertos con tests concretos; estado suspendido bloquea login/operaciones, sesiones son revocadas, reactivación exige sesión nueva, auditoría y fallos son fail-closed, y no hay escritura cruzada. Solo después: build Release, format y suites pertinentes verdes, evidencia fechada del entorno aislado y aprobación documental separada del cierre técnico.
