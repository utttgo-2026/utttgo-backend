# API-038 — Envío de mensaje del pedido · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Solo enviar mensajes de participantes del pedido en curso; leer historial es [API-037](../api-037-order-messages-list/spec.md), estados del pedido pertenecen a Orders y la purga obedece a DEC-005.

## Arquitectura existente

No hay módulo Chat ni Orders en `src/UTTTGO.Api/Modules/` (existen Admin, Identity y Sellers). Las fichas de Orders ([API-025](../api-025-order-detail/spec.md) y vecinas) son borrador sin código; la participación y el estado del pedido llegan por contrato del dueño de Orders, sin lectura directa de su colección. SignalR/FCM solo si la notificación de mensajes se aprueba (BR-008, DEC-004).

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Chat/Controllers/OrderMessagesController.cs` (previsto) | POST con `{orderId}` y cuerpo de mensaje. |
| Aplicación | `src/UTTTGO.Api/Modules/Chat/Application/OrderMessagesService.cs` (previsto) | Validación, participación, persistencia e idempotencia. |
| Datos | `src/UTTTGO.Api/Modules/Chat/Infrastructure/MongoOrderMessageRepository.cs` (previsto) | Escritura atómica; colección e índices por definir. |
| Contrato | `IOrderParticipation` y `IOrderStateReader` del dueño de Orders (previstos) | Participante y estado; sin colección cruzada. |
| Tiempo real | SignalR + backplane Redis (si aplica) | Entrega efímera; histórico sigue en MongoDB. |

## Secuencia técnica

JWT/blacklist → rate limit Redis (por participante) → validar `{orderId}` → verificar participación y estado vía contratos de Orders → validar y sanitizar contenido → persistir con clave de idempotencia → responder `201`; publicar en SignalR/FCM solo si aprobado. Candidato hasta acta: DTO, códigos ante cierre/fallo, límites y reintento. Redis/Mongo caídos ⇒ `503` fail-closed sin mensaje fantasma.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos/errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): aplicar solo los pertinentes, sin duplicar reglas.

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| MSGS-01/02/03 | `Modules/Chat/Controllers/OrderMessagesController.cs`, `Modules/Chat/Application/OrderMessagesService.cs`, `src/UTTTGO.Api/Program.cs` | Tras acta DEC-005 y gate de fase 07; middleware intacto. |
| MSGS-01/05 | `Modules/Chat/Infrastructure/MongoOrderMessageRepository.cs` | Escritura, índice único de idempotencia y purga según acta. |
| MSGS-01..05 | `tests/UtttGo.UnitTests/OrderMessagesServiceTests.cs`, `tests/UtttGo.IntegrationTests/OrderMessagesEndpointTests.cs`, `tests/UtttGo.SecurityTests/OrderMessagesSecurityTests.cs` | Pruebas locales nuevas; dobles solo en tests. |

## Orden de implementación

DEV-API038-01 ratifica DTO, códigos, límites, idempotencia y purga; DEV-API038-02 crea repositorio y servicio tras 01 y los contratos de Orders; DEV-API038-03 expone el endpoint con autorización tras 02; DEV-API038-04 cubre escenarios, tasa y fallos tras 02/03. No hay código previo de Chat que reutilizar.

## Dependencias y bloqueos

| Gate transversal o decisión particular (ID original) | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| DEC-005 (almacenamiento, idempotencia, purga) | DEV-API038-01..04 | Acta de diseño que no contradice BR-012. |
| Gate de fase 07 (spec → plan → task) | DEV-API038-01..04 | Aprobación integral de la fase 07. |
| Contrato de participación y estado de Orders (API-025) | DEV-API038-02/03 | Interfaz publicada por el dueño de Orders. |
| DEC-004 (notificación de mensajes) | DEV-API038-01/04 | Decisión explícita; FCM solo backend si aplica. |

## Estrategia de pruebas

Unitarias de validación, participación, idempotencia y máquina de estados; integración `WebApplicationFactory` con Mongo/Redis locales (Testcontainers) para envío, tercero, cierre y reintento; seguridad con JWT revocado, rate limit agotado y Redis/Mongo caídos; entrega en dos réplicas. Dobles no equivalen a integración externa; sin producción. Comandos: `dotnet test tests/UtttGo.UnitTests/UtttGo.UnitTests.csproj -c Release --no-build` y las suites de integración/seguridad con evidencia fechada.

## Seguridad, errores y observabilidad

JWT/blacklist y rate limit distribuido fail-closed, autorización por participación mediante contrato, validación/sanitización de contenido sin loguearlo, error único `correlationId`, logs con resultado sin payloads sensibles; sin alterar el orden del middleware de `Program.cs`.

## Criterios de cierre técnico

MSGS-01..05 con pruebas verdes de envío, tercero, cierre de pedido, tasa/fallos e idempotencia; build Release, `dotnet format` y suites pertinentes en verde con evidencia fechada; aprobación documental y gate de fase 07 por separado.
