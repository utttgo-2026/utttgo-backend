# API-031 — Listar reseñas de vendedor · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Reviews propietario de lista/ratings, Sellers del perfil, Catalog de productos; ninguna colección se comparte.

## Arquitectura existente

No hay Reviews ni pruebas. Reutilizar `ReviewDocument`/`MongoReviewRepository` previstos API-030; contrato Sellers para visibilidad solo si se aprueba. Reutilizar paginación/error/rate limit Bloque 1, no copiar repositorios de API-008/011.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Reviews/Controllers/ReviewsController.cs`, `Contracts/SellerReviewsQuery.cs`, `Contracts/SellerReviewsPageResponse.cs`, `Validators/SellerReviewsQueryValidator.cs` | GET sellerId paginado; DTO sin documentos. |
| Aplicación | `src/UTTTGO.Api/Modules/Reviews/Application/ListSellerReviewsService.cs` | Visibilidad vendedor según acta, proyectar solo reseñas permitidas. |
| Datos | `src/UTTTGO.Api/Modules/Reviews/Infrastructure/MongoReviewRepository.cs`, `ReviewMongoIndexes.cs` | Filtrar sellerId, ordenar por clave estable, paginar/indexar. |
| Interfaz | Reviews contrato de métricas para Sellers/Catalog | Ratings reales derivados, no copia de colección/valores inventados. |

## Secuencia técnica

Validar ID/page/pageSize → política de autenticación aprobada y rate limit Redis → obtener estado vendedor por interfaz Sellers si requerido → filtrar reseñas en Reviews por sellerId y visibilidad aprobada antes de ordenar/paginar → proyectar DTO mínimo; devolver página/total solo si contrato ratificado. Sin reseñas, no inventar promedio; si Redis falla, rechazar según política general fail-closed; si Mongo falla, error único.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| LIST-01..03 | `Modules/Reviews/Controllers/ReviewsController.cs`, `Contracts/SellerReviewsQuery.cs`, `Contracts/SellerReviewsPageResponse.cs`, `Validators/SellerReviewsQueryValidator.cs`, `Application/ListSellerReviewsService.cs`, `src/UTTTGO.Api/Program.cs` | GET/DTO/DI tras acta, middleware intacto. |
| LIST-01/04 | `Modules/Reviews/Infrastructure/MongoReviewRepository.cs`, `ReviewMongoIndexes.cs` | Reusar API-030; filtro sellerId e índice/orden según acta. |
| LIST-01..04 | `tests/UtttGo.UnitTests/ListSellerReviewsServiceTests.cs`, `tests/UtttGo.IntegrationTests/ListSellerReviewsEndpointTests.cs`, `tests/UtttGo.SecurityTests/ListSellerReviewsSecurityTests.cs` | Tests locales nuevos. |

## Orden de implementación

DEV-API031-01 aprueba acceso/DTO/orden/ratings; DEV-API031-02 GET/paginación; DEV-API031-03 agrega contrato de métricas propio Reviews si aprobado; DEV-API031-04 prueba privacidad/degradación. Revisar modelo API-030 antes de crear índices, no duplicar escritor.

## Dependencias y bloqueos

| Gate transversal o decisión particular | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| API-031 fase 08 sin spec, Excel pendiente | DEV-API031-01..04 | Acta acceso, DTO, paginación, métricas y estado vendedor. |
| API-030, API-008/011/012 | DEV-API031-02/03/04 | Repositorio Reviews y contratos Seller/Catalog aprobados, sin lectura inversa. |
| DEC-003 | DEV-API031-04 solo escenario cuenta eliminada | Política compartidos aprobada. |

## Estrategia de pruebas

Unitarias ID/paginación/DTO; WebApplicationFactory Mongo/Redis locales con dos vendedores y 25+ reseñas, orden estable, vacío e inactivo según acta; seguridad data leak, auth según acceso, rate limit/Redis/Mongo caídos; métricas sin false positives. Tras aprobación: build Release, format y tres suites `dotnet test -c Release --no-build`, evidencia fechada; no producción.

## Seguridad, errores y observabilidad

Rate limit distribuido fail-closed; JWT/blacklist si acceso autenticado. No PII/pedido propio ni texto sensible en logs, `correlationId` en error; no alteración middleware.

## Criterios de cierre técnico

LIST-01..04 probados sin mezclar vendedores ni inventar ratings, paginación máxima configurada, build/formato/suites verdes; decisiones privacidad documentadas aparte.
