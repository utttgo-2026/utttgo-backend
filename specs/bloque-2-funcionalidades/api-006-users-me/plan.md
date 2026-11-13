# API-006 — Leer perfil propio · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → este plan → [task](task.md). Solo `GET /api/v1/users/me`; edición API-007, sesión API-004, OTP/login/logout y subida/moderación Media tienen dueños distintos. El Excel original no está en este worktree; cotejo directo pendiente de fuente.

## Arquitectura existente

[`UsersController`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/UsersController.cs) delega a [`OwnProfileService`](../../../src/UTTTGO.Api/Modules/Identity/Application/OwnProfileService.cs), que consume `ISessionService.AuthenticateAsync` de [API-004](../api-004-auth-me/spec.md). `MongoUserRepositories` pertenece a Identity y lee `users` por `supabaseUserId`; no hay consulta por ID de query. `Program.cs` registra adaptadores JWT/Redis/Supabase/Mongo reales, pero la lectura de avatar usa `UnconfiguredApprovedAvatarReadAccess` (siempre `null`); el módulo Media es dueño de aprobación/propiedad/URL. El GET no usa `UpdateOwnProfileRequestValidator`, exclusivo de PATCH API-007.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| HTTP/DTO | [`UsersController.cs`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/UsersController.cs), [`BearerTokenReader.cs`](../../../src/UTTTGO.Api/Shared/Contracts/BearerTokenReader.cs), [`OwnProfileResponse.cs`](../../../src/UTTTGO.Api/Modules/Identity/Contracts/OwnProfileResponse.cs) | GET Bearer, respuesta de seis campos distintos de API-004, `401/503` candidatos; PRF-01/02/03. |
| Aplicación | [`OwnProfileService.cs`](../../../src/UTTTGO.Api/Modules/Identity/Application/OwnProfileService.cs), [`SessionService.cs`](../../../src/UTTTGO.Api/Modules/Identity/Application/SessionService.cs) | Proyección local y guard de sesión compartido; PRF-01..06. |
| Auth/datos | [`RedisJwtGate.cs`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisJwtGate.cs), [`SupabaseAuthClient.cs`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/SupabaseAuthClient.cs), [`MongoUserRepositories.cs`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoUserRepositories.cs) | Firma/issuer/audience/exp y revocación, confirmación, lectura propia; PRF-02/06. |
| Media | [`UnconfiguredApprovedAvatarReadAccess.cs`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/UnconfiguredApprovedAvatarReadAccess.cs), [`Program.cs`](../../../src/UTTTGO.Api/Program.cs); interfaz `IApprovedAvatarReadAccess` | Sin adaptador real: `null`; contrato futuro con Media dueño, no acceso directo a su colección/S3; PRF-04/05. |

## Secuencia técnica

**Candidato actual:** obtener Bearer → `SessionService.AuthenticateAsync`: JWT validado contra firma, issuer/audience/exp y revocación Redis por `jti` (fallback `session_id` no ratificado), consultar confirmación Supabase actual, buscar perfil por `sub` en Mongo y exigir `Active`, coincidencia email/sujeto y roles locales → traducir perfil ausente a `401` (API-004 devuelve `404` candidato) → proyectar `id/displayName/email/phone/roles` desde perfil local; si referencia de avatar existe, solicitar por interfaz URL temporal de activo propio aprobado, por defecto `null`. Media ausente/fallida se omite sin publicar imagen; auth crítica inaccesible devuelve `503`, token/estado inválidos `401`. No escribir datos ni consultar Media antes de autorizar.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos/errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md) y guard [API-004](../api-004-auth-me/spec.md). Respetar DTO propio, roles de `users`, error con `correlationId` y denegación ante Redis crítico; Media publica contrato, Identity no lee `mediaAssets` ni S3 directamente.

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| PRF-01/03 | [`UsersController.cs`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/UsersController.cs), [`OwnProfileResponse.cs`](../../../src/UTTTGO.Api/Modules/Identity/Contracts/OwnProfileResponse.cs) | Ninguno ahora; si DF02-07 ratifica una diferencia, actualizar spec/plan/task y abrir corrección autorizada concreta, no reimplementar GET. |
| PRF-02/06 | [`OwnProfileServiceTests.cs`](../../../tests/UtttGo.UnitTests/OwnProfileServiceTests.cs), [`OwnProfileEndpointTests.cs`](../../../tests/UtttGo.IntegrationTests/OwnProfileEndpointTests.cs), [`OwnProfileSecurityTests.cs`](../../../tests/UtttGo.SecurityTests/OwnProfileSecurityTests.cs) | Pruebas adicionales en entorno no productivo aislado tras gates SDD; no modificar guard compartido de API-004 por esta ficha. |
| PRF-04/05 | [`OwnProfileService.cs`](../../../src/UTTTGO.Api/Modules/Identity/Application/OwnProfileService.cs), [`Program.cs`](../../../src/UTTTGO.Api/Program.cs), contrato `IApprovedAvatarReadAccess` | Solo conectar adaptador de **lectura** Media cuando su dueño publique contrato aprobado y exista servicio autorizado; no generar URL ni lógica de moderación aquí. Cambios a middleware requieren autorización separada. |

## Orden de implementación

DEV-API006-01 (inventario de fuente) → DEV-API006-02 (acta HTTP/DTO y avatar de lectura) → DEV-API006-03 (QA aislada auth) y DEV-API006-04 (integración de lectura Media) solo con sus gates propios. DEV-API006-04 no comienza porque no existe contrato/adaptador Media aprobado; no se crea tarea para implementar S3 o aprobación dentro de Identity. Si la decisión cambia códigos/campos, formular tarea de corrección específica tras rebasar la ficha; las tareas de QA no autorizan cambios de producción.

## Dependencias y bloqueos

| Gate transversal o decisión particular (ID original) | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| DF02-07, TASK-AUTH-010/023 | DEV-API006-02/03/04 | Acta API Designer/Identity/seguridad con HTTP/DTO, `401` vs `404`, nulidad y cotejo Excel por fuente autorizada. |
| DF02-05, TASK-AUTH-004/023 | DEV-API006-02/04 | Acta Users/Media/seguridad para lectura: propiedad, `Approved`, presigned GET privada, representación/nulidad y fallo Media. |
| DF02-01/09; TASK-AUTH-012/022; DF02-04 | DEV-API006-03 | Guard compartido: acta confirmación/fallo parcial y estrategia de revocación conforme constitución, sin atribuir aprobación al fallback `session_id`. |
| DF02-08; TASK-AUTH-007→008→010 | DEV-API006-03/04 | Aprobación colectiva/delegación del SDD, ficha aprobada y autorización de entorno no productivo; autorización de cambio Media específica. |

## Estrategia de pruebas

[QA histórico API-006](../../../docs/qa-api-006.md) (2026-11-04): +7 unitarias, +11 WebApplicationFactory y +11 seguridad con dobles, total entonces 158 verdes; no reejecutadas aquí. Tras autorización: JWT firmado de prueba y Redis/Mongo aislados, Supabase controlado, Media simulado con contrato verificado y luego adaptador autorizado no productivo. Casos PRF-01..06: query ajena ignorada, estado pendiente/suspendido/eliminado, perfil ausente, JWT revocado/expirado, roles locales, caídas de auth, Media no configurada/fallida, asset ajeno/no `Approved`, URL GET privada temporal y DTO exacto con `phone`/`avatarUrl` nulos. Comandos futuros: `dotnet restore UTTTGO.Backend.slnx`, `dotnet build UTTTGO.Backend.slnx -c Release --no-restore`, `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore` y las tres suites `dotnet test tests/UtttGo.UnitTests/UtttGo.UnitTests.csproj -c Release --no-build`, `dotnet test tests/UtttGo.IntegrationTests/UtttGo.IntegrationTests.csproj -c Release --no-build`, `dotnet test tests/UtttGo.SecurityTests/UtttGo.SecurityTests.csproj -c Release --no-build`. Registrar salida/fecha cuando se ejecuten; nunca contra producción.

## Seguridad, errores y observabilidad

Solo perfil vinculado a `sub` validado, sin acceso por query ni roles de JWT; confirmar con Supabase y perfil Mongo `Active`. Redis caído → `503` fail-closed; Media fallida tras autorización → `avatarUrl:null` sin exposición de imagen, sujeto a ratificación DF02-05. Nunca leer S3 público, guardar URL cliente, emitir presigned en cliente o exponer `AvatarMediaAssetId`. Errores [`ApiError`](../../../src/UTTTGO.Api/Shared/Contracts/ApiError.cs) con `correlationId` sin tokens ni detalles; revisar `401` frente a `404` en DF02-07. No cambiar orden middleware sin aprobación de AGENT-BE-01.

## Criterios de cierre técnico

Evidencia fechada por PRF-01..06 en entorno aislado, aprobación de contrato y gates, pruebas pertinentes para auth/Media, build Release/formato/suites en verde, secretos ausentes y semántica HTTP ratificada. `200` atribuido en AWS con avatar `null` no acredita presigned, antienumeración ni fallo Redis. TASK-AUTH-004/022/023 y contratos Bloque 1 cierran solo por su proceso propio, nunca por esta ficha.
