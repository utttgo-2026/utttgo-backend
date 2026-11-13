# API-012 — Detalle de producto · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Lectura de un producto de Catalog, sin escritura ni oferta.

## Arquitectura existente

No existe `Modules/Catalog` ni tests del endpoint. Reutilizar modelo/escritor de API-013 cuando se apruebe, interfaces Sellers/Reviews de sus dueños; no leer `sellers` o `reviews` directamente desde Catalog.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/ProductDetailResponse.cs`, `Validators/ProductIdValidator.cs` | Ruta, ID y DTO aprobado. |
| Aplicación | `src/UTTTGO.Api/Modules/Catalog/Application/GetProductService.cs` | Visibilidad, lectura y proyección vía contratos dueños. |
| Datos | `src/UTTTGO.Api/Modules/Catalog/Infrastructure/MongoProductRepository.cs`, Redis read gate previsto | Buscar por `_id` y rate limit/cache; índice `_id` implícito sin índice global de nombre. |

## Secuencia técnica

Validar ID → rate limit Redis (fail-closed) → caché opcional (fail-open) → leer `products` → comprobar estado/categoría según decisión → proyectar vendedor/ratings solo vía interfaz, omitir Media hasta contrato fase 05 → responder o error único. No aceptar precio/stock del cliente; Orders revalida al comprar.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivos | Cambio condicionado |
|---|---|---|
| DET-01/02 | `Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/ProductDetailResponse.cs`, `Application/GetProductService.cs`, `Infrastructure/MongoProductRepository.cs` | GET/DTO/modelo compartido con API-013; DI en `src/UTTTGO.Api/Program.cs` sin alterar middleware. |
| DET-03 | `Modules/Catalog/Validators/ProductIdValidator.cs`, Redis gate Catalog | Validación y rate limit aprobados. |
| DET-01..04 | `tests/UtttGo.UnitTests/GetProductServiceTests.cs`, `tests/UtttGo.IntegrationTests/GetProductEndpointTests.cs`, `tests/UtttGo.SecurityTests/GetProductSecurityTests.cs` | Pruebas nuevas; sin invocar servicios reales. |

## Orden de implementación

DEV-API012-01 define estados/DTO/HTTP; DEV-API012-02 implementa GET sobre modelo API-013 sin segundo repo; DEV-API012-03 integra/valida seguridad y dependencias opcionales. Media/Reviews no se programan aquí.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| TASK-CATALOG-002, Sprint 2 | DEV-API012-01/02/03 | Acta contrato detalle, estados/ID/HTTP. |
| API-013/019..022, Sellers, Reviews/Media | DEV-API012-02/03 | Modelo y contratos lectores; ausencia opcional no debe inventar datos. |

## Estrategia de pruebas

Unitarias estados/rating ausente, integración WebApplicationFactory y Mongo/Redis aislados para producto activo/pausado/ausente y categoría inactiva; seguridad ID inválido, 429/503, sin PII/imagen no moderada. Registrar build Release, format y tres proyectos `dotnet test -c Release --no-build` cuando se autorice; nada productivo.

## Seguridad, errores y observabilidad

Error único con `correlationId`; cache fail-open y rate limit fail-closed; logs sin datos privados; no cambiar middleware.

## Criterios de cierre técnico

DET-01..04 con pruebas fechadas conforme decisión, sin ofertas ni imágenes sin aprobar; build/formato/suites verdes y ficha Sprint 2 ratificada.
