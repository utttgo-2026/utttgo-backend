# API-007 — Editar perfil propio · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-007: `PATCH /api/v1/users/me`, identificada en [trazabilidad](../../../docs/traceability.md) y [fase 02](../../phase-02-auth-users/spec.md). La carpeta `api-007-update-users-me` es la indicada por [migración](../../../docs/migracion-sdd.md), distinta del nombre sugerido `api-007-users-me-patch` para la misma ruta. El Excel no está en este worktree: **cotejo directo pendiente de fuente**. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md).

## Estado de aprobación

La fase 02 v1.5 autorizó implementación local incremental; no ratificó validaciones, nulidad, HTTP/DTO completos ni escritura de avatar. AF02-06/07 y reglas constitucionales obligan a comprobar confirmación/estado y propiedad. [Paquete de decisiones](../../../docs/aprobaciones-sdd-lote-2a.md) mantiene **11 decisiones PENDIENTE**; no cubre aprobación expresa del PATCH API-007 ni concede delegación. TASK-AUTH-004/023/024 y 007→008→010 permanecen abiertos.

## Responsabilidad única

Actualizar parcialmente **solo el perfil local propio** tras autorización; nunca editar sesión [API-004](../api-004-auth-me/spec.md), leer otro perfil ni hacer registro/OTP/login/logout/recuperación. [API-006](../api-006-users-me/spec.md) es lectura y comparte proyección de respuesta; Media es dueño de subida, moderación, propiedad del activo y URL presignada. El PATCH actual **no escribe avatar**.

## Fuentes consultadas

| Fuente (enlace local) | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md), [base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md) | BR-007/009, REQ-002/005/006/007 | Auth, propiedad, roles locales, CAS e imágenes privadas; no ratifican límites numéricos de edición. |
| [Fase 02 spec](../../phase-02-auth-users/spec.md), [plan](../../phase-02-auth-users/plan.md), [task](../../phase-02-auth-users/task.md) | RF-04, T-AUTH-17, CL-12, TASK-AUTH-004/023/024; 2026-11-04 | Contrato PATCH propuesto y avatar pendiente. |
| [Decisiones](../../../docs/decisiones-pendientes-fase-02.md), [paquete](../../../docs/aprobaciones-sdd-lote-2a.md), [trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | DF02-01/05/07/08/09; 2026-11-13 | Gates comunes, sin aprobación API-007 ni fuente Excel disponible. |
| [QA API-007](../../../docs/qa-api-007.md) y [guía](../../../docs/sdd-guia-api-atomica.md) | 2026-11-04 | Cobertura con dobles en corte anterior; **no describe el repositorio Mongo y DI actuales**. |

## Contrato aprobado

| Regla | ID/fuente de aprobación expresa | Límite |
|---|---|---|
| Solo propietario autenticado; identidad y rol local nunca elegidos por el cliente, sin acceso antes de confirmación | [Constitución](../../../docs/constitution.md), [DF02-01/09](../../../docs/decisiones-pendientes-fase-02.md) | No aprueba `401/409/503` precisos ni política de confirmación/caché. |
| Nombre editable validado; email no editable, teléfono y avatar opcionales a nivel de producto | [RF-04](../../phase-02-auth-users/spec.md) | Campos/longitudes/formato/null/HTTP definitivos y escritura avatar sin aprobación de ficha. |
| Imagen solo propia y aprobada, moderación previa, S3 privado y presigned backend | [BR-007](../../../docs/constitution.md) | Media publica verificación; `avatarUrl` de escritura no está ratificado. |

## Comportamiento implementado candidato

[`UsersController.UpdateMe`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/UsersController.cs) exige Bearer vía Shared, lee JSON y valida [`UpdateOwnProfileRequest`](../../../src/UTTTGO.Api/Modules/Identity/Contracts/UpdateOwnProfileRequest.cs) con [`UpdateOwnProfileRequestValidator`](../../../src/UTTTGO.Api/Modules/Identity/Validators/UpdateOwnProfileRequestValidator.cs). Parser estricto: al menos una propiedad `displayName` o `phone` exacta, strings no nulas, sin propiedades adicionales ni duplicadas; `avatarUrl` y `mediaAssetId` se rechazan igual que `email`, `roles`, `accountStatus`, `sellerStatus`, `userId`, `emailVerified`. Nombre recortado no vacío de **1–100** caracteres; teléfono `^\+?[0-9]{10,15}$` (10–15 dígitos, `+` inicial opcional); límites candidatos. Campos omitidos se preservan; `null` explícito se rechaza, no borra. [`UpdateOwnProfileService`](../../../src/UTTTGO.Api/Modules/Identity/Application/UpdateOwnProfileService.cs) reutiliza `ISessionService.AuthenticateAsync` de API-004 (JWT firma/issuer/audience/exp, Redis, confirmación Supabase y perfil Mongo `Active` coherente), nunca toma sujeto/ID del body/query. [`IProfileUpdateRepository`](../../../src/UTTTGO.Api/Modules/Identity/Application/IProfileUpdateRepository.cs) exige CAS; [`MongoUserRepositories.UpdateOwnAsync`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoUserRepositories.cs) **sí existe y está registrado en [`Program.cs`](../../../src/UTTTGO.Api/Program.cs)**: `FindOneAndUpdate` condicional por `_id`, `supabaseUserId`, `Version` y `Active`, incrementa `Version` y solo escribe campos presentes. Si falla el filtro, relee existencia del mismo dueño `Active`: existente → `Conflict`; ausente/no `Active` → `Missing`. El servicio comprueba respuesta: versión incrementada, mismo ID/`sub`/email/rol/estado/avatar y campos esperados; de lo contrario `Unavailable`. Proyecta [`OwnProfileResponse`](../../../src/UTTTGO.Api/Modules/Identity/Contracts/OwnProfileResponse.cs) de API-006; el adaptador Media registrado sigue [sin configurar](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/UnconfiguredApprovedAvatarReadAccess.cs) y `avatarUrl` es `null` en ese caso. Ninguna de estas existencias es prueba Mongo concurrente real ni aprobación de contrato.

## Funcionalidad requerida faltante

Ratificar DTO/HTTP/validaciones, política de `null` y eliminación de teléfono/avatar, y escritura de avatar propia mediante contrato de Media (DF02-05/07). No existe asociación de nuevo activo en el PATCH; tampoco servicio Media real para lectura. Faltan pruebas *aisladas* de CAS Mongo real, carrera/suspensión entre réplicas, coherencia GET tras PATCH, JWT/Redis/Supabase reales y errores de dependencias. **No** crear tarea de reimplementación del CAS o guard ya codificados; registrar pruebas y correcciones específicas solo si se demuestra discrepancia. Logout/OTP/login/Media upload pertenecen a otras fichas.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir |
|---|---|---|---|
| Ruta/acceso | `PATCH /api/v1/users/me`, propio autorizado; RF-04 y constitución | Bearer único, JSON objeto con `displayName` y/o `phone`; query `userId` no selecciona dueño | Cotejo Excel, whitelist y forma JSON final DF02-07. |
| Validaciones | Nombre validado; email/roles/identidad no editables | Propiedades exactas sin duplicados; omisión conserva; `null` rechazada; nombre trim 1–100; phone `+` opcional y 10–15 dígitos; `avatarUrl`/`mediaAssetId` rechazados | Límites/null/borrado y escritura avatar DF02-05/07, sin imponer candidato como regla aprobada. |
| Éxito y salida | Solo cambios propios válidos; respuesta exacta pendiente | `200` mismo DTO de perfil API-006: `id,displayName,email,phone,avatarUrl,roles`; `phone`/`avatarUrl` nulos posibles; sin `accountStatus`, `version` ni token | Semántica de PATCH repetido/representación avatar y DTO final. |
| Errores | Error único `correlationId`, fallos críticos fail-closed | `400 VALIDATION_ERROR` entrada inválida/prohibida; `401 UNAUTHORIZED` sesión o perfil faltante; `409 PROFILE_CONFLICT` versión obsoleta; `503 PROFILE_UNAVAILABLE` dependencia/repo incoherente; JSON malformado puede usar `400 INVALID_REQUEST` del model binding compartido | Códigos/cuerpos y antienumeración por API-007 en DF02-07; `422` avatar no aprobado es propuesta histórica CL-12, no respuesta actual. |

## Seguridad y validaciones

Bearer/JWT validado, gate Redis fail-closed y perfil `Active` confirmado por Supabase. Escritura solo tras guard de [`API-004`](../api-004-auth-me/spec.md); filtro Mongo limita por sujeto, ID, versión y estado. Validación antes de servicio para JSON prohibido; no hay mass assignment ni escritura de email/roles/estado/avatar. La versión se incrementa **por cada PATCH exitoso**, incluso si el valor coincide: CAS evita sobrescritura silenciosa pero **no garantiza idempotencia de reintentos** ni garantía end-to-end si se pierde respuesta; decidir si se necesita deduplicación sin inventar endpoint o cabecera. `409` ante versión obsoleta, `401` si perfil desaparece, `503` ante fallo; cualquier respuesta incoherente queda cerrada, aunque una escritura ya realizada podría requerir reconciliación (no afirmar rollback). La referencia de avatar original no se cambia; sin Media real solo `null` y nunca URL pública. [Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). No modificar middleware ni datos ajenos.

## Escenarios Given/When/Then

| ID local | Dado (precondiciones) | Cuando (acción) | Entonces (resultado, etiquetar candidato si no aprobado) | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| EDT-01 | Usuario confirmado, perfil `Active` propio y versión actual | PATCH nombre o teléfono válido | Candidato `200` con nuevo campo, omitidos y avatar conservados, versión interna +1 | RF-04, TASK-AUTH-024 | UpdateOwnProfileServiceTests/EndpointTests con dobles | Validaciones y HTTP DF02-07 pendientes |
| EDT-02 | JSON vacío, malformado, tipo errado, `null`, nombre/phone fuera de límites candidatos | PATCH perfil | Candidato `400` saneado sin escritura | RF-04 | UpdateOwnProfileEndpointTests/ServiceTests con dobles | Nulidad y límites DF02-07 pendientes |
| EDT-03 | Body contiene ID ajeno, rol, estado, email o avatarUrl/mediaAssetId, incluso junto a campo válido | PATCH perfil | Candidato `400` sin llegar a repositorio; query ajena ignorada si body permitido | RF-04, BR-009 | UpdateOwnProfileSecurityTests y EndpointTests con dobles | Escritura avatar DF02-05 aparte |
| EDT-04 | Bearer inválido/revocado, Supabase no confirma, cuenta pendiente/suspendida o perfil faltante/ajeno | PATCH perfil | Candidato `401` genérico, no escribe | RF-04, DF02-09 | UpdateOwnProfileSecurityTests con dobles | Guard compartido; pruebas reales pendientes |
| EDT-05 | Dos PATCH autorizados leyeron la misma versión, o estado cambia durante CAS | Intentan guardar | Candidato un éxito/otro `409` por versión; perfil ausente/inactivo `401`; sin sobrescritura | RF-04, RNF-17 | ConcurrentOwnProfileUpdateTests con almacén simulado; Mongo real pendiente | DEV-API007-03 y política conflicto DF02-07 |
| EDT-06 | JWT/Redis/Supabase/Mongo fallan o repo devuelve resultado incoherente | PATCH válido | Candidato `503` saneado, no informar éxito; escritura parcial a investigar | RF-04, REQ-007 | UpdateOwnProfileServiceTests/EndpointTests con dobles | Pruebas aisladas pendientes |
| EDT-07 | Asset no aprobado o petición de asociar/borrar avatar | PATCH avatar | Hoy candidato `400`, sin cambio; futuro contrato Media propio aprobado pendiente, no aceptar URL arbitraria | RF-04, BR-007, T-AUTH-17, CL-12 | Tests rechazan `avatarUrl`/`mediaAssetId`; integración Media ausente | DF02-05, TASK-AUTH-004 |
| EDT-08 | PATCH `200` seguido de GET propio o reintento mismo valor | Se consulta o repite | GET refleja cambio con misma política de avatar (doble); cada PATCH exitoso incrementa versión: idempotencia no prometida | RF-04, TASK-AUTH-024 | UpdateOwnProfileEndpointTests con store simulado; prueba reintento real pendiente | Política de reintento y prueba Mongo pendientes |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales (sin renumerar) | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| RF-04, TASK-AUTH-024, HU-029/UI-025, PB-022/HU-22 | [Fase 02](../../phase-02-auth-users/spec.md), [task](../../phase-02-auth-users/task.md), [trazabilidad](../../../docs/traceability.md) | EDT-01/02/03/05/08 | DEV-API007-01/02/03 |
| DF02-01/09, TASK-AUTH-012/022/023, REQ-007 | [Decisiones](../../../docs/decisiones-pendientes-fase-02.md), [API-004](../api-004-auth-me/spec.md) | EDT-04/06 | DEV-API007-04; guard de API-004 |
| RF-04, T-AUTH-17, CL-12, BR-007, TASK-AUTH-004 | [Fase 02](../../phase-02-auth-users/spec.md), [constitución](../../../docs/constitution.md) | EDT-07 | DEV-API007-02/05; Media dueña de activo |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| EDT-01 | IMPLEMENTADO_PROBADO con dobles | PATCH/proyección existentes; contrato y Mongo real no verificados por estas suites | DEV-API007-02/03 |
| EDT-02 | BLOQUEADO | Límites, nulidad, tipos y códigos son candidatos | DEV-API007-02; DF02-07 |
| EDT-03 | IMPLEMENTADO_PROBADO con dobles | Whitelist/propiedad simuladas; contrato de escritura de avatar pendiente | DEV-API007-02/05; DF02-05 |
| EDT-04 | SIN_EVIDENCIA real | JWT/Redis/Supabase/estado Mongo reales no cubiertos por dobles de PATCH | DEV-API007-04 |
| EDT-05 | SIN_EVIDENCIA real | CAS implementado en Mongo y en doble; carrera real y clasificación `409/401` no aisladas | DEV-API007-03 |
| EDT-06 | SIN_EVIDENCIA real | Fallos reales y posible fallo tras escritura sin prueba específica | DEV-API007-03/04 |
| EDT-07 | NO_IMPLEMENTADO | Asociación/borrado avatar no existe; escritura y errores requieren contrato Media separado | DEV-API007-05; DF02-05 |
| EDT-08 | SIN_EVIDENCIA real | GET tras PATCH probado solo con store simulado; política de retry/idempotencia sin decisión | DEV-API007-03; DF02-07 |

## Evidencia con nivel y fecha

| Fecha | Nivel (documental/dobles/local real/externa) | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-04 | Dobles históricos | [QA API-007](../../../docs/qa-api-007.md): 20 unitarias, 36 integración WebApplicationFactory, 12 seguridad; entonces 226 totales verdes | No Mongo/JWT/Redis/Supabase ni Media reales; descripción de repositorio no configurado corresponde al corte anterior. |
| 2026-11-08 | Externa atribuida | [Trazabilidad](../../../docs/traceability.md) comunica AWS PATCH con CAS Mongo y `409` por concurrencia | No se reprodujo aquí ni acredita todas las ramas/seguridad, rollback o aprobación. |
| 2026-11-13 | Inspección de código | [`MongoUserRepositories.UpdateOwnAsync`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoUserRepositories.cs) y [DI](../../../src/UTTTGO.Api/Program.cs) presentes | Presencia de código ≠ prueba CAS Mongo aislada ni resultado de tests en esta intervención. |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas (si hay) | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| DF02-07, TASK-AUTH-010/024 | [Decisiones](../../../docs/decisiones-pendientes-fase-02.md) | Ratificar o modificar whitelist, null, longitudes/phone, `400/401/409/503`, reintento y DTO; no importar API-006 entrada | DEV-API007-02/03/04/05 | Equipo API Designer, Identity/Users y Security Reviewer: acta por PATCH + fuente Excel autorizada. |
| DF02-05, TASK-AUTH-004/023/024 | [Decisiones](../../../docs/decisiones-pendientes-fase-02.md) | `avatarUrl` histórico de escritura vs `mediaAssetId` propio aprobado propuesto; nulidad/eliminación y `422` avatar no aprobado sin ratificar | DEV-API007-02/05 | Equipo Users/Media/API Designer/seguridad: acta escritura separada de lectura, contrato Media publicado y pruebas. |
| DF02-01/09, TASK-AUTH-012/022 | [Decisiones](../../../docs/decisiones-pendientes-fase-02.md) | Fuente/frecuencia de confirmación y reconciliación por ratificar; guard API-004 existente | DEV-API007-04 | Equipo Identity/Data Modeler/seguridad: acta y pruebas no productivas de guard/fallo parcial. |
| DF02-08, TASK-AUTH-007/008/010 | [Paquete](../../../docs/aprobaciones-sdd-lote-2a.md) | Aprobación individual backend no acredita delegación colectiva; paquete tiene 11 decisiones pendientes | DEV-API007-03/04/05 | Equipo/delegación expresa: cadena SDD, ficha API-007 aprobada y permiso de pruebas no productivas. |

## Criterios de preparación para desarrollo

Alcance técnico: ratificar PATCH, **probar** CAS/guard ya implementados y solo tras aprobación Media integrar asociación segura del avatar. [Estado operativo BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos): hay código candidato, una tarea documental READY y ninguna técnica READY. Requiere decisión DF02-05/07 específica API-007, cadena TASK-AUTH-007→008→010, dependencias 004/012/022/023, contrato Media para escritura y pruebas aisladas autorizadas. TASK-AUTH-024 es el **cierre posterior** de API-007, no requisito previo de sus propias DEV. Un `200` en AWS o tests con dobles no cierran la ficha.
