# API-003 — Cerrar sesión · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Solo logout del JWT presentado; sesión Supabase/móvil es dependencia de decisión, no otro endpoint.

## Arquitectura existente

Dueño Identity. Reutilizar gate JWT de API-002/004 y `RedisJwtGate`; falta escritor Redis y acción en `AuthController`. No duplicar `users` ni repositorios de Sellers.

## Componentes involucrados

| Capa | Componente | Responsabilidad |
|---|---|---|
| Controller | `src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs` | Añadir POST logout después de aprobación. |
| Aplicación | `src/UTTTGO.Api/Modules/Identity/Application/LogoutService.cs` (nuevo) | Validar token, coordinar revocación e informar resultado. |
| Infraestructura | `src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisJwtGate.cs`, adaptador escritor nuevo | SET `revoked:{jti}` TTL hasta `exp` compartido. |
| Proveedor | `SupabaseAuthClient.cs` | Solo si acta exige revocación de refresh; no asumir interfaz. |

## Secuencia técnica

Resolver DF02-04 y contrato → validar Bearer/JWT con gate compartido → decidir comportamiento de token repetido → escribir revocación Redis con TTL → coordinar refresh si procede → responder solo tras éxito; ante caída devolver error sin afirmar logout. No registrar tokens.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md) y [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). El escritor pertenece a Identity; política de validación compartida, sin tocar orden de middleware.

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Ruta | Cambio condicionado |
|---|---|---|
| LOG-01 | `src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs`, `Application/LogoutService.cs`, `Infrastructure/RedisJwtGate.cs` | Acción, servicio/escritor con TTL y DI tras acta. |
| LOG-02/03 | `tests/UtttGo.UnitTests/LogoutServiceTests.cs`, `tests/UtttGo.IntegrationTests/LogoutEndpointTests.cs`, `tests/UtttGo.SecurityTests/LogoutSecurityTests.cs` | Nuevas pruebas; no existen actualmente. |

## Orden de implementación

DEV-API003-01 decide HTTP/refresh; DEV-API003-02 crea escritor y ruta; DEV-API003-03 verifica Redis y seguridad en contenedores no productivos; DEV-API003-04 implementa solo la coordinación de refresh que el acta seleccione. No crear alias.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| DF02-04, TASK-AUTH-003/025 | DEV-API003-01/02/04 | Acta Identity/móvil/seguridad con repetición, `jti` y refresh. |
| TASK-AUTH-007→008→010, DF02-08 | DEV-API003-02/03/04 | Contrato de ficha y gates colectivos identificables. |

## Estrategia de pruebas

Unitarias TTL hasta `exp`, repetición y excepciones; WebApplicationFactory + Redis aislado en integración para revocación compartida; seguridad JWT falso/expirado/sin `jti`, rate limit, fallo Redis y ausencia de secretos; Supabase simulado hasta acceso autorizado. Al ejecutar: `dotnet build UTTTGO.Backend.slnx -c Release --no-restore`, `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore` y tres proyectos `dotnet test` Release `--no-build`.

## Seguridad, errores y observabilidad

Contrato único con `correlationId`; Redis y auth fallan cerrado, sin JWT, `jti`, refresh ni PII en logs. No afirmar logout global por blacklist local.

## Criterios de cierre técnico

LOG-01..03 probados con evidencia fechada multi-réplica local y proveedor según acta; tests y formato verdes, ninguna credencial en diff; TASK-AUTH-025 se cierra aparte, no por redactar esta ficha.
