# API-013 — Publicar producto propio · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Escritor del módulo Catalog, sin primera oferta ni Media en Sprint 2.

## Arquitectura existente

No existe Catalog ni tests de productos. Reutilizar lector JWT/perfil local de Identity mediante contrato y vendedor propio de Sellers mediante interfaz pública, nunca repositorios de otros módulos; categorías serán dueñas del catálogo cuando API-019..022 existan.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/CreateProductRequest.cs`, `Contracts/ProductResponse.cs`, `Validators/CreateProductRequestValidator.cs` | Whitelist, JSON/HTTP según acta. |
| Aplicación | `src/UTTTGO.Api/Modules/Catalog/Application/CreateProductService.cs`, `ISellerForCatalogReader.cs` | Derivar Seller propio/estado, validar categoría por interfaz Catalog y crear sin oferta. |
| Datos | `src/UTTTGO.Api/Modules/Catalog/Infrastructure/ProductDocument.cs`, `MongoProductRepository.cs`, `CatalogMongoIndexes.cs` | `products` de Catalog, índices de búsquedas reales sin unicidad global. |

## Secuencia técnica

JWT/blacklist y rate limit Redis → validar campos, precio/stock → resolver Seller propio activo (API-009) → consultar categoría activa por contrato dueño (API-019..022) → insertar documento propio con `_id`/version/UTC generados backend → invalidar cache API-011/012 → responder tras confirmación. Si falla dependencia crítica no afirmar publicación; acordar política de retry e invalidación antes de código. Imagen se rechaza hasta fase 05.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivo | Cambio condicionado |
|---|---|---|
| CRT-01/03 | `Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/CreateProductRequest.cs`, `Contracts/ProductResponse.cs`, `Validators/CreateProductRequestValidator.cs`, `Application/CreateProductService.cs` | POST con DTO/errores aprobados. |
| CRT-01/02 | `Modules/Catalog/Infrastructure/ProductDocument.cs`, `MongoProductRepository.cs`, `CatalogMongoIndexes.cs`, `src/UTTTGO.Api/Program.cs` | Insert e índices, registrar DI sin alterar middleware. |
| CRT-01..04 | `tests/UtttGo.UnitTests/CreateProductServiceTests.cs`, `tests/UtttGo.IntegrationTests/CreateProductEndpointTests.cs`, `tests/UtttGo.SecurityTests/CreateProductSecurityTests.cs` | Pruebas nuevas en entorno aislado. |

## Orden de implementación

DEV-API013-01 aprueba contrato/modelo e interfaces; DEV-API013-02 crea DTO, validador, servicio; DEV-API013-03 crea repo/índices y prueba homónimos; DEV-API013-04 cubre seguridad/categoría y errores. API-011/012 reutilizan repo; Media/Orders son externos.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| TASK-CATALOG-002, Sprint 2 | DEV-API013-01/02/03/04 | Acta payload, visibilidad, índices y estados. |
| API-009, API-019..022 | DEV-API013-02/04 | Contratos Seller propio y categoría activa de sus dueños. |
| API-017/018 | DEV-API013-04 | Solo dependencia futura para imágenes; sin bloquear documentación sin imagen. |

## Estrategia de pruebas

Unitarias precio decimal/stock/whitelist; integración WebApplicationFactory + Mongo/Redis locales con dos Seller homónimos, categoría ausente/oculta, fallo parcial; seguridad Buyer/token ajeno/revocado/imagen no aprobada/429/503. Cuando autorizado: `dotnet build UTTTGO.Backend.slnx -c Release --no-restore`, `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore`, tres `dotnet test` proyectos Release `--no-build`.

## Seguridad, errores y observabilidad

Error único `correlationId`, rate limit fail-closed; no confiar en campos privilegiados ni publicar imagen; logs sin tokens/PII. No alterar orden del middleware.

## Criterios de cierre técnico

CRT-01..04 demostrados con Mongo/Redis locales, propietario derivado, homónimos permitidos, imagen rechazada, suites verdes; contrato Sprint 2 aprobado y evidencia de interfaces dueñas.
