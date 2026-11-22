# API-005 — Eliminar cuenta propia · plan

Estado de aprobación de la ficha: APROBADA

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Dueño coordinador Identity; cada módulo borra solo sus propios datos después de resolver DEC-003.

## Arquitectura existente

`src/UTTTGO.Api/Modules/Identity/Controllers/UsersController.cs` y guard de API-006 son reutilizables; no hay DELETE ni flujo de eliminación. `users` pertenece a Identity; `sellers` a Sellers; Catalog/Media/Orders/Chat conservan propiedad de sus datos.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad |
|---|---|---|
| Controller | `src/UTTTGO.Api/Modules/Identity/Controllers/UsersController.cs` | DELETE `/me`, respuesta aprobada. |
| Aplicación | `src/UTTTGO.Api/Modules/Identity/Application/DeleteOwnAccountService.cs` (nuevo) | Coordinar solo contratos publicados por dueños y reintentos. |
| Datos | `src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoUserRepositories.cs`, `SupabaseAuthClient.cs` | Borrar perfil/identidad en orden aprobado; sin accesos a colecciones ajenas. |
| Contratos de otros módulos | Interfaces futuras de Sellers/Catalog/Media/Orders/Chat | Borrar recursos propios o preservar compartidos según acta; módulos dueños implementan interfaces. |

## Secuencia técnica

DEV-API005-01 queda aprobado el 2026-11-22 con baja lógica `Deleted`, anonimización de perfil, `deletedUserRef` UUID, bloqueo por pedidos activos, productos del vendedor no disponibles, revocación Redis compartida con API-003 y logout global de Supabase best-effort. La secuencia implementada es: validar sujeto con gate Identity → consultar pedidos activos → anonimizar en Mongo con CAS → pausar productos mediante Catalog → revocar sesión en Redis → llamar logout global de Supabase → confirmar `204`. Cada paso local es idempotente; un reintento con perfil `Deleted` continúa los pasos pendientes. Mongo o Redis fallidos devuelven `503` fail-closed.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md) y [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Sin repositorios cross-module.

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Ruta o componente | Cambio condicionado |
|---|---|---|
| DEL-01/04 | `src/UTTTGO.Api/Modules/Identity/Controllers/UsersController.cs`, `Application/DeleteOwnAccountService.cs` (nuevo), `Infrastructure/MongoUserRepositories.cs` | Orquestación/guard/borrado Identity solo tras acta. |
| DEL-02/03 | Contratos publicados por `Modules/Sellers`, `Catalog`, `Media`, `Orders`, `Chat` | Dependencia exigida, implementación en fichas dueñas; no escribir sus colecciones aquí. |
| DEL-01..04 | `tests/UtttGo.UnitTests/DeleteOwnAccountServiceTests.cs`, `tests/UtttGo.IntegrationTests/DeleteOwnAccountEndpointTests.cs`, `tests/UtttGo.SecurityTests/DeleteOwnAccountSecurityTests.cs` | Pruebas nuevas tras contrato. |

## Orden de implementación

DEV-API005-01 decide privacidad/orden y contrato; DEV-API005-02 crea coordinación Identity y DELETE con interfaces inyectadas; DEV-API005-03 integra consumidores cuando dueños entreguen contratos; DEV-API005-04 prueba consistencia, idempotencia y seguridad. No atribuir a API-005 la implementación interna de Orders/Catalog.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| DEC-003, AC-007 | DEV-API005-01/02/03/04 | DEV-API005-01 aprobado el 2026-11-22; contratos mínimos Orders/Catalog publicados y evidencia de contenedores pendiente. |
| Contratos de módulos dueños/API-003 | DEV-API005-03/04 | `IActiveOrderReader` e `IAccountDeletionCatalog` publicados; pruebas aisladas pendientes. |
| TASK-AUTH-007→008→010 | DEV-API005-02/03/04 | Se usa el gate JWT/Redis existente; no se usa service_role ni Admin API. |

## Estrategia de pruebas

Unitarias sobre orden/fallos/reintento con dobles; integración WebApplicationFactory con Mongo/Redis aislados, interfaces de módulos dueños y Supabase simulado; seguridad contra token revocado, sujeto ajeno y fallo Redis. Verificar persistencia real solo en no-producción autorizado; `dotnet build UTTTGO.Backend.slnx -c Release --no-restore`, `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore`, tres proyectos `dotnet test` Release `--no-build`.

## Seguridad, errores y observabilidad

Autorización por identidad/propiedad en cada módulo; error único `correlationId`, no log de PII/tokens; Redis fail-closed. Sin eliminación lógica sustituta ni mutación de middleware.

## Criterios de cierre técnico

DEL-01..04 con pruebas fechadas de comprador, vendedor, compartidos y fallo parcial; identidad y propios no accesibles, ajenos intactos, build/formato/suites verdes. AC-007 se cierra por evidencia separada.
