# API-004 — Sesión propia · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → este plan → [task](task.md). Solo `GET /api/v1/auth/me`; no trasladar contratos de API-001/002 ni implementar OTP, login, refresh, logout o recuperación. Implementación actual candidata; acciones técnicas condicionadas a decisiones/gates, no a que exista código.

## Arquitectura existente

Identity contiene controlador, `SessionService`, DTO, lector JWT, gate Redis, cliente Supabase y repositorio Mongo conectados en [`Program.cs`](../../../src/UTTTGO.Api/Program.cs). El parser Bearer vive en Shared. API-006/007 reutilizan `AuthenticateAsync`, pero sus DTO y reglas de edición no pertenecen a API-004. No crear segundo repositorio de `users` ni persistir sesión en memoria.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| HTTP/contratos | [`AuthController`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs), [`BearerTokenReader`](../../../src/UTTTGO.Api/Shared/Contracts/BearerTokenReader.cs), [`SessionResponse`](../../../src/UTTTGO.Api/Modules/Identity/Contracts/SessionResponse.cs) | Bearer obligatorio, respuesta local y errores candidatos; SES-01/02/04. |
| Aplicación | [`SessionService`](../../../src/UTTTGO.Api/Modules/Identity/Application/SessionService.cs), [`ISessionDependencies`](../../../src/UTTTGO.Api/Modules/Identity/Application/ISessionDependencies.cs) | Guard de estado y correspondencia de identidades; SES-01/03/05. |
| Adaptadores | [`ValidatedJwtSubjectReader`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/ValidatedJwtSubjectReader.cs), [`RedisJwtGate`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisJwtGate.cs) | Validación de firma/issuer/audience/exp y consulta de revocación; SES-02/05. |
| Proveedor y datos | [`SupabaseAuthClient`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/SupabaseAuthClient.cs), [`MongoUserRepositories`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoUserRepositories.cs) | Confirmación actual y perfil `users` por sujeto; SES-03/04/05. |

## Secuencia técnica

Secuencia **candidata existente**: leer Bearer único → extraer `sub` sin confiar en él hasta validar por gate JWT/Redis → volver a comprobar gate por vínculo token/sujeto → consultar `/auth/v1/user` a Supabase (incluye validación del sujeto) → leer `users` Mongo por `supabaseUserId` → exigir `Active`, email/sujeto coherentes y roles locales → proyectar `SessionResponse`. Falta de Bearer/identidad no válida → `401`; perfil inexistente después de validación → `404` candidato; dependencia crítica no disponible → `503` sin habilitar acceso. No hay promoción de pendiente ni escritura. Consultas reales están cableadas en DI, pero las suites históricas usan dobles; no afirmar prueba de fallos reales.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos/errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Aplicar fronteras Identity, error con `correlationId`, lectura de roles desde Mongo y revocación fail-closed; las fichas del Bloque 1 permanecen pendientes de aprobación propia.

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| SES-01/04 | [`AuthController.cs`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs), [`SessionResponse.cs`](../../../src/UTTTGO.Api/Modules/Identity/Contracts/SessionResponse.cs) | **Ninguno ahora**. Solo ajustar si acta DF02-07 ratifica diferencia concreta entre contrato y candidato; no reimplementar ruta vigente. |
| SES-02/05 | [`RedisJwtGate.cs`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisJwtGate.cs) | **Ninguno ahora**. Eventual cambio sobre JWT sin `jti` solo tras decisión DF02-04 y aprobación SDD; no relajar fail-closed. |
| SES-03/05 | [`SessionService.cs`](../../../src/UTTTGO.Api/Modules/Identity/Application/SessionService.cs), [`SupabaseAuthClient.cs`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/SupabaseAuthClient.cs) | **Ninguno ahora**. Diferencias en frecuencia/confirmación solo tras DF02-09; no introducir activación en API-004. |
| SES-01/02/03/04/05 | [`SessionServiceTests.cs`](../../../tests/UtttGo.UnitTests/SessionServiceTests.cs), [`SessionEndpointTests.cs`](../../../tests/UtttGo.IntegrationTests/SessionEndpointTests.cs), [`SessionSecurityTests.cs`](../../../tests/UtttGo.SecurityTests/SessionSecurityTests.cs) | Añadir evidencia de adaptadores reales en entorno aislado **solo** después de autorización; reutilizar pruebas con dobles existentes. |

## Orden de implementación

DEV-API004-01 (inventario documental) → DEV-API004-02 (acta del contrato) → DEV-API004-03 (verificación técnica aislada). No se planifica reescritura del código presente: si la aprobación revela una diferencia concreta, primero actualizar spec/plan/task y crear tarea específica autorizada; nunca convertir automáticamente DEV-API004-03 en refactor. La ejecución de DEV-API004-03 también requiere los gates transversales aunque DEV-API004-02 termine.

## Dependencias y bloqueos

| Gate transversal o decisión particular (ID original) | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| DF02-07; TASK-AUTH-010/022 | DEV-API004-02/03 | Acta de equipo por API-004: HTTP/DTO, perfil ausente, antienumeración, cotejo Excel por fuente autorizada. |
| DF02-04; REQ-007/008 | DEV-API004-03 | Acta Identity/seguridad sobre `jti` obligatorio y comportamiento sin él, conforme constitución; API-003 conserva escritor de revocación. |
| DF02-01/09; TASK-AUTH-012/021 | DEV-API004-03 | Diseño ratificado de confirmación, transición/reparación y pruebas de fallo parcial sin acceso pendiente. |
| DF02-08; TASK-AUTH-007→008→010 | DEV-API004-03 | Aprobación colectiva/delegación y cadena SDD cerrada; autorización explícita para entorno no productivo. |

## Estrategia de pruebas

[QA histórico](../../../docs/qa-api-004.md) (2026-11-04): +13 unitarias, +12 integración WebApplicationFactory y +8 seguridad con dobles; no ejecutar contra AWS/Atlas/Supabase productivos. Tras autorización, preparar entorno local/no productivo con JWT firmados de prueba, Redis y Mongo aislados y proveedor Supabase simulado/controlado; probar firma, issuer, audience, expiración, `jti` revocado, caída Redis, identidad confirmada/no confirmada, estados `Active`/pendiente/suspendido/eliminado, perfil inexistente y caída Supabase/Mongo. Registrar comandos `dotnet restore UTTTGO.Backend.slnx`, `dotnet build UTTTGO.Backend.slnx -c Release --no-restore`, `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore`, suites unit/integration/security de AGENTS.md y salida fechada **solo cuando se ejecuten**. No declarar suites actuales verdes por QA pasado.

## Seguridad, errores y observabilidad

No confiar en `sub` antes de gate criptográfico, ni en claims de rol; comprobar coincidencia Supabase/Mongo y `Active`. Redis/Supabase/Mongo inaccesibles niegan acceso, sin fallback a token sin verificar. Respuesta candidata `401/404/503` mediante `ApiError` con `correlationId`; revisar `404` por enumeración. No loguear tokens, `jti`, SAS ni datos personales sensibles. No cambiar orden de middleware sin aprobación de AGENT-BE-01; [contratos compartidos](../../bloque-1-arquitectura/contratos-transversales/spec.md).

## Criterios de cierre técnico

Por SES-01..05: acta y gate correspondiente, prueba fechada con entorno y límite, comandos Release/formato/suites pertinentes y resultados sanitizados; no contar dobles como JWT/Redis reales. Tarea histórica TASK-AUTH-022 y contratos del Bloque 1 requieren su propio cierre documental. Esta ficha queda BLOCKED para cambios técnicos mientras falten decisiones; un `200` atribuido en AWS no reemplaza pruebas de ramas negativas.
