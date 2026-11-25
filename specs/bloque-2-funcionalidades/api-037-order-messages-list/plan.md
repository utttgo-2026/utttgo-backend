# API-037 — Historial de mensajes del pedido · plan

Estado de aprobación de la ficha: APROBADA explícitamente para API-037

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Solo lectura del historial de un pedido; enviar mensajes es [API-038](../api-038-send-order-message/spec.md), la purga obedece a DEC-005 y no se accede a colecciones de otros módulos.

## Arquitectura existente

Chat consume el contrato publicado por Orders para participación y estado; no lee directamente la colección `orders`.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Chat/Controllers/OrderMessagesController.cs` | GET con `{orderId}` y paginación. |
| Aplicación | `src/UTTTGO.Api/Modules/Chat/Application/ChatService.cs` | Participación, lectura y DTO seguro. |
| Datos | `src/UTTTGO.Api/Modules/Chat/Infrastructure/MongoOrderMessageRepository.cs` | Colección `orderMessages` e índices de orden/idempotencia. |
| Contrato | `IOrderChatParticipantReader` de Orders | Comprador/vendedor/estado; sin colección cruzada. |

## Secuencia técnica

JWT/blacklist → rate limit Redis → validar `orderId` → consultar participación mediante contrato de Orders → leer mensajes paginados en orden estable → responder sin campos ajenos. Mongo/Redis caídos ⇒ `503` fail-closed; nunca mensajes de otro pedido.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos/errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): aplicar solo los pertinentes, sin duplicar reglas.

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| MSGL-01/02 | `Modules/Chat/Controllers/OrderMessagesController.cs`, `Modules/Chat/Application/ChatService.cs`, `src/UTTTGO.Api/Program.cs` | Implementado; middleware intacto. |
| MSGL-01/03/05 | `Modules/Chat/Infrastructure/MongoOrderMessageRepository.cs` | Almacén e índices implementados; purga posterior. |
| MSGL-01..05 | `tests/UtttGo.UnitTests/ChatServiceTests.cs`, `tests/UtttGo.IntegrationTests/ChatEndpointTests.cs`, `tests/UtttGo.SecurityTests/ChatSecurityTests.cs` | Suites Release ejecutadas con Docker activo; pruebas específicas cubren sanitización, idempotencia, cierre, perfil inactivo, autenticación y ownership nulo. |

## Orden de implementación

DEV-API037-01 ratifica DTO y paginación; DEV-API037-02/03 implementan modelo, repositorio, endpoint y autorización; DEV-API037-04 cubre escenarios y fallos. La purga posterior queda separada.

## Dependencias y bloqueos

| Gate transversal o decisión particular (ID original) | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| DEC-005 (almacenamiento, disparador, purga) | Purga posterior | Acta y job UTC; no bloquea REST de esta entrega. |
| Gate de fase 07 (spec → plan → task) | DEV-API037-01..04 | Ratificación de esta ficha, plan y task. |
| Contrato de participación de Orders | DEV-API037-02/03 | `IOrderChatParticipantReader` publicado por Orders. |
| DEC-003 (cuenta eliminada con chat compartido) | DEV-API037-04 | Política de privacidad aprobada. |

## Estrategia de pruebas

Unitarias de participación, orden y paginación; integración `WebApplicationFactory` con Mongo/Redis locales (Testcontainers) para dos participantes y un tercero; seguridad con JWT revocado, Redis/Mongo caídos y pedido ajeno; purga con reloj controlado solo tras diseño aprobado. Dobles no equivalen a integración externa; sin producción. Comandos: `dotnet test tests/UtttGo.UnitTests/UtttGo.UnitTests.csproj -c Release --no-build` y las suites de integración/seguridad con evidencia fechada.

## Seguridad, errores y observabilidad

JWT/blacklist y rate limit distribuido fail-closed, autorización por participación mediante contrato, error único `correlationId`, logs sin contenido de mensajes ni PII; sin alterar el orden del middleware de `Program.cs`.

## Criterios de cierre técnico

MSGL-01, MSGL-02, MSGL-04 y MSGL-05 con pruebas de REST, seguridad y almacenamiento; build Release, `dotnet format` y suites pertinentes en verde. La purga queda como tarea posterior.
