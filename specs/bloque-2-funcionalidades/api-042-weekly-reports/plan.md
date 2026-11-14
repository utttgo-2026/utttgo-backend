# API-042 — Reportes semanales administrativos · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Reporte semanal agregado para Admin consumiendo contratos de lectura de los módulos dueños; sin métricas propias en Admin, sin PII y sin colecciones cruzadas.

## Arquitectura existente

No existe módulo de reportes ni contratos de agregación; `Admin` solo implementa el lector de API-039 y `Orders`/`Reviews`/`Sellers` no publican métricas (las fichas de Orders son borrador sin código). La recolección debe partir de interfaces de lectura publicadas por cada dueño, con caché opcional en Redis conforme a [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md) sin guardar datos sensibles.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Admin/Controllers/AdminWeeklyReportController.cs` (previsto) | `GET /api/v1/admin/reports/weekly`. |
| Aplicación | `src/UTTTGO.Api/Modules/Admin/Application/WeeklyReportService.cs` (previsto) | Validar ventana, orquestar contratos y agregar. |
| Contratos | Interfaces de métricas de Orders/Reviews/Sellers (previstas en sus dueños) | Cada módulo expone sus agregados; Admin no consulta sus colecciones. |
| Reutilización | [SessionService.cs](../../../src/UTTTGO.Api/Modules/Identity/Application/SessionService.cs), [RedisAdminUsersRateLimitGate.cs](../../../src/UTTTGO.Api/Modules/Admin/Infrastructure/RedisAdminUsersRateLimitGate.cs) | Autorización y tasa existentes del flujo Admin. |

## Secuencia técnica

Controller → validador de parámetros de semana → `ISessionService` (JWT/blacklist/perfil `Active`) → rol local `Admin` → gate de rate limit → invocar contratos de métricas de cada módulo dueño → agregar sin PII → responder `200`. Candidato hasta fuente: ventana horaria, métricas y caché. Dependencia caída ⇒ `503` sin datos parciales; nunca servir agregados incompletos como completos.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos/errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): aplicar sin duplicar y sin alterar el middleware de `Program.cs`.

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| RPTW-01/03/05 | `Modules/Admin/Controllers/AdminWeeklyReportController.cs`, `Modules/Admin/Application/WeeklyReportService.cs`, `src/UTTTGO.Api/Program.cs` | Tras definir alcance, parámetros y métricas; DI sin tocar middleware. |
| RPTW-01/05 | Interfaces de métricas en `Modules/Orders/…`, `Modules/Reviews/…`, `Modules/Sellers/…` (previstas) | Solo en sus módulos dueños, con contratos aprobados. |
| RPTW-02/04 | Autorización y gate reutilizando [RedisAdminUsersRateLimitGate.cs](../../../src/UTTTGO.Api/Modules/Admin/Infrastructure/RedisAdminUsersRateLimitGate.cs) | Tasa de lectura administrativa; sin relajar límites. |
| RPTW-01..05 | `tests/UtttGo.UnitTests/WeeklyReportTests.cs`, `tests/UtttGo.IntegrationTests/WeeklyReportEndpointTests.cs`, `tests/UtttGo.SecurityTests/WeeklyReportSecurityTests.cs` | Pruebas nuevas locales; dobles solo en tests. |

## Orden de implementación

DEV-API042-01 ratifica alcance, ventana y métricas; DEV-API042-02 crea contratos de lectura en los dueños y el servicio de agregación tras 01; DEV-API042-03 asegura autorización y ausencia de PII tras 02; DEV-API042-04 prueba los cinco escenarios tras 02/03.

## Dependencias y bloqueos

| Gate transversal o decisión particular (ID original) | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| Alcance y métricas (fase-10 o fuente equivalente) | DEV-API042-01..04 | Catálogo de métricas, unidades y ventana aprobados. |
| Contratos de lectura de Orders/Reviews/Sellers | DEV-API042-02/03 | Interfaces publicadas por cada dueño. |
| Autorización de reportes | DEV-API042-03 | Política de acceso definida por seguridad. |
| Estado de fuentes de datos (fase 04 v0.1) | DEV-API042-02 | Datos de pedidos aprobados o sustitutos definidos. |

## Estrategia de pruebas

Unitarias de validación de ventana, agregación y exclusión de PII; integración `WebApplicationFactory` de `200`, `400`, `401`/`403` y `503` sin parciales; seguridad con JWT revocado, rol forjado, rate limit y búsqueda de PII en la respuesta. Dobles solo en tests; integración real posterior sin producción. Comandos: `dotnet test … -c Release --no-build` con evidencia fechada.

## Seguridad, errores y observabilidad

JWT/blacklist y rate limit distribuido fail-closed, rol local `Admin`, respuesta solo agregada sin PII, caché Redis sin datos sensibles, error único `correlationId`, logs sin payloads de reporte; middleware intacto.

## Criterios de cierre técnico

RPTW-01..05 con pruebas verdes de reporte, autorización, parámetros, fallos y no-PII; build Release, `dotnet format` y suites pertinentes en verde con evidencia fechada; alcance de métricas aprobado por separado.
