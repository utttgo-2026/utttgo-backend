# API-026 — Cancelar pedido propio · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Orders escribe estado/auditoría, Catalog libera stock mediante interfaz; [fase 04](../../phase-04-orders-cash/plan.md) v0.1 no aprobada.

## Arquitectura existente

No existe módulo Orders ni tests; reutilizar `OrderDocument`/`MongoOrderRepository` y reserva de API-023 tras aprobación, rate limit/guard Identity y contrato `ICatalogProductCommands` del dueño Catalog. API-027 pertenece a vendedor; no usar su ruta para cancelar.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Orders/Controllers/OrdersController.cs`, `Contracts/CancelOrderResponse.cs`, `Validators/OrderIdValidator.cs` | POST cancel, errores/DTO ratificados. |
| Aplicación | `src/UTTTGO.Api/Modules/Orders/Application/CancelOrderService.cs` | Verificar comprador/estado, coordinar CAS y liberación idempotente. |
| Datos | `src/UTTTGO.Api/Modules/Orders/Infrastructure/MongoOrderRepository.cs`, `MongoOrderAuditRepository.cs` | Transición condicional, `completedAt`/version y auditoría. |
| Interfaces | Catalog `ICatalogProductCommands`, Notifications `INotificationPublisher` | Liberar una reserva; encolar hecho si aprobado, sin push directo. |

## Secuencia técnica

JWT/blacklist → rate limit Redis → validar orderId/actor comprador → cargar order propio → si Pending (único permitido mientras D-ORD-07 abierta), aplicar transición condicional y liberar reserva **en orden recuperable ratificado D-ORD-02**, registrar auditoría/`completedAt` según acta → emitir notificación mediante interfaz si disponible bajo política aprobada → responder. Replay o carrera no libera segunda vez; fallo en cualquiera de Mongo/Catalog/auditoría exige reconciliación antes de afirmar cierre. No habilitar Accepted por inferencia.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivos | Cambio condicionado |
|---|---|---|
| CAN-01/03/04 | `Modules/Orders/Controllers/OrdersController.cs`, `Contracts/CancelOrderResponse.cs`, `Validators/OrderIdValidator.cs`, `Application/CancelOrderService.cs`, `src/UTTTGO.Api/Program.cs` | Guard comprador, transición, error/DI sin alterar middleware. |
| CAN-01/02/05 | `Modules/Orders/Infrastructure/MongoOrderRepository.cs`, `MongoOrderAuditRepository.cs`, `Application/CancelOrderService.cs` | CAS/auditoría y coordinación idempotente con Catalog; no editar repositorio Catalog. |
| CAN-01..05 | `tests/UtttGo.UnitTests/CancelOrderServiceTests.cs`, `tests/UtttGo.IntegrationTests/CancelOrderEndpointTests.cs`, `tests/UtttGo.SecurityTests/CancelOrderSecurityTests.cs` | Pruebas nuevas, Notification mock. |

## Orden de implementación

DEV-API026-01 ratifica compensación/HTTP; DEV-API026-02 crea guard y transición Pending; DEV-API026-03 integra liberación y auditoría; DEV-API026-04 verifica concurrencia/roles; DEV-API026-05 prueba fallos y recuperación. Sin implementar notificaciones FCM ni Catalog internamente.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| TASK-ORD-001..004, D-ORD-02/06/07/08 | DEV-API026-01/02/03/04/05 | Acta fase 04/stock/CAS, Accepted no autorizado hasta veredicto. |
| API-023 y Catalog Sprint 2 | DEV-API026-02/03 | Pedido con reservationId y comando de liberación idempotente publicado por Catalog. |
| Notifications/Chat, D-ORD-08 | DEV-API026-03/05 | Contratos consumidores de hecho/completedAt; sin acoplar FCM o purga de chat aquí. |

## Estrategia de pruebas

Unitarias estado Pending/Accepted/terminal/propiedad y replay; WebApplicationFactory con Mongo/Redis aislados, CatalogCommands doble con ledger para doble cancel y caída entre CAS/liberación, auditoría/compensación; seguridad seller/tercero, JWT revocado, rate limit caído. Después de acta: build Release, format y tres proyectos `dotnet test -c Release --no-build`; sin producción.

## Seguridad, errores y observabilidad

`buyerUserId` de pedido contra sujeto JWT, `409` transición inválida; Redis fail-closed, error único `correlationId`, logs sin PII/tokens. No cachear pedido, no payment gateway ni enviar push desde Orders.

## Criterios de cierre técnico

CAN-01..05 probados localmente con stock liberado una vez y protocolo de fallo parcial recuperable, auditoría conforme acta, suites/build/formato verdes; Accepted prohibido sin aprobación expresa.
