# API-028 — Emitir token de entrega · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Orders emite secreto para comprador; entrega API-029, no aquí. [Plan fase 04](../../phase-04-orders-cash/plan.md) no aprobado.

## Arquitectura existente

No existe Orders ni pruebas; reutilizar `OrderDocument`, `MongoOrderRepository` y auditoría de API-023/027 al implementarse. No crear colección de tokens ni estado en memoria de réplicas.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Orders/Controllers/OrdersController.cs`, `Contracts/DeliveryTokenResponse.cs`, `Validators/OrderIdValidator.cs` | POST y respuesta solo al comprador. |
| Aplicación | `src/UTTTGO.Api/Modules/Orders/Application/IssueDeliveryTokenService.cs` | Generador CSPRNG, hash y expiración, sin logs del secreto. |
| Datos | `src/UTTTGO.Api/Modules/Orders/Infrastructure/MongoOrderRepository.cs`, `MongoOrderAuditRepository.cs` | Reemplazo condicional de hash/expiración, auditoría sin token. |

## Secuencia técnica

JWT/blacklist → rate limit Redis → validar orderId, comprador propio y estado Ready → generar secreto seguro → actualizar condicionalmente hash/expiry solo si sigue Ready y versión esperada → auditar sin secreto → devolver token una sola vez si commit confirmado. Si auditoría falla o compite confirmación de API-029, resolver antes política de compensación/reintento; nunca devolver token no persistido ni escribirlo en logs.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| TOK-01/03 | `Modules/Orders/Controllers/OrdersController.cs`, `Contracts/DeliveryTokenResponse.cs`, `Application/IssueDeliveryTokenService.cs`, `src/UTTTGO.Api/Program.cs` | Auth/generación/DI tras acta; middleware intacto. |
| TOK-01/02/04 | `Modules/Orders/Infrastructure/MongoOrderRepository.cs`, `MongoOrderAuditRepository.cs` | CAS hash y auditoría, sin token claro, tras D-ORD-06. |
| TOK-01..04 | `tests/UtttGo.UnitTests/IssueDeliveryTokenServiceTests.cs`, `tests/UtttGo.IntegrationTests/DeliveryTokenEndpointTests.cs`, `tests/UtttGo.SecurityTests/DeliveryTokenSecurityTests.cs` | Casos locales, sin imprimir secreto. |

## Orden de implementación

DEV-API028-01 ratifica secreto/TTL/concurrencia; DEV-API028-02 crea guard y generador; DEV-API028-03 reemplazo CAS/auditoría; DEV-API028-04 prueba carreras, fugas y degradación.

## Dependencias y bloqueos

| Gate transversal o decisión particular | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| TASK-ORD-001..004, D-ORD-03/06/10 | DEV-API028-01..04 | Acta fase 04 con token/reemplazo/rate limit y Sprint 2. |
| API-023/027/029 | DEV-API028-02..04 | Documento Orders, transición Ready y consumo único acordado con confirmación. |

## Estrategia de pruebas

Unitarias CSPRNG/hash/TTL y no log; WebApplicationFactory con Mongo/Redis locales Ready, reemplazo y emisión simultánea con confirmación; seguridad comprador vs vendedor, revocado, Redis caído y redacción de errores. Después de acta: build Release, format, tres suites `dotnet test -c Release --no-build` con evidencia sanitizada; sin producción.

## Seguridad, errores y observabilidad

Solo comprador por `buyerUserId`; hash persistido, secreto únicamente respuesta HTTPS aprobada; rate limit fail-closed, error `correlationId`; no token ni hash en logs. No modificar middleware.

## Criterios de cierre técnico

TOK-01..04 probados en aislado, reemplazo y confirmación sin doble uso; build/formato/suites verdes, secretos excluidos de evidencia.
