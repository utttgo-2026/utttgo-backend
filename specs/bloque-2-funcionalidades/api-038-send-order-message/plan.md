# API-038 — Envío de mensaje del pedido · plan

Estado de aprobación de la ficha: APROBADA explícitamente para API-038

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Solo enviar mensajes de participantes del pedido en curso; leer historial es [API-037](../api-037-order-messages-list/spec.md), estados del pedido pertenecen a Orders y la purga obedece a DEC-005.

## Arquitectura existente

Chat consume el contrato publicado por Orders para participación y estado, sin leer directamente su colección. SignalR/FCM y la purga quedan fuera de esta entrega.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Chat/Controllers/OrderMessagesController.cs` | POST con `{orderId}` y `{content}`. |
| Aplicación | `src/UTTTGO.Api/Modules/Chat/Application/ChatService.cs` | Validación, participación, persistencia e idempotencia. |
| Datos | `src/UTTTGO.Api/Modules/Chat/Infrastructure/MongoOrderMessageRepository.cs` | Escritura atómica e índices únicos por idempotencia. |
| Contrato | `IOrderChatParticipantReader` de Orders | Participante y estado; sin colección cruzada. |
| Tiempo real | SignalR + backplane Redis (si aplica) | Entrega efímera; histórico sigue en MongoDB. |

## Secuencia técnica

JWT/blacklist → rate limit Redis (por participante) → validar `{orderId}` → verificar participación y estado vía Orders → validar/sanitizar `content` → persistir con `Idempotency-Key` → responder `201`. Redis/Mongo caídos ⇒ `503` fail-closed sin mensaje fantasma.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos/errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): aplicar solo los pertinentes, sin duplicar reglas.

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| MSGS-01/02/03 | `Modules/Chat/Controllers/OrderMessagesController.cs`, `Modules/Chat/Application/ChatService.cs`, `src/UTTTGO.Api/Program.cs` | Implementado; middleware intacto. |
| MSGS-01/05 | `Modules/Chat/Infrastructure/MongoOrderMessageRepository.cs` | Escritura e índice único de idempotencia implementados; purga posterior. |
| MSGS-01..05 | `tests/UtttGo.UnitTests/ChatServiceTests.cs`, `tests/UtttGo.IntegrationTests/ChatEndpointTests.cs`, `tests/UtttGo.SecurityTests/ChatSecurityTests.cs` | Suites Release ejecutadas con Docker activo; pruebas específicas cubren sanitización, idempotencia, cierre, perfil inactivo, autenticación y ownership nulo. |

## Orden de implementación

DEV-API038-01 ratifica DTO, códigos, límites e idempotencia; DEV-API038-02/03 implementan repositorio, servicio, endpoint y autorización; DEV-API038-04 cubre escenarios, tasa y fallos. La purga queda separada.

## Dependencias y bloqueos

| Gate transversal o decisión particular (ID original) | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| DEC-005 (almacenamiento, idempotencia, purga) | Purga posterior | Acta y job UTC; no bloquea REST de esta entrega. |
| Gate de fase 07 (spec → plan → task) | DEV-API038-01..04 | Ratificación de esta ficha, plan y task. |
| Contrato de participación y estado de Orders | DEV-API038-02/03 | `IOrderChatParticipantReader` publicado por Orders. |
| DEC-004 (notificación de mensajes) | DEV-API038-01/04 | Decisión explícita; FCM solo backend si aplica. |

## Estrategia de pruebas

Unitarias de validación, participación, idempotencia y máquina de estados; integración `WebApplicationFactory` con Mongo/Redis locales (Testcontainers) para envío, tercero, cierre y reintento; seguridad con JWT revocado, rate limit agotado y Redis/Mongo caídos; entrega en dos réplicas. Dobles no equivalen a integración externa; sin producción. Comandos: `dotnet test tests/UtttGo.UnitTests/UtttGo.UnitTests.csproj -c Release --no-build` y las suites de integración/seguridad con evidencia fechada.

## Seguridad, errores y observabilidad

JWT/blacklist y rate limit distribuido fail-closed, autorización por participación mediante contrato, validación/sanitización de contenido sin loguearlo, error único `correlationId`, logs con resultado sin payloads sensibles; sin alterar el orden del middleware de `Program.cs`.

## Criterios de cierre técnico

MSGS-01..05 con pruebas de envío, tercero, cierre, tasa/fallos e idempotencia; build Release, `dotnet format` y suites pertinentes en verde. Purga y notificaciones quedan como trabajo posterior.
