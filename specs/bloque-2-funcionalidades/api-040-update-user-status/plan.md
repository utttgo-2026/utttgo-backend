# API-040 — Cambiar estado de cuenta de usuario · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Suspensión/reactivación administrativa en la ruta única de RF-14; sin cambio de rol, sin borrado ([API-005](../api-005-delete-users-me/spec.md)) ni listado ([API-039](../api-039-list-users/spec.md)).

## Arquitectura existente

`src/UTTTGO.Api/Modules/Admin/` solo implementa lectura (API-039). El dueño documental de `users` es `Identity` (`IAdminUserListReader`, `ISessionService`); no existe contrato de escritura administrativa publicado. La suspensión debe llegar desde Admin mediante un contrato de Identity, sin `IMongoCollection` cruzado ni lectura directa de su colección; el bloqueo del suspendido se deriva del perfil local ya consultado por sesión.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Admin/Controllers/AdminUsersStatusController.cs` (previsto) | `PATCH /api/v1/admin/users/{userId}/status`. |
| Aplicación | `src/UTTTGO.Api/Modules/Admin/Application/AdminUserStatusService.cs` (previsto) | Validar destino, orquestar escritura y auditoría. |
| Validador | `src/UTTTGO.Api/Modules/Admin/Validators/AdminUserStatusRequestValidator.cs` (previsto) | `accountStatus` de lista blanca y forma del cuerpo. |
| Contrato | `IAdminUserStatusWriter` publicado por Identity (previsto) | Escritura del estado detrás del dueño de `users`. |
| Reutilización | [AdminUsersController.cs](../../../src/UTTTGO.Api/Modules/Admin/Controllers/AdminUsersController.cs), [SessionService.cs](../../../src/UTTTGO.Api/Modules/Identity/Application/SessionService.cs) | Autorización y tasa del flujo de lectura, sin duplicar. |
| Auditoría | [UnconfiguredAdminUsersDependencies.cs](../../../src/UTTTGO.Api/Modules/Admin/Infrastructure/UnconfiguredAdminUsersDependencies.cs) | Extender a mutaciones con el mismo fail-closed. |

## Secuencia técnica

Controller → validador de cuerpo → `ISessionService` (JWT/blacklist/perfil `Active`) → rol local `Admin` y no-cuenta-propia → gate de rate limit → verificar `{userId}` y estado destino vía contrato de Identity → escribir estado con auditoría obligatoria → `200`. Candidato hasta acta: códigos ante inexistente/mismo estado, efecto sobre sesiones y orden transaccional. Cualquier dependencia caída ⇒ `503` sin cambio parcial.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos/errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): aplicar sin duplicar y sin alterar el middleware de `Program.cs`.

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| ADMS-01/04 | `Modules/Admin/Controllers/AdminUsersStatusController.cs`, `Modules/Admin/Application/AdminUserStatusService.cs`, `src/UTTTGO.Api/Program.cs` | Tras ratificar HTTP/DTO de RF-14; DI sin tocar middleware. |
| ADMS-01/06 | `Modules/Identity/Application/IAdminUserStatusWriter.cs` y repositorio de estado en Identity | Solo detrás del contrato dueño; verificar `Suspended` en el modelo real. |
| ADMS-02/03/05 | Guardas en `AdminUserStatusService.cs` y extensión de `UnconfiguredAdminUsersDependencies.cs` | Rol/cuenta propia T-AUTH-13 y auditoría fail-closed. |
| ADMS-01..06 | `tests/UtttGo.UnitTests/AdminUserStatusTests.cs`, `tests/UtttGo.IntegrationTests/AdminUserStatusEndpointTests.cs`, `tests/UtttGo.SecurityTests/AdminUserStatusSecurityTests.cs` | Pruebas nuevas locales; dobles solo en tests. |

## Orden de implementación

DEV-API040-01 ratifica DTO, códigos y efecto de `Suspended`; DEV-API040-02 crea contrato de escritura y endpoint tras 01; DEV-API040-03 añade guardas (rol, cuenta propia) y auditoría tras 02; DEV-API040-04 prueba los seis escenarios tras 03; DEV-API040-05 integra servicios no productivos para AC-005/AC-009 al final.

## Dependencias y bloqueos

| Gate transversal o decisión particular (ID original) | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| Ratificación de HTTP/DTO de RF-14 | DEV-API040-01/02/04 | Acta de contrato con códigos y cuerpo. |
| Efecto de `Suspended` (RF-08) | DEV-API040-01/03 | Diseño de bloqueo de sesiones/operaciones. |
| Contrato de escritura de Identity | DEV-API040-02/03 | Interfaz publicada sin colecciones cruzadas. |
| AC-005/AC-009 y T-AUTH-13 | DEV-API040-04/05 | Pruebas específicas y evidencia fechada. |
| DEC-003 (desactivación definitiva) | DEV-API040-01 | Política aparte; no ampliar API-040. |

## Estrategia de pruebas

Unitarias del validador, guardas de rol/cuenta propia, transición de estado y auditoría; integración `WebApplicationFactory` de `200`, rechazos `400`/`403`/`404`, `503` sin cambio parcial y bloqueo del suspendido; seguridad con JWT revocado, rol forjado, cuenta propia y rate limit (`T-AUTH-13`, `CL-16`). Dobles solo en tests; integración real posterior sin producción. Comandos: `dotnet test … -c Release --no-build` con evidencia fechada.

## Seguridad, errores y observabilidad

JWT/blacklist y rate limit distribuido fail-closed, autorización por recurso y no-cuenta-propia, auditoría de mutación sin PII, sin cambio de rol ni borrado, error único `correlationId`, logs con estado previo→nuevo sin datos personales; middleware intacto.

## Criterios de cierre técnico

ADMS-01..06 con pruebas verdes de mutación, guardas, bloqueo del suspendido y fallos; build Release, `dotnet format` y suites pertinentes en verde con evidencia fechada; AC-005/AC-009 cerrados solo con evidencia de esta ruta.
