# API-041 — Moderar reseña · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Cambio de estado de moderación de una reseña mediante contrato del dueño de Reviews; sin crear/editar reseñas, sin moderar imágenes y sin leer colecciones ajenas.

## Arquitectura existente

No existe módulo `Reviews` en `src/UTTTGO.Api/Modules/` (solo Admin, Identity, Sellers). Las fichas [API-030](../api-030-create-review/spec.md) y [API-031](../api-031-list-seller-reviews/spec.md) son borrador sin código y la moderación de texto no está aprobada. `Admin` ya conoce la autorización de lectura (API-039); la mutación exigirá un contrato nuevo de Reviews y, si aplica, extender la auditoría existente.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Admin/Controllers/AdminReviewModerationController.cs` (previsto) | `PATCH /api/v1/admin/reviews/{reviewId}/moderation`. |
| Aplicación | `src/UTTTGO.Api/Modules/Admin/Application/ReviewModerationService.cs` (previsto) | Validar destino, orquestar escritura y auditoría. |
| Contrato | `IReviewModerationWriter` publicado por Reviews (previsto) | Transiciones detrás del dueño; sin colección cruzada. |
| Reutilización | [AdminUsersController.cs](../../../src/UTTTGO.Api/Modules/Admin/Controllers/AdminUsersController.cs), [SessionService.cs](../../../src/UTTTGO.Api/Modules/Identity/Application/SessionService.cs) | Autorización y tasa del flujo Admin, sin duplicar. |
| Auditoría | [UnconfiguredAdminUsersDependencies.cs](../../../src/UTTTGO.Api/Modules/Admin/Infrastructure/UnconfiguredAdminUsersDependencies.cs) | Mismo patrón fail-closed para mutaciones. |

## Secuencia técnica

Controller → validador de cuerpo → `ISessionService` (JWT/blacklist/perfil `Active`) → rol local `Admin` → gate de rate limit → resolver `{reviewId}` y transición mediante contrato de Reviews → auditoría obligatoria → `200`. Candidato hasta fuente: estados, códigos ante inexistente/transición inválida y efecto en listados (API-031). Dependencias caídas ⇒ `503` sin cambio parcial.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos/errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): aplicar sin duplicar y sin alterar el middleware de `Program.cs`.

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| ADMR-01/02/04 | `Modules/Admin/Controllers/AdminReviewModerationController.cs`, `Modules/Admin/Application/ReviewModerationService.cs`, `src/UTTTGO.Api/Program.cs` | Solo tras fuente de estados y DTO; DI sin tocar middleware. |
| ADMR-01 | `Modules/Reviews/Infrastructure/…` e `IReviewModerationWriter.cs` (previstos, módulo Reviews) | Tras existir el módulo dueño con contrato aprobado. |
| ADMR-03/05 | Guardas en `ReviewModerationService.cs` y extensión de `UnconfiguredAdminUsersDependencies.cs` | Auditoría fail-closed y tasa de mutación. |
| ADMR-01..05 | `tests/UtttGo.UnitTests/ReviewModerationTests.cs`, `tests/UtttGo.IntegrationTests/ReviewModerationEndpointTests.cs`, `tests/UtttGo.SecurityTests/ReviewModerationSecurityTests.cs` | Pruebas nuevas locales; dobles solo en tests. |

## Orden de implementación

DEV-API041-01 ratifica estados, DTO y códigos desde la fuente; DEV-API041-02 crea el contrato de Reviews y el endpoint tras 01; DEV-API041-03 añade auditoría y guardas tras 02; DEV-API041-04 prueba los cinco escenarios tras 02/03. Sin código previo de Reviews que reutilizar.

## Dependencias y bloqueos

| Gate transversal o decisión particular (ID original) | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| Fuente de estados (fase-10 o fase Reviews) | DEV-API041-01..04 | Contrato de transiciones aprobado. |
| Moderación de texto sin aprobar (API-030) | DEV-API041-01/02 | Política de contenido y DTO ratificados. |
| Módulo Reviews con contrato dueño | DEV-API041-02/03 | Interfaz publicada sin colecciones cruzadas. |
| Coherencia con lectura (API-031) | DEV-API041-02 | Efecto de la moderación en listados definido. |

## Estrategia de pruebas

Unitarias del validador, transiciones, guardas de rol y auditoría; integración `WebApplicationFactory` de `200`, `404`, `400`/`409`, `401`/`403` y `503` sin cambio parcial; seguridad con JWT revocado, rol forjado, `reviewId` ajeno y rate limit. Dobles solo en tests; integración real posterior sin producción. Comandos: `dotnet test … -c Release --no-build` con evidencia fechada.

## Seguridad, errores y observabilidad

JWT/blacklist y rate limit distribuido fail-closed, autorización por recurso, auditoría sin PII ni contenido de reseñas en logs, sin acceso a colecciones de Reviews desde Admin, error único `correlationId`; middleware intacto.

## Criterios de cierre técnico

ADMR-01..05 con pruebas verdes de transición, rechazos, guardas y fallos; build Release, `dotnet format` y suites pertinentes en verde con evidencia fechada; fuente de estados aprobada por separado.
