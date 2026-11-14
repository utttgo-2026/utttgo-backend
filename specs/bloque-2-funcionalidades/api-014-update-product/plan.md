# API-014 — Editar producto propio · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Catalog modifica solo su producto, no Orders ni Media.

## Arquitectura existente

No existe Catalog; reutilizar `ProductDocument`/`MongoProductRepository` de API-013 cuando se implementen, perfil Seller de Sellers mediante contrato dueño, gate Identity/Redis y errores comunes. No convertir `PATCH /sellers/me` en edición de producto.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/UpdateProductRequest.cs`, `Validators/UpdateProductRequestValidator.cs` | PATCH parcial/whitelist y DTO de API-013. |
| Aplicación | `src/UTTTGO.Api/Modules/Catalog/Application/UpdateProductService.cs` | Autorizar vendedor propietario, validar cambios y concurrencia. |
| Datos | `src/UTTTGO.Api/Modules/Catalog/Infrastructure/MongoProductRepository.cs` | Filtrar por `_id`/`sellerId`/versión según acta; invalidación cache. |

## Secuencia técnica

Validar JWT/perfil local y `productId` → validar PATCH parcial sin campos privilegiados → rate limit Redis → resolver Seller propio por interfaz → cargar `products` → verificar propiedad/estado/categoría → escritura condicional versión y stock según contrato Orders → invalidar cache API-011/012 → devolver DTO. Ante conflicto/caída no sobreescribir ni confirmar cambio; no publicar imagen.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivo | Cambio condicionado |
|---|---|---|
| UPD-01/02 | `Modules/Catalog/Controllers/ProductsController.cs`, `Contracts/UpdateProductRequest.cs`, `Validators/UpdateProductRequestValidator.cs`, `Application/UpdateProductService.cs` | PATCH con propiedad y whitelist tras acta. |
| UPD-03 | `Modules/Catalog/Infrastructure/MongoProductRepository.cs`, `src/UTTTGO.Api/Program.cs` | CAS/index y DI sobre repo compartido, sin tocar middleware. |
| UPD-01..04 | `tests/UtttGo.UnitTests/UpdateProductServiceTests.cs`, `tests/UtttGo.IntegrationTests/UpdateProductEndpointTests.cs`, `tests/UtttGo.SecurityTests/UpdateProductSecurityTests.cs` | Pruebas nuevas. |

## Orden de implementación

DEV-API014-01 decide payload, Admin y stock con Orders; DEV-API014-02 implementa PATCH autorizado, DEV-API014-03 añade escritura condicional/coherencia; DEV-API014-04 verifica propiedad y fallos. La interfaz stock para Orders se acuerda con su dueño, no se implementa Orders aquí.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| TASK-CATALOG-002, Sprint 2 | DEV-API014-01/02/03/04 | Acta campos, actor, HTTP, CAS e invalidación. |
| API-013/009, Orders API-023 | DEV-API014-02/03 | Modelo producto, vendedor propio y política reserva/snapshot aprobada. |

## Estrategia de pruebas

Unitarias PATCH parcial/null, precio/stock; integración WebApplicationFactory + Mongo/Redis aislados: dos vendedores, versión concurrente, stock con interfaz Orders simulada; seguridad Buyer/ajeno, cambio sellerId/status/media, JWT revocado y Redis caído. Build Release, format y tres `dotnet test` proyectos `-c Release --no-build` con evidencia fechada; no tocar servicios productivos.

## Seguridad, errores y observabilidad

Autorizar por `_id` y `sellerId`, no solo rol; rate limit fail-closed, caché fail-open; `correlationId` y logs sin PII/token. Sin cambio de middleware.

## Criterios de cierre técnico

UPD-01..04 probados con persistencia local real, propiedad y conflicto sin pérdida; contrato Orders explícito, build/formato/suites verdes, sin precio alterado de pedidos existentes.
