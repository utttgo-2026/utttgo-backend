# API-015 — Cambiar disponibilidad de producto · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Solo estado de publicación del vendedor, no stock, producto eliminado ni pedido.

## Arquitectura existente

No existe Catalog. Reutilizar `ProductDocument`/`MongoProductRepository` de API-013 y lectura API-011/012 cuando se aprueben; perfil Seller de módulo dueño. Orders lee elegibilidad mediante interfaz Catalog, no escribe `products` a través de esta ruta.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/ChangeProductAvailabilityRequest.cs`, `Validators/ChangeProductAvailabilityValidator.cs` | PATCH, intención/DTO/errores aprobados. |
| Aplicación | `src/UTTTGO.Api/Modules/Catalog/Application/ChangeProductAvailabilityService.cs` | Autorizar propiedad, aplicar transición sin tocar stock/precio. |
| Datos | `src/UTTTGO.Api/Modules/Catalog/Infrastructure/MongoProductRepository.cs`, Redis read gate Catalog | CAS por propietario/estado/version según acta e invalidación de caché. |

## Secuencia técnica

JWT/blacklist y rate limit → validar ID e intención aprobada → resolver Seller propio → cargar producto, propietario y estado → transición condicional (no `SoldOut` por pausa) → invalidar caché API-011/012 → responder conforme acta. Orders verifica elegibilidad actual al comprar; Redis caído no permite operación. Fallo parcial de invalidación se diseña antes de exponer lecturas obsoletas.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivo | Cambio condicionado |
|---|---|---|
| AVL-01/02/03 | `Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/ChangeProductAvailabilityRequest.cs`, `Validators/ChangeProductAvailabilityValidator.cs`, `Application/ChangeProductAvailabilityService.cs` | PATCH/estado tras acta. |
| AVL-01/04 | `Modules/Catalog/Infrastructure/MongoProductRepository.cs`, Redis read gate Catalog, `src/UTTTGO.Api/Program.cs` | Escritura condicional/invalidez y DI sin alterar middleware. |
| AVL-01..04 | `tests/UtttGo.UnitTests/ChangeProductAvailabilityServiceTests.cs`, `tests/UtttGo.IntegrationTests/ChangeProductAvailabilityEndpointTests.cs`, `tests/UtttGo.SecurityTests/ChangeProductAvailabilitySecurityTests.cs` | Pruebas nuevas. |

## Orden de implementación

DEV-API015-01 decide transición y contrato Orders; DEV-API015-02 implementa pausa con propiedad sobre API-013; DEV-API015-03 implementa reactivación solo si aprobada y coherencia de caché; DEV-API015-04 comprueba carrera/seguridad. Ninguna ruta DELETE nueva.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| TASK-CATALOG-002, Sprint 2 | DEV-API015-01/02/03/04 | Acta estado, reactivación, HTTP, CAS. |
| API-013/014/019..022 y Orders API-023 | DEV-API015-02/03/04 | Modelo producto, stock/categoría y contrato elegibilidad. |
| TASK-CATALOG-004 | DEV-API015-03/04 | Baja lógica `Deleted` separada; no reactivar eliminado. |

## Estrategia de pruebas

Unitarias transición/stock preservado; integración WebApplicationFactory + Mongo/Redis aislados, cache invalidada y pedido concurrente con interfaz Orders simulada; seguridad Seller ajeno/Buyer/JWT revocado/estado Deleted/429/503. Tras acta: build Release, format y tres `dotnet test` proyectos `-c Release --no-build`; evidencia fechada local, nunca producción.

## Seguridad, errores y observabilidad

Filtrar por propietario/version, no confiar en estado enviado salvo intención ratificada; Redis fail-closed rate limit y cache fail-open para lectura; errores `correlationId`, logs sin PII/tokens; no modificar middleware.

## Criterios de cierre técnico

AVL-01..04 con Mongo/Redis local, stock/precio preservados, no venta tras pausa, reactivación solo bajo política, build/formato/suites verdes; revisión Orders independiente.
