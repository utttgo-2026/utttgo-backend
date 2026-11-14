# API-034 — Quitar producto favorito · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Favorites modifica solo vínculo usuario-producto propio; Catalog no se borra.

## Arquitectura existente

Sin módulo Favorites ni pruebas; reusar colección/escritor e índice único de API-033 y controlador compartido API-032, no crear segunda colección/repo.

## Componentes involucrados

| Capa | Archivo previsto | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Favorites/Controllers/FavoritesController.cs`, `Validators/FavoriteProductIdValidator.cs` | DELETE ruta/ID/body aprobados. |
| Aplicación | `src/UTTTGO.Api/Modules/Favorites/Application/RemoveFavoriteService.cs` | Identidad servidor, semántica de ausente/retry. |
| Datos | `src/UTTTGO.Api/Modules/Favorites/Infrastructure/MongoFavoriteRepository.cs` | DeleteOne filtrado por userId y productId. |

## Secuencia técnica

JWT/blacklist → rate limit Redis → validar productId → derivar userId → DELETE filtrado en Favorites → invalidar caché personal si existe/aprobada → responder según acta. No necesitar lectura cross-module salvo decisión expresa; si Mongo falla no afirmar borrado. Definir carrera con PUT según orden Mongo y política idempotencia.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Ruta/componente | Cambio condicionado |
|---|---|---|
| FAVR-01/03/04 | `Modules/Favorites/Controllers/FavoritesController.cs`, `Validators/FavoriteProductIdValidator.cs`, `Application/RemoveFavoriteService.cs`, `src/UTTTGO.Api/Program.cs` | DELETE/DI tras acta, middleware intacto. |
| FAVR-01/02 | `Modules/Favorites/Infrastructure/MongoFavoriteRepository.cs` | Reusar API-033, filtro doble/idempotencia aprobados. |
| FAVR-01..04 | `tests/UtttGo.UnitTests/RemoveFavoriteServiceTests.cs`, `tests/UtttGo.IntegrationTests/RemoveFavoriteEndpointTests.cs`, `tests/UtttGo.SecurityTests/RemoveFavoriteSecurityTests.cs` | Tests nuevos locales. |

## Orden de implementación

DEV-API034-01 ratifica ausente/retirado; DEV-API034-02 guard/controlador; DEV-API034-03 borrado filtrado/idempotencia; DEV-API034-04 pruebas falla/concurrencia; reusar código API-033 cuando exista.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| API-034 fase 09 | DEV-API034-01..04 | Acta HTTP, ID, repetición/retirado y carrera. |
| API-033 | DEV-API034-02/03/04 | Modelo e índice compartidos; no reimplementarlos. |

## Estrategia de pruebas

Unit ausente/repetido; WebApplicationFactory Mongo/Redis locales con dos usuarios/PUT-DELETE concurrentes, producto retirado con doble si necesario; seguridad revocado/Redis caído. Tras acta build Release, format, tres suites `dotnet test -c Release --no-build` con evidencia fechada, sin producción.

## Seguridad, errores y observabilidad

Filtro userId+productId, JWT/Redis fail-closed, `correlationId` en error, logs sin PII/token; no alterar middleware.

## Criterios de cierre técnico

FAVR-01..04 probados, favorito de otro usuario y producto intactos, build/formato/suites verdes.
