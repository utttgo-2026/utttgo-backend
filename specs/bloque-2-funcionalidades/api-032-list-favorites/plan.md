# API-032 — Listar favoritos propios · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Favorites lee su colección; Catalog decide visibilidad/precio; Identity sujeto propio.

## Arquitectura existente

No existe módulo Favorites ni tests. Compartir repositorio/modelo con API-033/034 cuando se aprueben; usar contratos Catalog/Identity, no acceder a `products` o `users` directamente.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad/fuente |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Favorites/Controllers/FavoritesController.cs`, `Contracts/FavoritesPageResponse.cs`, `Validators/FavoritesQueryValidator.cs` | GET y validación de paginación ratificada. |
| Aplicación | `src/UTTTGO.Api/Modules/Favorites/Application/ListFavoritesService.cs` | Resolver usuario y proyección autorizada. |
| Datos | `src/UTTTGO.Api/Modules/Favorites/Infrastructure/MongoFavoriteRepository.cs`, `FavoriteMongoIndexes.cs` | Query por userId y orden estable; colección propia candidata `favorites`. |

## Secuencia técnica

JWT/blacklist → rate limit Redis → validar `page/pageSize` → resolver sujeto local → filtrar en Favorites por userId antes de skip/limit → enriquecer por contrato Catalog solo si aprobado y sin convertir retirados en comprables → proyectar DTO; Mongo/Redis caídos error único, fallo Catalog conforme acta, sin cache de PII.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Ruta/componente | Cambio condicionado |
|---|---|---|
| FAVL-01/03 | `Modules/Favorites/Controllers/FavoritesController.cs`, `Contracts/FavoritesPageResponse.cs`, `Validators/FavoritesQueryValidator.cs`, `Application/ListFavoritesService.cs`, `src/UTTTGO.Api/Program.cs` | GET/DI tras acta, middleware intacto. |
| FAVL-01/02 | `Modules/Favorites/Infrastructure/MongoFavoriteRepository.cs`, `FavoriteMongoIndexes.cs` | Reusar API-033; consulta indexada y proyección Catalog aprobada. |
| FAVL-01..04 | `tests/UtttGo.UnitTests/ListFavoritesServiceTests.cs`, `tests/UtttGo.IntegrationTests/ListFavoritesEndpointTests.cs`, `tests/UtttGo.SecurityTests/ListFavoritesSecurityTests.cs` | Tests nuevos locales. |

## Orden de implementación

DEV-API032-01 ratifica DTO/visibilidad; DEV-API032-02 lector filtrado; DEV-API032-03 integración Catalog opcional; DEV-API032-04 aislamiento/fallos. Verificar implementación de API-033 antes de crear repositorio.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| API-032 sin spec fase 09 | DEV-API032-01..04 | Acta DTO, paginación, estado producto, HTTP. |
| API-033/034 y Catalog Sprint 2 | DEV-API032-02/03/04 | Modelo Favorites y lector Catalog publicados. |
| API-005/BR-011 | DEV-API032-04 caso baja | Política de eliminación propia aprobada. |

## Estrategia de pruebas

Unit: paginación y proyección; integración WebApplicationFactory Mongo/Redis aislados con dos usuarios y Catalog doble; seguridad JWT revocado/otro userId/Redis caído. Tras acta: build Release, format, tres suites `dotnet test -c Release --no-build`, evidencia fechada; no producción.

## Seguridad, errores y observabilidad

Perfil local y filtro server-side, rate limit y blacklist fail-closed; `correlationId` en error, sin favoritos personales en logs ni middleware alterado.

## Criterios de cierre técnico

FAVL-01..04 en pruebas locales, listado estable sin terceros, reglas de producto según acta y suites/build/formato verdes.
