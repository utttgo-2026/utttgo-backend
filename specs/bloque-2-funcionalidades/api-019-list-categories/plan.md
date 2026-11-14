# API-019 — Listar categorías · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Catalog dueño de `categories`; solo listado, sin escribir productos.

## Arquitectura existente

No existe `Modules/Catalog` ni tests de categorías. Reutilizar arquitectura modular, contratos/paginación transversal, Mongo/Redis; misma colección/reader que API-020/021/022, no segunda fuente de categorías.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Catalog/Controllers/CategoriesController.cs`, `Contracts/CategoryListQuery.cs`, `Contracts/CategoryListResponse.cs`, `Validators/CategoryListQueryValidator.cs` | GET, paginación y DTO. |
| Aplicación | `src/UTTTGO.Api/Modules/Catalog/Application/ListCategoriesService.cs` | Visibilidad/orden por decisión e invalidación cache. |
| Datos | `src/UTTTGO.Api/Modules/Catalog/Infrastructure/CategoryDocument.cs`, `MongoCategoryRepository.cs`, Redis read gate | Consulta `categories` e índices según filtros reales. |

## Secuencia técnica

Validar page/pageSize → rate limit Redis fail-closed → caché opcional fail-open → consultar `categories` de Catalog, filtrar estados/ordenar/paginar según acta → proyectar DTO público. Escrituras API-020/021/022 notifican invalidación bajo contrato común, sin borrar productos.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivo | Cambio condicionado |
|---|---|---|
| CAT-01/02 | `Modules/Catalog/Controllers/CategoriesController.cs`, `Contracts/CategoryListQuery.cs`, `Contracts/CategoryListResponse.cs`, `Application/ListCategoriesService.cs`, `Infrastructure/CategoryDocument.cs`, `MongoCategoryRepository.cs` | Lector paginado sobre modelo común API-020. |
| CAT-03/04 | `Modules/Catalog/Validators/CategoryListQueryValidator.cs`, Redis read gate Catalog, `src/UTTTGO.Api/Program.cs` | Límites y DI sin alterar middleware. |
| CAT-01..04 | `tests/UtttGo.UnitTests/ListCategoriesServiceTests.cs`, `tests/UtttGo.IntegrationTests/ListCategoriesEndpointTests.cs`, `tests/UtttGo.SecurityTests/ListCategoriesSecurityTests.cs` | Pruebas nuevas. |

## Orden de implementación

DEV-API019-01 ratifica proyección/orden; DEV-API019-02 implementa lector/paginación reutilizando API-020; DEV-API019-03 prueba Redis y coherencia con API-021/022. La desactivación es API-022, no esta tarea.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| Sprint 2, API-019..022 | DEV-API019-01/02/03 | Acta DTO/slug/orden/ocultación y límites. |
| API-020/021/022 | DEV-API019-02/03 | Modelo compartido y contrato de invalidación; no implementar escrituras en GET. |

## Estrategia de pruebas

Unitarias orden/paginación y visibilidad; integración WebApplicationFactory Mongo/Redis local con activas/inactivas, cambios Admin y caché; seguridad pageSize excesivo, rate limit, fallo Redis/Mongo, ausencia datos internos. Tras aprobación: build Release, format y tres proyectos `dotnet test -c Release --no-build`; no producción.

## Seguridad, errores y observabilidad

Error único `correlationId`, rate limit fail-closed, caché fail-open; sin datos privados ni query sin límites; no modificar middleware.

## Criterios de cierre técnico

CAT-01..04 con evidencia fechada, paginación consistente, lectura tras cambio sin borrado de productos, build/formato/suites verdes.
