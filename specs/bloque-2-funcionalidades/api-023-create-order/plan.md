# API-023 — Crear pedido directo · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Orders dueño de pedido, Catalog dueño de stock; [fase 04 v0.1](../../phase-04-orders-cash/plan.md) no aprobada.

## Arquitectura existente

No hay `Modules/Orders` ni tests. Reutilizar guard JWT/perfil local Identity, Sellers propietario y Bloque 1; `ICatalogProductReader/Commands` solo tras entrega por dueño Catalog Sprint 2. No leer ni escribir colecciones Catalog/Sellers desde Orders.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Orders/Controllers/OrdersController.cs`, `Contracts/CreateOrderRequest.cs`, `Contracts/OrderResponse.cs`, `Validators/CreateOrderRequestValidator.cs` | Whitelist y respuesta ratificada, sin aceptar importes. |
| Aplicación | `src/UTTTGO.Api/Modules/Orders/Application/CreateOrderService.cs`, `ICatalogProductReader.cs`, `ICatalogProductCommands.cs` (interfaces consumidas del módulo dueño; no implementar allí) | Autorizar, derivar precio/vendedor, coordinar reserva idempotente. |
| Datos | `src/UTTTGO.Api/Modules/Orders/Infrastructure/OrderDocument.cs`, `MongoOrderRepository.cs`, `OrderMongoIndexes.cs`, `MongoOrderAuditRepository.cs` | `orders` y `orderAuditLogs` propias; snapshot/índices solo tras decisión. |

## Secuencia técnica

Tras aprobar D-ORD-01/02/04: JWT/estado activo → rate limit Redis → validar productId/cantidad/extras e idempotency key si aprobada → Catalog reader + Seller reader verifican producto, categoría y vendedor activos/precio vigente → Catalog commands reserva con `reservationId` atómico → Orders inserta pedido Pending con importe decimal MXN y Cash/registro auditoría → si inserción/auditoría falla, ejecutar compensación recuperable según acta, nunca confirmar sin consistencia; duplicado idempotente devuelve mismo pedido sin segunda reserva. No efectuar transacción cross-module ficticia.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivos | Cambio condicionado |
|---|---|---|
| ORD-01/05 | `Modules/Orders/Controllers/OrdersController.cs`, `Contracts/CreateOrderRequest.cs`, `Contracts/OrderResponse.cs`, `Validators/CreateOrderRequestValidator.cs`, `Application/CreateOrderService.cs` | POST, guard y precio Cash server-side. |
| ORD-01/02/03/04 | `Modules/Orders/Infrastructure/OrderDocument.cs`, `MongoOrderRepository.cs`, `OrderMongoIndexes.cs`, `MongoOrderAuditRepository.cs`, `src/UTTTGO.Api/Program.cs` | Inserción/índices/retry/auditoría y DI, no alterar middleware. |
| ORD-01..05 | `tests/UtttGo.UnitTests/CreateOrderServiceTests.cs`, `tests/UtttGo.IntegrationTests/CreateOrderEndpointTests.cs`, `tests/UtttGo.SecurityTests/CreateOrderSecurityTests.cs` | Pruebas locales nuevas. |

## Orden de implementación

DEV-API023-01 confirma acta/gates y contrato Catalog; DEV-API023-02 crea API/DTO/servicio; DEV-API023-03 agrega reserva/inserción/compensación; DEV-API023-04 asegura clave idempotente si se aprueba; DEV-API023-05 cubre fallos/seguridad. No crear colecciones Catalog en Orders.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| TASK-ORD-001..004, D-ORD-01/02/04/06 | DEV-API023-01/02/03/04/05 | Acta fase 04 y contratos stock/fase 03 Sprint 2, estrategias de fallo. |
| API-013/015/022 y API-009, Identity | DEV-API023-02/03 | Interfaces publicadas que revaliden producto, categoría y vendedor, sin acceso cruzado. |

## Estrategia de pruebas

Unitarias total decimal, whitelist/cantidad, idempotencia y fallos; WebApplicationFactory con Mongo/Redis aislados y dobles Catalog/Sellers para carrera stock, inserción fallida/retry sin reserva huérfana; seguridad JWT, actor, precio manipulado, Redis caído. Nunca Atlas/S3/producción. Tras acta: build Release, format y tres proyectos `dotnet test -c Release --no-build` con evidencia fechada.

## Seguridad, errores y observabilidad

Rate limit fail-closed, `correlationId` en error/auditoría, sin JWT/PII/credenciales en logs; `paymentMethod=Cash`, no datos financieros ni caché de pedidos. Middleware intacto.

## Criterios de cierre técnico

ORD-01..05 con pruebas de un producto/reserva, snapshot según acta, concurrencia y compensación/retry, build/formato/suites verdes; TASK-ORD-020 cerrado solo por evidencia adicional, no por esta ficha.
