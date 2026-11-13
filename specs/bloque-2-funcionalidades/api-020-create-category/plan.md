# API-020 — Crear categoría · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Catalog es dueño de `categories`; Identity resuelve rol Admin mediante contrato, sin acceso cruzado a `users`.

## Arquitectura existente

No existe módulo Catalog ni tests categoría. Reutilizar gate JWT/Redis/lector de perfil local Identity, contrato de errores y `Program.cs` para DI; API-019 compartirá modelo/repo, API-021 actualizará por ese repo.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Catalog/Controllers/AdminCategoriesController.cs`, `Contracts/CreateCategoryRequest.cs`, `Contracts/CategoryResponse.cs`, `Validators/CreateCategoryRequestValidator.cs` | POST/DTO/whitelist y error. |
| Aplicación | `src/UTTTGO.Api/Modules/Catalog/Application/CreateCategoryService.cs` | Verificar Admin vía Identity, generar identidad/slug según acta. |
| Datos | `src/UTTTGO.Api/Modules/Catalog/Infrastructure/CategoryDocument.cs`, `MongoCategoryRepository.cs`, `CatalogCategoryIndexes.cs` | `categories` de Catalog, unicidad solo tras contrato e índices de consultas. |

## Secuencia técnica

Validar JWT/blacklist, rol local Admin y rate limit → validar body/name/slug/orden según acta → normalizar con algoritmo aprobado → insertar categoría con ID y UTC en Mongo de Catalog (índice único solo si ratificado) → invalidar caché de API-019 según protocolo → devolver DTO; no tocar products ni asignar roles.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivo | Cambio condicionado |
|---|---|---|
| NEW-01/02 | `Modules/Catalog/Controllers/AdminCategoriesController.cs`, `Contracts/CreateCategoryRequest.cs`, `Contracts/CategoryResponse.cs`, `Validators/CreateCategoryRequestValidator.cs`, `Application/CreateCategoryService.cs` | POST autorizado tras acta. |
| NEW-01/03/04 | `Modules/Catalog/Infrastructure/CategoryDocument.cs`, `MongoCategoryRepository.cs`, `CatalogCategoryIndexes.cs`, `src/UTTTGO.Api/Program.cs` | Insert, índices justificados, DI sin alterar middleware. |
| NEW-01..04 | `tests/UtttGo.UnitTests/CreateCategoryServiceTests.cs`, `tests/UtttGo.IntegrationTests/CreateCategoryEndpointTests.cs`, `tests/UtttGo.SecurityTests/CreateCategorySecurityTests.cs` | Pruebas nuevas. |

## Orden de implementación

DEV-API020-01 ratifica contrato común categorías; DEV-API020-02 crea POST/validación/guard; DEV-API020-03 crea repositorio/índices; DEV-API020-04 prueba duplicados/rol/Redis. API-019 consume reader, no duplicar colección.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| Sprint 2, categorías API-019..022 | DEV-API020-01/02/03/04 | Acta DTO/slug/orden/HTTP/índices/auditoría. |
| Identity y API-019 | DEV-API020-02/04 | Interfaz perfil local Admin y contrato invalidación lectura. |

## Estrategia de pruebas

Unitarias normalización/whitelist, integración WebApplicationFactory Mongo/Redis local con alta/duplicado/carrera y GET API-019; seguridad JWT faltante/revocado, Buyer, campos privilegiados, 429/503. Build Release, format, tres proyectos `dotnet test -c Release --no-build` una vez autorizado; no producción.

## Seguridad, errores y observabilidad

Rol local Admin y JWT gate, rate limit fail-closed; `correlationId`, logs sin PII/tokens; cache de lectura fail-open sin confirmar éxito con escritura fallida. Sin cambio middleware.

## Criterios de cierre técnico

NEW-01..04 con evidencia fechada de categoría creada solo por Admin, duplicado según acta, lectura coherente, suites/build/formato verdes.
