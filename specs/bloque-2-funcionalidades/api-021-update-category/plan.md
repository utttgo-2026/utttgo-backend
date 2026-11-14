# API-021 — Editar o reactivar categoría · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Catalog actualiza solo `categories`; Admin es actor autorizado por Identity, no módulo dueño de datos.

## Arquitectura existente

No existe Catalog ni tests categorías. Reutilizar `CategoryDocument`/`MongoCategoryRepository` proyectados por API-020, lector API-019, gate JWT/rol local de Identity, rate limit compartido. No acceder a `users` desde Catalog ni a `products` para cambiar categoría.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Catalog/Controllers/AdminCategoriesController.cs`, `Contracts/UpdateCategoryRequest.cs`, `Validators/UpdateCategoryRequestValidator.cs` | PATCH, whitelist, ID/HTTP según acta. |
| Aplicación | `src/UTTTGO.Api/Modules/Catalog/Application/UpdateCategoryService.cs` | Validar Admin, cambios/reacción de `isActive` y conflicto. |
| Datos | `src/UTTTGO.Api/Modules/Catalog/Infrastructure/MongoCategoryRepository.cs`, Redis read gate Catalog | Escritura condicional y cache invalidada sin mutar productos. |

## Secuencia técnica

JWT/blacklist y Admin local → rate limit → validar categoryId y body no privilegiado → leer categoría → aplicar cambios y reactivación solo por transición aprobada → CAS/índice de nombre/slug según acta → invalidar cache de API-019 y visibilidad de API-011/012 → DTO/error. Fallo Mongo/Redis o conflicto no confirma edición; Orders revalida categoría activa por contrato Catalog, no escribe aquí.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivo | Cambio condicionado |
|---|---|---|
| EDT-01/02/03 | `Modules/Catalog/Controllers/AdminCategoriesController.cs`, `Contracts/UpdateCategoryRequest.cs`, `Validators/UpdateCategoryRequestValidator.cs`, `Application/UpdateCategoryService.cs` | PATCH solo tras aprobación contrato. |
| EDT-02/04 | `Modules/Catalog/Infrastructure/MongoCategoryRepository.cs`, Redis read gate Catalog, `src/UTTTGO.Api/Program.cs` | CAS/invalidación y DI sin modificar middleware. |
| EDT-01..04 | `tests/UtttGo.UnitTests/UpdateCategoryServiceTests.cs`, `tests/UtttGo.IntegrationTests/UpdateCategoryEndpointTests.cs`, `tests/UtttGo.SecurityTests/UpdateCategorySecurityTests.cs` | Pruebas nuevas. |

## Orden de implementación

DEV-API021-01 aprueba campos/estado; DEV-API021-02 implementa PATCH y propiedad Admin sobre modelo API-020; DEV-API021-03 maneja concurrencia/reactivación/cache; DEV-API021-04 prueba rol/errores. No escribir productos desde el servicio.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| Sprint 2 categorías | DEV-API021-01/02/03/04 | Acta patch, slug/orden, transiciones, HTTP/CAS. |
| API-020/019/022 y Orders | DEV-API021-02/03/04 | Modelo compartido, lector/invalidez y elegibilidad de producto por contrato dueño. |

## Estrategia de pruebas

Unitarias PATCH parcial/invalidación y reactivación; integración WebApplicationFactory + Mongo/Redis aislados con dos Admin concurrentes, GET paginado tras edición y productos intactos; seguridad Buyer/JWT revocado, body privilegiado, 429/503. Una vez autorizado: build Release, format y tres `dotnet test` proyectos `-c Release --no-build`; no producción.

## Seguridad, errores y observabilidad

JWT validado, rol local Admin, whitelist y CAS; Redis fail-closed rate limit/cache lectura fail-open, error único `correlationId`, logs sin token/PII. Sin cambios middleware.

## Criterios de cierre técnico

EDT-01..04 con evidencia fechada, categoría coherente sin escrituras de productos, conflicto protegido, suites/build/formato verdes.
