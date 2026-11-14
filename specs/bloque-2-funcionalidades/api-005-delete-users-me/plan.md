# API-005 — Eliminar cuenta propia · plan

Estado de aprobación de la ficha: PENDIENTE

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

Resolver DEC-003 (pedidos compartidos y en curso, revocación, idempotencia, fallo parcial, borrado físico/tiempos) → validar sujeto con gate Identity → consultar contratos dueños para inventario/ejecución → coordinar borrado recuperable → revocar identidad/sesión conforme política → confirmar solo cuando se cumpla criterio aprobado. Si algún paso falla, no declarar éxito ni borrar datos ajenos; definir compensación/reintento antes de codificar.

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
| DEC-003, AC-007 | DEV-API005-01/02/03/04 | Acta privacidad/negocio de datos compartidos, pedidos activos y fallos. |
| Contratos de módulos dueños/API-003 | DEV-API005-03/04 | Interfaces versionadas y pruebas locales de borrado/revocación. |
| TASK-AUTH-007→008→010 | DEV-API005-02/03/04 | Aprobación equipo/delegación y ficha identificable. |

## Estrategia de pruebas

Unitarias sobre orden/fallos/reintento con dobles; integración WebApplicationFactory con Mongo/Redis aislados, interfaces de módulos dueños y Supabase simulado; seguridad contra token revocado, sujeto ajeno y fallo Redis. Verificar persistencia real solo en no-producción autorizado; `dotnet build UTTTGO.Backend.slnx -c Release --no-restore`, `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore`, tres proyectos `dotnet test` Release `--no-build`.

## Seguridad, errores y observabilidad

Autorización por identidad/propiedad en cada módulo; error único `correlationId`, no log de PII/tokens; Redis fail-closed. Sin eliminación lógica sustituta ni mutación de middleware.

## Criterios de cierre técnico

DEL-01..04 con pruebas fechadas de comprador, vendedor, compartidos y fallo parcial; identidad y propios no accesibles, ajenos intactos, build/formato/suites verdes. AC-007 se cierra por evidencia separada.
