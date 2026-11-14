# API-033 — Marcar producto favorito · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Favorites escribe solo su colección; Catalog valida producto, no lo modifica.

## Arquitectura existente

No hay Favorites ni Catalog Sprint 2 ni tests relacionados. Compartir documento/escritor de API-032/034 cuando existan; no crear un repositorio para `products` aquí.

## Componentes involucrados

| Capa | Componente previsto | Función |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Favorites/Controllers/FavoritesController.cs`, `Validators/FavoriteProductIdValidator.cs` | PUT, validar ruta/body conforme acta. |
| Aplicación | `src/UTTTGO.Api/Modules/Favorites/Application/AddFavoriteService.cs` | Actor propio y elegibilidad mediante contrato Catalog. |
| Datos | `src/UTTTGO.Api/Modules/Favorites/Infrastructure/FavoriteDocument.cs`, `MongoFavoriteRepository.cs`, `FavoriteMongoIndexes.cs` | Par usuario-producto e índice único candidato. |

## Secuencia técnica

JWT/blacklist y rate limit Redis → validar productId → leer estado visible en Catalog vía interfaz aprobada → upsert atómico filtrado por userId+productId y gestión de carrera/duplicado según acta → respuesta. Catalog indisponible impide crear sin comprobación; fallo Mongo no altera Catalog. No se reserva stock ni se edita publicación.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Escenario | Ruta/componente | Cambio condicionado |
|---|---|---|
| FAVA-01/03/04 | `Modules/Favorites/Controllers/FavoritesController.cs`, `Validators/FavoriteProductIdValidator.cs`, `Application/AddFavoriteService.cs`, `src/UTTTGO.Api/Program.cs` | PUT/guard/DI sin cambiar middleware tras acta. |
| FAVA-01/02 | `Modules/Favorites/Infrastructure/FavoriteDocument.cs`, `MongoFavoriteRepository.cs`, `FavoriteMongoIndexes.cs` | Modelo/upsert e índice tras política duplicados. |
| FAVA-01..04 | `tests/UtttGo.UnitTests/AddFavoriteServiceTests.cs`, `tests/UtttGo.IntegrationTests/AddFavoriteEndpointTests.cs`, `tests/UtttGo.SecurityTests/AddFavoriteSecurityTests.cs` | Tests nuevos. |

## Orden de implementación

DEV-API033-01 contrato/elegibilidad; DEV-API033-02 API/servicio; DEV-API033-03 persistencia única; DEV-API033-04 carrera y seguridad. Reusar repositorio si API-032/034 se adelantaron.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| API-033 fase 09 | DEV-API033-01..04 | Acta de semántica PUT, duplicados, HTTP. |
| Catalog Sprint 2/API-011/012/015 | DEV-API033-02/04 | Lector de existencia/visibilidad publicado, no acceso directo. |

## Estrategia de pruebas

Unit caso elegible/inexistente, identidad; WebApplicationFactory Mongo/Redis locales y Catalog doble para dos PUT concurrentes, estado retirado/timeout; seguridad revocado/Redis caído y ausencia de userId ajeno. Tras acta: build Release, format y tres suites `dotnet test -c Release --no-build`; no producción.

## Seguridad, errores y observabilidad

UserId servidor, JWT/blacklist/rate limit fail-closed, errores `correlationId` sin fuga de producto oculto; logs sin datos personales; middleware intacto.

## Criterios de cierre técnico

FAVA-01..04 probados localmente con unicidad si aprobada, sin mutación Catalog, build/formato/suites verdes.
