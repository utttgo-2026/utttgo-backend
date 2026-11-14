# API-039 — Listar usuarios y vendedores · plan

Estado de aprobación de la ficha: PARCIAL

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Compilación atómica del trío [phase-02](../../phase-02-auth-users/api-039-list-users/spec.md) v0.2.0 sin sustituirlo; solo lectura paginada de cuentas. Suscribir y conservar los originales; no modificar código de otras rutas ni de API-040 en esta ficha.

## Arquitectura existente

El código real vive en `src/UTTTGO.Api/Modules/Admin/` (controlador, servicio, contrato, validador, gate de rate limit y dependencias sin configurar) y consume el contrato de lectura `IAdminUserListReader` publicado por `src/UTTTGO.Api/Modules/Identity/`, dueño actual de `users`; `ISessionService` aporta la frontera de autorización (JWT, blacklist, confirmación y perfil local). No existe módulo `Users` implementado; no mover colecciones ni exponer repositorios de Identity a Admin.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| API | [AdminUsersController.cs](../../../src/UTTTGO.Api/Modules/Admin/Controllers/AdminUsersController.cs) | `GET /api/v1/admin/users` con query acotada. |
| Aplicación | [AdminUsersService.cs](../../../src/UTTTGO.Api/Modules/Admin/Application/AdminUsersService.cs) | Orquesta sesión, tasa, lectura y auditoría previa al `200`. |
| Contrato | [AdminUsersResponse.cs](../../../src/UTTTGO.Api/Modules/Admin/Contracts/AdminUsersResponse.cs) | Página de cuatro campos y metadatos. |
| Validador | [AdminUsersQueryRequestValidator.cs](../../../src/UTTTGO.Api/Modules/Admin/Validators/AdminUsersQueryRequestValidator.cs) | Límites, enums, duplicados y overflow. |
| Tasa | [RedisAdminUsersRateLimitGate.cs](../../../src/UTTTGO.Api/Modules/Admin/Infrastructure/RedisAdminUsersRateLimitGate.cs) | Distribuido; `429`/`503` fail-closed. |
| Auditoría | [UnconfiguredAdminUsersDependencies.cs](../../../src/UTTTGO.Api/Modules/Admin/Infrastructure/UnconfiguredAdminUsersDependencies.cs) | Sink sin configurar: `503` si no registra. |
| Lectura | [IAdminUserListReader.cs](../../../src/UTTTGO.Api/Modules/Identity/Application/IAdminUserListReader.cs) | Contrato de Identity; sin repositorio expuesto. |
| Sesión | [SessionService.cs](../../../src/UTTTGO.Api/Modules/Identity/Application/SessionService.cs) | `401`/`403`/`503` por sesión y rol local. |

## Secuencia técnica

Controller → validador de query (rechaza antes de consultar) → `ISessionService` (JWT/blacklist/confirmación/perfil Active) → rol local `Admin` → gate de rate limit Redis → lectura mediante `IAdminUserListReader` → auditoría obligatoria → `200`. Si falla cualquier paso crítico: `400`/`401`/`403`/`429`/`503` con error único `correlationId`; la auditoría fallida prohíbe el `200`. Redis/Mongo/Supabase caídos ⇒ fail-closed.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos/errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): aplicar sin duplicar y sin alterar el middleware de `Program.cs`.

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| ADMU-01..05/07 | `Modules/Admin/Controllers/AdminUsersController.cs`, `Modules/Admin/Application/AdminUsersService.cs`, `Modules/Admin/Validators/AdminUsersQueryRequestValidator.cs` | Cierre local ya ejecutado; cambios posteriores requieren verificación del modelo (DEV-API039-01). |
| ADMU-06/08 | `Modules/Admin/Infrastructure/RedisAdminUsersRateLimitGate.cs`, `Modules/Admin/Infrastructure/UnconfiguredAdminUsersDependencies.cs` | Sustituir por adaptadores reales sin relajar fail-closed (DEV-API039-05). |
| ADMU-01..07 | `Modules/Identity/Application/IAdminUserListReader.cs` y futuro repositorio/índices en Identity | Solo detrás del contrato dueño; sin colecciones cruzadas (DEV-API039-06). |
| ADMU-01..08 | `tests/UtttGo.UnitTests/AdminUsersTests.cs`, `tests/UtttGo.IntegrationTests/AdminUsersEndpointTests.cs`, `tests/UtttGo.SecurityTests/AdminUsersSecurityTests.cs` | Suites locales existentes; extender solo con evidencia fechada. |

## Orden de implementación

DEV-API039-01 verifica el modelo real antes de índices; DEV-API039-02 (contrato de lectura) y DEV-API039-03 (endpoint) ya ejecutadas el 2026-11-04; DEV-API039-04 (QA con dobles) ya ejecutada; DEV-API039-05 requiere sink y Redis reales; DEV-API039-06 integra servicios no productivos al final. TASK-039-001 permanece gate de 05/06.

## Dependencias y bloqueos

| Gate transversal o decisión particular (ID original) | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| Verificación del modelo real (TASK-039-001) | DEV-API039-01, DEV-API039-05/06 | Confirmación de `Suspended`, `createdAt` e índices. |
| Sink de auditoría y Redis de tasa (ratificación 2026-11-04) | DEV-API039-05 | Adaptadores reales probados fail-closed. |
| Servicios no productivos (TASK-039-020) | DEV-API039-06 | JWT/Supabase/Redis/Mongo verificados sin secretos en Git. |
| AC-005 y TASK-AUTH-007/008 | DEV-API039-06 | Evidencia de API-040 y aprobación integral de fase. |

## Estrategia de pruebas

Unitarias del validador, autorización, proyección de cuatro campos, `total` filtrado y auditoría `503` ([AdminUsersTests.cs](../../../tests/UtttGo.UnitTests/AdminUsersTests.cs)); integración `WebApplicationFactory` de contrato JSON y errores con `correlationId` ([AdminUsersEndpointTests.cs](../../../tests/UtttGo.IntegrationTests/AdminUsersEndpointTests.cs)); seguridad de PII, rol forjado y degradación ([AdminUsersSecurityTests.cs](../../../tests/UtttGo.SecurityTests/AdminUsersSecurityTests.cs)). Dobles solo en tests; integración real posterior sin producción. Comandos: `dotnet test … -c Release --no-build` con evidencia en [qa-api-039](../../../docs/qa-api-039.md).

## Seguridad, errores y observabilidad

JWT/blacklist y rate limit distribuido fail-closed, rol solo del perfil local, auditoría sin PII previa al `200`, respuesta sin `email` ni datos de Sellers, error único `correlationId`, logs sin tokens ni búsqueda en bruto; middleware intacto.

## Criterios de cierre técnico

ADMU-01..08 con verificación real de `200`, `429`/`503`, auditoría persistente e índices medidos; build Release, `dotnet format` y suites en verde con evidencia fechada; AC-005 y TASK-AUTH-007/008 cerrados solo con sus gates.
