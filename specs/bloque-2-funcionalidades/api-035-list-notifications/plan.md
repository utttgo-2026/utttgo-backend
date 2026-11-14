# API-035 — Listar notificaciones propias · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Notifications dueño de sus entradas y FCM; Orders/Chat solo publican hechos, no escriben su colección.

## Arquitectura existente

No existe Notifications ni tests. Reutilizar gate JWT/rate limit de Bloque 1; modelo/escritor de bandeja depende de contrato fase 06 y se comparte con API-036. `INotificationPublisher` fase 04 es borrador, no interfaz implementada.

## Componentes involucrados

| Capa | Componente previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Notifications/Controllers/NotificationsController.cs`, `Contracts/NotificationsPageResponse.cs`, `Validators/NotificationsQueryValidator.cs` | GET, paginación/DTO. |
| Aplicación | `src/UTTTGO.Api/Modules/Notifications/Application/ListNotificationsService.cs` | Resolver sujeto, proyectar sin secretos/datos caducados. |
| Datos | `src/UTTTGO.Api/Modules/Notifications/Infrastructure/MongoNotificationRepository.cs`, `NotificationMongoIndexes.cs` | Query recipient+orden; colección candidata `notifications` tras acta. |
| Interfaces | Notifications publisher para Orders/Chat, adaptador FCM separado | Emitir hechos idempotentes; lectura GET no manda push. |

## Secuencia técnica

JWT/blacklist → rate limit Redis → validar `page/pageSize` → resolver destinatario → consulta en colección Notifications por recipientUserId con filtro/orden determinista y paginación en Mongo → DTO autorizado; FCM no se invoca. Para fallos Mongo/Redis, error único; política de purga de chat/baja acordada antes de proyectar datos vinculados. Escritura/dispatch se diseñan por separado, no se infiere que FCM crea registro.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Ruta/componente | Cambio condicionado |
|---|---|---|
| NOTL-01/03 | `Modules/Notifications/Controllers/NotificationsController.cs`, `Contracts/NotificationsPageResponse.cs`, `Validators/NotificationsQueryValidator.cs`, `Application/ListNotificationsService.cs`, `src/UTTTGO.Api/Program.cs` | GET/guard/DI tras acta, middleware intacto. |
| NOTL-01/02 | `Modules/Notifications/Infrastructure/MongoNotificationRepository.cs`, `NotificationMongoIndexes.cs` | Reusar API-036; lectura e índice tras modelo bandeja aprobado. |
| NOTL-01..04 | `tests/UtttGo.UnitTests/ListNotificationsServiceTests.cs`, `tests/UtttGo.IntegrationTests/ListNotificationsEndpointTests.cs`, `tests/UtttGo.SecurityTests/ListNotificationsSecurityTests.cs` | Tests locales, FCM mock. |

## Orden de implementación

DEV-API035-01 aprueba modelo/DTO/TTL; DEV-API035-02 GET paginado; DEV-API035-03 integra lectura con política de hechos/duplicados/privacidad (sin programar emisión de push en GET); DEV-API035-04 pruebas. Reusar modelo API-036 cuando exista.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| API-035/036 fase 06 sin spec | DEV-API035-01..04 | Acta persistencia, DTO/retención/HTTP y máximo pageSize. |
| Orders/Chat publishers | DEV-API035-03/04 | Hechos aprobados y garantía idempotente, no acceso directo. |
| DEC-003/005, BR-012 | DEV-API035-04 datos vinculados | Política de baja/purga aprobada. |

## Estrategia de pruebas

Unit filtrado y page; WebApplicationFactory Mongo/Redis aislados dos usuarios/25 registros, duplicado de hecho con dobles, sin FCM real; seguridad JWT revocado, Redis/Mongo caídos, fuga contenido chat purgado. Tras acta: build Release, format y tres suites `dotnet test -c Release --no-build`, evidencia fechada.

## Seguridad, errores y observabilidad

recipientUserId server-side, rate limit/blacklist fail-closed, no deviceToken ni credenciales FCM en logs/respuesta; error `correlationId`, middleware intacto.

## Criterios de cierre técnico

NOTL-01..04 probados localmente, sin terceros ni duplicados visibles según acta; build/formato/suites verdes; GET no envía FCM.
