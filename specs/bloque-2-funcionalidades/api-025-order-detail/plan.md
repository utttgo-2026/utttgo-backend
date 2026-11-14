# API-025 — Consultar detalle de pedido · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Consulta Orders por participante; fase 04 v0.1 no aprobada.

## Arquitectura existente

No hay Orders ni tests. Reutilizar `OrderDocument`/`MongoOrderRepository` futuros API-023, gate Identity y contrato Seller propio del dueño Sellers; no acceder directamente a colección de perfiles ajenos.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Orders/Controllers/OrdersController.cs`, `Contracts/OrderDetailResponse.cs`, `Validators/OrderIdValidator.cs` | GET por orderId y DTO separado de BSON. |
| Aplicación | `src/UTTTGO.Api/Modules/Orders/Application/GetOrderDetailService.cs` | Resolver actor y aplicar autorización por recurso, proyección mínima por rol. |
| Datos | `src/UTTTGO.Api/Modules/Orders/Infrastructure/MongoOrderRepository.cs` | Buscar `_id`/participante con índice implícito, nunca devolver registros ajenos. |

## Secuencia técnica

JWT/blacklist y Redis rate limit → validar orderId → resolver comprador Identity / vendedor propio Sellers → consultar order en colección Orders → verificar participante antes de proyectar → respuesta DTO aprobado; ausente/no participante respuesta indistinguible propuesta, falla Mongo/Sellers => error único. No cache de pedidos ni leer auditLogs. Borrado compartido DEC-003 por ficha API-005.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivo | Cambio condicionado |
|---|---|---|
| DETAIL-01/02/03 | `Modules/Orders/Controllers/OrdersController.cs`, `Contracts/OrderDetailResponse.cs`, `Validators/OrderIdValidator.cs`, `Application/GetOrderDetailService.cs`, `Infrastructure/MongoOrderRepository.cs`, `src/UTTTGO.Api/Program.cs` | GET+guard, DI sin cambiar middleware. |
| DETAIL-01..04 | `tests/UtttGo.UnitTests/GetOrderDetailServiceTests.cs`, `tests/UtttGo.IntegrationTests/GetOrderDetailEndpointTests.cs`, `tests/UtttGo.SecurityTests/GetOrderDetailSecurityTests.cs` | Pruebas de propiedad/datos. |

## Orden de implementación

DEV-API025-01 ratifica DTO según actor y HTTP; DEV-API025-02 añade consulta propia sobre modelo API-023; DEV-API025-03 aplica proyección comprador/vendedor sin fugas; DEV-API025-04 prueba JWT/Redis y denegación. No implementar handler de eliminación DEC-003 aquí.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| TASK-ORD-001..004, D-ORD-01/10 | DEV-API025-01/02/03/04 | Acta fase 04/DTO/error y modelo Orders creado. |
| API-023 y Sellers API-009 | DEV-API025-02/03 | Pedido con sellerId y contrato de vendedor propio; sin lectura directa Sellers. |
| DEC-003/D-ORD-09 | API-005, no handler aquí | Política compartida aprobada si afecta visibilidad tras eliminación. |

## Estrategia de pruebas

Unitarias actor comprador/vendedor/ajeno y DTO seguro; integración WebApplicationFactory Mongo/Redis aislados con dos vendedores, comprador y tercero; seguridad pedido ajeno vs inexistente indistinguible, JWT revocado, rate limit, 503. Tras acta: build Release, format, tres proyectos `dotnet test -c Release --no-build` y evidencia sanitizada.

## Seguridad, errores y observabilidad

Autorización por `buyerUserId` o vendedor vinculado, sin exposición de hash token, datos privados de tercero ni Mongo crudo; Redis fail-closed, errores `correlationId`, sin caché ni PII en logs; middleware intacto.

## Criterios de cierre técnico

DETAIL-01..04 verificados en local con respuesta por actor aprobada; acceso ajeno no revela existencia, build/formato/suites verdes, borrado compartido permanece decisión externa.
