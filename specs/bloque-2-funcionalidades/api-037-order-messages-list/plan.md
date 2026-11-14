# API-037 — Historial de mensajes del pedido · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Solo lectura del historial de un pedido; enviar mensajes es [API-038](../api-038-send-order-message/spec.md), la purga obedece a DEC-005 y no se accede a colecciones de otros módulos.

## Arquitectura existente

No hay módulo Chat ni Orders en `src/UTTTGO.Api/Modules/` (existen Admin, Identity y Sellers). Las fichas de Orders ([API-025](../api-025-order-detail/spec.md) y vecinas) son borrador sin código; la participación del pedido debe llegar mediante contrato del dueño de Orders, nunca por lectura directa de su colección.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Chat/Controllers/OrderMessagesController.cs` (previsto) | GET con `{orderId}` y paginación. |
| Aplicación | `src/UTTTGO.Api/Modules/Chat/Application/OrderMessagesService.cs` (previsto) | Participación, lectura y ventana temporal. |
| Datos | `src/UTTTGO.Api/Modules/Chat/Infrastructure/MongoOrderMessageRepository.cs` (previsto) | Mensajes; colección e índices por definir. |
| Contrato | `IOrderParticipation` del dueño de Orders (previsto) | Comprador/vendedor del pedido; sin colección cruzada. |

## Secuencia técnica

JWT/blacklist → rate limit Redis → validar `orderId` → consultar participación mediante contrato de Orders → leer mensajes paginados en orden estable → responder sin campos ajenos. Pasos candidatos hasta acta: parámetros, DTO, código ante tercero y comportamiento tras purga. Mongo/Redis caídos ⇒ `503` fail-closed; nunca mensajes de otro pedido.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos/errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): aplicar solo los pertinentes, sin duplicar reglas.

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| MSGL-01/02 | `Modules/Chat/Controllers/OrderMessagesController.cs`, `Modules/Chat/Application/OrderMessagesService.cs`, `src/UTTTGO.Api/Program.cs` | Solo tras acta DEC-005 y gate de fase 07; middleware intacto. |
| MSGL-01/03/05 | `Modules/Chat/Infrastructure/MongoOrderMessageRepository.cs` | Almacén, índices y purga según acta; sin colecciones de otros módulos. |
| MSGL-01..05 | `tests/UtttGo.UnitTests/OrderMessagesServiceTests.cs`, `tests/UtttGo.IntegrationTests/OrderMessagesEndpointTests.cs`, `tests/UtttGo.SecurityTests/OrderMessagesSecurityTests.cs` | Pruebas locales nuevas; dobles solo en tests. |

## Orden de implementación

DEV-API037-01 ratifica DTO, paginación, almacén y purga; DEV-API037-02 crea modelo y repositorio tras 01 y el contrato de Orders; DEV-API037-03 expone el endpoint y la autorización tras 02; DEV-API037-04 cubre escenarios y fallos tras 02/03. No hay código previo de Chat que reutilizar.

## Dependencias y bloqueos

| Gate transversal o decisión particular (ID original) | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| DEC-005 (almacenamiento, disparador, purga) | DEV-API037-01..04 | Acta de diseño que no contradice BR-012. |
| Gate de fase 07 (spec → plan → task) | DEV-API037-01..04 | Aprobación integral de la fase 07. |
| Contrato de participación de Orders (API-025) | DEV-API037-02/03 | Interfaz publicada por el dueño de Orders. |
| DEC-003 (cuenta eliminada con chat compartido) | DEV-API037-04 | Política de privacidad aprobada. |

## Estrategia de pruebas

Unitarias de participación, orden y paginación; integración `WebApplicationFactory` con Mongo/Redis locales (Testcontainers) para dos participantes y un tercero; seguridad con JWT revocado, Redis/Mongo caídos y pedido ajeno; purga con reloj controlado solo tras diseño aprobado. Dobles no equivalen a integración externa; sin producción. Comandos: `dotnet test tests/UtttGo.UnitTests/UtttGo.UnitTests.csproj -c Release --no-build` y las suites de integración/seguridad con evidencia fechada.

## Seguridad, errores y observabilidad

JWT/blacklist y rate limit distribuido fail-closed, autorización por participación mediante contrato, error único `correlationId`, logs sin contenido de mensajes ni PII; sin alterar el orden del middleware de `Program.cs`.

## Criterios de cierre técnico

MSGL-01..05 con pruebas verdes de lectura, tercero, fallos y purga cuando exista; build Release, `dotnet format` y suites pertinentes en verde con evidencia fechada; aprobación documental y cierre del gate de fase 07 por separado.
