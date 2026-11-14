# API-036 — Marcar notificación propia como leída · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Notifications actualiza solo documento propio, no envía FCM ni altera Orders/Chat.

## Arquitectura existente

No hay módulo Notifications ni tests. Compartir modelo/repositorio del listado API-035 cuando se apruebe, sin crear segunda colección. Reutilizar Identity y Bloque 1; FCM no participa.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Notifications/Controllers/NotificationsController.cs`, `Validators/NotificationIdValidator.cs`, `Contracts/MarkNotificationReadResponse.cs` | PATCH y respuesta aprobada. |
| Aplicación | `src/UTTTGO.Api/Modules/Notifications/Application/MarkNotificationReadService.cs` | Derivar actor, decidir idempotencia/purga. |
| Datos | `src/UTTTGO.Api/Modules/Notifications/Infrastructure/MongoNotificationRepository.cs` | Actualización condicional `_id+recipientUserId` con primera fecha estable si aprobada. |

## Secuencia técnica

JWT/blacklist → rate limit Redis → validar notificationId → derivar recipientUserId → actualización condicional en Notifications por ID+destinatario y no leído si acta conserva primera fecha → respuesta sin divulgación de tercero; invalidar caché de API-035 si aprobada. Mongo/Redis caídos no se reportan como éxito; si registro expiró no resucitar. No FCM ni cambios de otro módulo.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Ruta/componente | Cambio condicionado |
|---|---|---|
| NOTR-01/02/04 | `Modules/Notifications/Controllers/NotificationsController.cs`, `Validators/NotificationIdValidator.cs`, `Contracts/MarkNotificationReadResponse.cs`, `Application/MarkNotificationReadService.cs`, `src/UTTTGO.Api/Program.cs` | PATCH/guard/DI tras acta, middleware intacto. |
| NOTR-01/03 | `Modules/Notifications/Infrastructure/MongoNotificationRepository.cs` | Reusar API-035, filtro por destinatario y fecha conforme acta. |
| NOTR-01..04 | `tests/UtttGo.UnitTests/MarkNotificationReadServiceTests.cs`, `tests/UtttGo.IntegrationTests/MarkNotificationReadEndpointTests.cs`, `tests/UtttGo.SecurityTests/MarkNotificationReadSecurityTests.cs` | Tests locales nuevos. |

## Orden de implementación

DEV-API036-01 ratifica cuerpo/replay/HTTP; DEV-API036-02 PATCH/guard; DEV-API036-03 update atómico/idempotencia y caché; DEV-API036-04 seguridad/fallos. Reusar infraestructura API-035, no programar envío FCM.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| API-035/036 fase 06 | DEV-API036-01..04 | Acta bandeja/readAt/tercero/idempotencia/HTTP. |
| API-035 | DEV-API036-02/03/04 | Modelo y consulta propios, invalidación si cache aprobada. |
| DEC-003/005 | DEV-API036-04 baja/purga | Política privacidad aprobada; no inventar handler. |

## Estrategia de pruebas

Unit respuesta ausente/reintento; WebApplicationFactory Mongo/Redis locales con dos usuarios, dos PATCH concurrentes y purga controlada, API-035 reflejando lectura; seguridad tercero/revocado/Redis caído, FCM mock con cero invocaciones. Tras acta: build Release, format, tres suites `dotnet test -c Release --no-build` con evidencia fechada; no producción.

## Seguridad, errores y observabilidad

Mongo filtro compuesto por destinatario derivado, JWT y Redis fail-closed, error `correlationId`, logs sin PII/token; sin alterar middleware.

## Criterios de cierre técnico

NOTR-01..04 con pruebas de propietario/retry/expiración, GET API-035 consistente, FCM no invocado y suites/build/formato verdes.
