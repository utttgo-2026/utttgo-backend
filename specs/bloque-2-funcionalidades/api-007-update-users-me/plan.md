# API-007 — Editar perfil propio · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → este plan → [task](task.md). Solo `PATCH /api/v1/users/me`, no reabrir ficha GET [API-006](../api-006-users-me/spec.md), sesión [API-004](../api-004-auth-me/spec.md) ni incorporar rutas de OTP/Media. [Paquete de decisiones](../../../docs/aprobaciones-sdd-lote-2a.md) está PENDIENTE, no ratifica el PATCH. Carpeta `api-007-update-users-me` coincide con migración.

## Arquitectura existente

[`UsersController`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/UsersController.cs) comparte ruta base con GET pero invoca servicio de edición separado. Parser de propiedades/validador y [`UpdateOwnProfileService`](../../../src/UTTTGO.Api/Modules/Identity/Application/UpdateOwnProfileService.cs) ya existen. [`SessionService.AuthenticateAsync`](../../../src/UTTTGO.Api/Modules/Identity/Application/SessionService.cs) comparte guard de API-004; [`MongoUserRepositories`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoUserRepositories.cs) implementa `IProfileUpdateRepository` y está registrado en DI; **no** está sin configurar como indicaba [QA histórico](../../../docs/qa-api-007.md) en 2026-11-04. La proyección de salida y `UnconfiguredApprovedAvatarReadAccess` se comparten con API-006; no duplicar repositorio ni consultar Media directamente desde Identity.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| HTTP/entrada | [`UsersController.cs`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/UsersController.cs), [`UpdateOwnProfileRequest.cs`](../../../src/UTTTGO.Api/Modules/Identity/Contracts/UpdateOwnProfileRequest.cs), [`UpdateOwnProfileRequestValidator.cs`](../../../src/UTTTGO.Api/Modules/Identity/Validators/UpdateOwnProfileRequestValidator.cs) | Bearer, whitelist exacta, omisiones/null, límites candidatos y `ApiError`; EDT-01/02/03. |
| Aplicación/DTO | [`UpdateOwnProfileService.cs`](../../../src/UTTTGO.Api/Modules/Identity/Application/UpdateOwnProfileService.cs), [`OwnProfileResponse.cs`](../../../src/UTTTGO.Api/Modules/Identity/Contracts/OwnProfileResponse.cs) | Autorizar, preservar campos, exigir versión/estado/identidad y proyectar perfil API-006; EDT-01/04/06/08. |
| Persistencia | [`IProfileUpdateRepository.cs`](../../../src/UTTTGO.Api/Modules/Identity/Application/IProfileUpdateRepository.cs), [`MongoUserRepositories.cs`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoUserRepositories.cs) | `FindOneAndUpdate` por `sub`, `_id`, `Version`, `Active`; conflicto o ausente; EDT-05/06. |
| Media | [`OwnProfileService.cs`](../../../src/UTTTGO.Api/Modules/Identity/Application/OwnProfileService.cs) (`IApprovedAvatarReadAccess`), [`Program.cs`](../../../src/UTTTGO.Api/Program.cs) (adaptador no configurado) | Solo lectura opcional y respuesta `avatarUrl:null` si no hay servicio; escritura de asset requiere contrato Media futuro; EDT-07. |

## Secuencia técnica

**Actual candidato:** Bearer → JSON objeto y validador (sin escribir si inválido) → `AuthenticateAsync` de API-004 valida JWT firma/issuer/audience/exp, blacklist Redis y confirmación Supabase, busca perfil propio Mongo `Active` coherente → servicio recorta nombre, pasa ID local/`sub`/versión leídos, flags de presencia y teléfono al repositorio → Mongo `FindOneAndUpdate` atómico por dueño + versión + `Active` incrementa versión, devuelve documento actualizado; si filtro no coincide, consulta por dueño activo para distinguir `409` versión obsoleta de `401` perfil ausente/no activo → servicio verifica resultado no cambió campos privilegiados → `200` perfil con proyección de avatar de API-006. No hay `If-Match`, deduplicación ni promesa de idempotencia: aun mismo valor incrementa versión; un retry tras respuesta perdida requiere política aprobada. Un fallo de coherencia tras escritura no acredita rollback. JWT/Redis/Supabase/Mongo críticos fallan cerrados `503`, Media de lectura ausente/fallida omite URL; HTTP final pendiente.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos y errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [persistencia Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis y revocación](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md), guard [API-004](../api-004-auth-me/spec.md) y proyección de perfil [API-006](../api-006-users-me/spec.md). Frontera Media: consumir contrato publicado por dueño, sin acceso a `mediaAssets`/S3 desde repositorio Identity, sin publicar imágenes no moderadas.

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| EDT-01/02/03 | [`UpdateOwnProfileRequest.cs`](../../../src/UTTTGO.Api/Modules/Identity/Contracts/UpdateOwnProfileRequest.cs), [`UpdateOwnProfileRequestValidator.cs`](../../../src/UTTTGO.Api/Modules/Identity/Validators/UpdateOwnProfileRequestValidator.cs), [`UsersController.cs`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/UsersController.cs) | **Ningún cambio ahora.** Si acta DF02-07 ratifica diferencia de límites, null o HTTP, actualizar SDD y abrir corrección técnica específica; no reimplementar whitelist actual. |
| EDT-05/06/08 | [`MongoUserRepositories.cs`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoUserRepositories.cs), [`UpdateOwnProfileService.cs`](../../../src/UTTTGO.Api/Modules/Identity/Application/UpdateOwnProfileService.cs) | Código CAS existe. Prueba Mongo en entorno aislado; cambiar solo si prueba descubre defecto y se aprueba corrección con evidencia, sin declarar atomicidad real por doble. |
| EDT-04/06 | [`UpdateOwnProfileSecurityTests.cs`](../../../tests/UtttGo.SecurityTests/UpdateOwnProfileSecurityTests.cs), [`UpdateOwnProfileEndpointTests.cs`](../../../tests/UtttGo.IntegrationTests/UpdateOwnProfileEndpointTests.cs), [`ConcurrentOwnProfileUpdateTests.cs`](../../../tests/UtttGo.UnitTests/ConcurrentOwnProfileUpdateTests.cs) | Extender/verificar guard y carreras solo tras permiso no productivo; no editar componentes compartidos de API-004 sin su ficha aprobada. |
| EDT-07 | [`UpdateOwnProfileRequest.cs`](../../../src/UTTTGO.Api/Modules/Identity/Contracts/UpdateOwnProfileRequest.cs), [`UpdateOwnProfileService.cs`](../../../src/UTTTGO.Api/Modules/Identity/Application/UpdateOwnProfileService.cs), [`Program.cs`](../../../src/UTTTGO.Api/Program.cs) | Asociación de avatar **no existe**; diseñar integración solo después de DF02-05/07, contrato Media aprobado y ficha API-007 actualizada. Media dueña de subida/moderación/presigned; cambio de middleware requiere autorización independiente. |

## Orden de implementación

DEV-API007-01 (lectura/fuente) → DEV-API007-02 (ratificación específica PATCH) → DEV-API007-03 (QA CAS Mongo) y DEV-API007-04 (QA seguridad/guard), condicionadas además a gates y permisos. DEV-API007-05 (asociación avatar propia) depende de 02 y contrato Media publicado; no usar para crear Media ni para implementar `avatarUrl` arbitraria. Si ratificar DTO obliga cambios distintos de avatar, actualizar trío y crear tarea nueva concreta antes de producción. No ejecutar 03/04/05 solo por aprobar una propuesta de la responsable backend.

## Dependencias y bloqueos

| Gate transversal o decisión particular (ID original) | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| DF02-07, TASK-AUTH-010; cierre posterior TASK-AUTH-024 | DEV-API007-02/03/04/05 | Acta por PATCH con whitelist, límites, null, reintentos, `400/401/409/503`, DTO y cotejo Excel autorizado; API Designer/Identity/seguridad + equipo/delegación. No exigir cerrar 024 antes de trabajar en sus pruebas. |
| DF02-05, TASK-AUTH-004/023 | DEV-API007-02/05 | Acta separada de **escritura** avatar y nulidad/eliminación, contrato Media de activo propio `Approved`; lectura API-006 no habilita escritura API-007. |
| DF02-01/09, TASK-AUTH-012/022 | DEV-API007-04 | Diseño guard/confirmación y fallo parcial validado por Identity/Data Modeler/seguridad, sin reabrir política de sesión unilateralmente. |
| DF02-08, TASK-AUTH-007→008→010 | DEV-API007-03/04/05 | Actas SDD equipo/delegación, aprobación de esta ficha, permisos de pruebas Mongo/Redis/Supabase/Media **no productivas**; resultados de 03/04/05 aportan evidencia para cerrar 024 después, no al revés. |

## Estrategia de pruebas

[QA 2026-11-04](../../../docs/qa-api-007.md): 20 unitarias + 36 WebApplicationFactory + 12 seguridad añadidas, con dobles, no reejecutadas aquí. [`UpdateOwnProfileServiceTests`](../../../tests/UtttGo.UnitTests/UpdateOwnProfileServiceTests.cs), [`ConcurrentOwnProfileUpdateTests`](../../../tests/UtttGo.UnitTests/ConcurrentOwnProfileUpdateTests.cs), [`UpdateOwnProfileEndpointTests`](../../../tests/UtttGo.IntegrationTests/UpdateOwnProfileEndpointTests.cs), [`UpdateOwnProfileSecurityTests`](../../../tests/UtttGo.SecurityTests/UpdateOwnProfileSecurityTests.cs) verifican rechazo de propiedades, actualización parcial, consulta GET tras PATCH y CAS *simulado*. Tras autorización: Mongo local aislado con dos escritores/versión igual y carrera con suspensión, `409`/`401`, omisión/null y preservación de rol/email/avatar; prueba de fallo tras escritura con resultado coherente; JWT firmado de prueba/Redis/Supabase controlados y fallo de cada dependencia; avatar aprobado/ajeno solo tras Media. Comandos futuros: `dotnet restore UTTTGO.Backend.slnx`, `dotnet build UTTTGO.Backend.slnx -c Release --no-restore`, `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore`, `dotnet test tests/UtttGo.UnitTests/UtttGo.UnitTests.csproj -c Release --no-build`, `dotnet test tests/UtttGo.IntegrationTests/UtttGo.IntegrationTests.csproj -c Release --no-build`, `dotnet test tests/UtttGo.SecurityTests/UtttGo.SecurityTests.csproj -c Release --no-build`. Registrar fecha/salida/entorno; no producción.

## Seguridad, errores y observabilidad

Whitelist estricta contra mass assignment, `sub`/ID/rol del backend, CAS con `Active` y versión, confirmación Supabase actual. `401` para auth/propiedad fallida, `409` para versión del mismo propietario y `503` ante infraestructura/incoherencia son **candidatos**, sujetos a DF02-07. Error compartido con `correlationId`, sin JWT, email sensible, teléfono o URL completa en logs; Redis crítico fail-closed. No alterar orden middleware ni relajar rate limit de producción. Media lectura falla sin exponer avatar y escritura no autorizada es rechazada; ningún objeto S3 público.

## Criterios de cierre técnico

Para EDT-01..08: contrato PATCH ratificado con acta, autorización de gates/entorno, evidencia específica fechada (Mongo CAS real y pruebas de fallo), suite Release/formato/unit/integración/seguridad sin errores y sin secretos. Si un PATCH repetido incrementa versión bajo contrato aprobado, documentar explícitamente que no es idempotente; no inventar garantía. Cierre TASK-AUTH-024 depende de 023/004 y verificación de su propia ficha; existencia de DI/Mongo y AWS atribuido no bastan.
