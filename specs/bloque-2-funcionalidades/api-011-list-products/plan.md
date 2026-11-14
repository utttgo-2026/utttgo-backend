# API-011 — Buscar productos · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Catalog dueño de `products`; lectura pública no implementa ofertas ni Orders.

## Arquitectura existente

No existe `Modules/Catalog` ni tests de productos. Reutilizar arquitectura [base](../../bloque-1-arquitectura/base-modular/spec.md), gate Redis y errores; Sellers expone identidad/perfil, no repositorio compartido. Categorías, Reviews y escritura de productos son dependencias, no implementarlas desde la lectura.

## Componentes involucrados

| Capa | Componente previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/ProductListQuery.cs`, `Contracts/ProductListResponse.cs`, `Validators/ProductListQueryValidator.cs` | Parsear filtros aprobados, paginar DTO y mapear errores. |
| Aplicación | `src/UTTTGO.Api/Modules/Catalog/Application/ListProductsService.cs` | Aplicar visibilidad, composición vía interfaces Sellers/Reviews, política de caché. |
| Datos | `src/UTTTGO.Api/Modules/Catalog/Infrastructure/MongoProductRepository.cs`, adaptador Redis Catalog | Consulta/indexación por filtros aprobados, rate limit/cache; índices en spec Sprint 2, sin inventar global. |

## Secuencia técnica

Tras aprobar filtros: validar paginación/params → rate limit Redis (caída 503) → caché de lectura (caída: Mongo) → consultar `products` visibles con orden estable/paginación → resolver vendedor y ratings por interfaces dueñas → proyectar sin PII ni imágenes no aprobadas. Cambios API-013..015/categorías invalidan caché según contrato; Orders revalida disponibilidad al comprar.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivo | Cambio tras autorización |
|---|---|---|
| LST-01/04 | `Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/ProductListQuery.cs`, `Contracts/ProductListResponse.cs`, `Validators/ProductListQueryValidator.cs` | GET y DTO/paginación aprobados. |
| LST-01/02 | `Modules/Catalog/Application/ListProductsService.cs`, `Infrastructure/MongoProductRepository.cs` | Filtros y proyección sin colecciones de otros dueños. |
| LST-03 | `Modules/Catalog/Infrastructure/RedisProductReadGate.cs`, `src/UTTTGO.Api/Program.cs` (registro DI, no alterar middleware) | Cache fail-open y rate limit fail-closed. |
| LST-01..04 | `tests/UtttGo.UnitTests/ListProductsServiceTests.cs`, `tests/UtttGo.IntegrationTests/ListProductsEndpointTests.cs`, `tests/UtttGo.SecurityTests/ListProductsSecurityTests.cs` | Pruebas nuevas. |

## Orden de implementación

DEV-API011-01 decide parámetros y contrato; DEV-API011-02 crea lectura/paginación sobre modelo dueño de API-013; DEV-API011-03 consume proyecciones de Sellers/Reviews (sin implementar esos módulos); DEV-API011-04 verifica visibilidad, cache/rate limit y seguridad en contenedores.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| TASK-CATALOG-002, aprobación Sprint 2 | DEV-API011-01/02/03/04 | Acta query/DTO/HTTP, índices y límites. |
| API-013 y API-019..022, Sellers/Reviews | DEV-API011-02/03 | Modelo `products`, estado de categoría y contrato lector de ratings publicados por dueños. |

## Estrategia de pruebas

Unitarias filtros/paginación/ratings ausentes; WebApplicationFactory + Mongo/Redis aislados con dos vendedores y categoría activa/inactiva; seguridad límites, rate limit caído, sin PII. Revisar invalidación tras escrituras solo cuando API-013..015 existan. En trabajo autorizado: `dotnet build UTTTGO.Backend.slnx -c Release --no-restore`, `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore` y tres proyectos `dotnet test` Release `--no-build`.

## Seguridad, errores y observabilidad

Error único `correlationId`; no log de query sensible ni PII, filtros acotados, Redis rate limit cerrado/cache abierta. No modificar middleware sin aprobación.

## Criterios de cierre técnico

LST-01..04 con evidencia Mongo/Redis local, contrato aprobado, suites/build/formato verdes, sin endpoint API-016 ni índices globales; TASK-CATALOG-002 no se cierra por redactar ficha.
