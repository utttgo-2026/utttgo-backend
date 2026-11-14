# API-024 — Listar mis pedidos · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Orders lista propios; [fase 04](../../phase-04-orders-cash/plan.md) v0.1 sin aprobar.

## Arquitectura existente

No hay módulo Orders ni tests. Compartir `OrderDocument`/`MongoOrderRepository` de API-023 cuando exista. Identidad por interfaz Identity; Seller por interfaz Sellers **solo** si D-ORD-05 aprobado. No crear lector directo de `users`/`sellers` en Orders.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Orders/Controllers/OrdersController.cs`, `Contracts/OrdersMineQuery.cs`, `Contracts/OrdersPageResponse.cs`, `Validators/OrdersMineQueryValidator.cs` | GET paginado y DTO sin documentos Mongo. |
| Aplicación | `src/UTTTGO.Api/Modules/Orders/Application/ListOwnOrdersService.cs` | Resolver identidad propia, aplicar filtro participante con alcance ratificado. |
| Datos | `src/UTTTGO.Api/Modules/Orders/Infrastructure/MongoOrderRepository.cs`, `OrderMongoIndexes.cs` | Query por comprador, orden estable e índices según acta. |

## Secuencia técnica

JWT/blacklist → rate limit Redis → validar `page/pageSize` y filtros aprobados → consultar `orders` por `buyerUserId` del actor con orden determinista antes de skip/limit → proyectar sin PII de terceros; solo si D-ORD-05 aprobado, resolver seller propio y filtrar por `sellerId` del perfil. Redis/Mongo caído error, no cachear pedidos. D-ORD-09 no se implementa en lectura.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivos | Cambio condicionado |
|---|---|---|
| MINE-01/03 | `Modules/Orders/Controllers/OrdersController.cs`, `Contracts/OrdersMineQuery.cs`, `Contracts/OrdersPageResponse.cs`, `Validators/OrdersMineQueryValidator.cs`, `Application/ListOwnOrdersService.cs` | GET + validaciones aprobadas. |
| MINE-01/02 | `Modules/Orders/Infrastructure/MongoOrderRepository.cs`, `OrderMongoIndexes.cs`, `src/UTTTGO.Api/Program.cs` | Filtro/index y DI; seller solo con D-ORD-05, sin cambiar middleware. |
| MINE-01..04 | `tests/UtttGo.UnitTests/ListOwnOrdersServiceTests.cs`, `tests/UtttGo.IntegrationTests/ListOwnOrdersEndpointTests.cs`, `tests/UtttGo.SecurityTests/ListOwnOrdersSecurityTests.cs` | Pruebas nuevas. |

## Orden de implementación

DEV-API024-01 ratifica alcance comprador/role y pageSize; DEV-API024-02 implementa lector comprador sobre API-023; DEV-API024-03 integra Seller solo si aprobada extensión; DEV-API024-04 prueba aislamiento/Redis/privacidad. No crear ruta vendedor.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| TASK-ORD-001..004, D-ORD-05 | DEV-API024-01/02/03/04 | Acta fase 04 y alcance `/mine`; modelo `orders` API-023. |
| DEC-003/D-ORD-09 | DEV-API024-04 (solo rama datos compartidos) | Política Identity/Orders sobre borrado; no bloquea documentar lectura activa. |

## Estrategia de pruebas

Unitarias filtros y paginación; WebApplicationFactory Mongo/Redis aislados 25 propios + terceros, orden/total, seller solo con decisión; seguridad JWT falso/revocado, role ajeno, 400/429/503. Build Release, format y tres `dotnet test -c Release --no-build` tras permiso local; no producción.

## Seguridad, errores y observabilidad

Predicado por participante antes de consulta/paginación; no cachear pedidos, rate limit fail-closed; error `correlationId`, logs sin PII/tokens, middleware intacto.

## Criterios de cierre técnico

MINE-01..04 con privacidad y paginación probadas localmente, vendedor solo si aprobado, build/formato/suites verdes; borrado compartido permanece tarea de su dueño.
