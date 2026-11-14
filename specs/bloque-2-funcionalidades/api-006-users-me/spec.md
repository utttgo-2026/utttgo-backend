# API-006 — Leer perfil propio · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-006: `GET /api/v1/users/me`, sin body; ruta en [trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) y [fase 02](../../phase-02-auth-users/spec.md). La carpeta `api-006-users-me` coincide con la migración. Excel original ausente de este worktree: **cotejo directo con Excel pendiente de fuente**; no se declara consultado. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md).

## Estado de aprobación

RF-03/04/12 y las reglas constitucionales sustentan la lectura privada, propiedad y origen local de roles. Fase 02 v1.5 autorizó implementación local incremental, **no** aprobó íntegramente HTTP/DTO ni integración Media. TASK-AUTH-023/004/022 y la cadena de gates SDD siguen abiertas; la presencia de código o el despliegue comunicado no suplen acta de aprobación de API-006.

## Responsabilidad única

Leer el perfil privado **propio**, nunca editarlo (API-007) ni consultar el perfil de otro usuario por query. [API-004](../api-004-auth-me/spec.md) aporta el guard compartido, pero su respuesta de sesión no es el perfil. Registro/OTP, login, logout, refresh, recuperación y subida/moderación de imágenes pertenecen a otras fichas/módulos; esta ruta solo puede consumir lectura controlada de avatar aprobada.

## Fuentes consultadas

| Fuente (enlace local) | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md), [base modular](../../bloque-1-arquitectura/base-modular/spec.md), [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md) | BR-007, REQ-002/005/006/007 | Auth, roles locales, frontera Media, imágenes privadas moderadas y error; no ratifican DTO exacto. |
| [Spec fase 02](../../phase-02-auth-users/spec.md), [plan](../../phase-02-auth-users/plan.md), [task](../../phase-02-auth-users/task.md) | RF-03/04/12, T-AUTH-11/17, TASK-AUTH-004/022/023; 2026-11-04 | Perfil propuesto, lectura avatar, dependencias históricas aún abiertas. |
| [Decisiones fase 02](../../../docs/decisiones-pendientes-fase-02.md), [trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | DF02-01/04/05/07/08/09 | Fuente secundaria del inventario, decisiones parciales, Excel no disponible. |
| [QA API-006](../../../docs/qa-api-006.md), [guía](../../../docs/sdd-guia-api-atomica.md), [API-004](../api-004-auth-me/spec.md) | 2026-11-04 / ficha de guard | Pruebas con dobles y límites; no aprobación de HTTP ni de presigned real. |

## Contrato aprobado

| Regla | ID/fuente de aprobación expresa | Límite |
|---|---|---|
| Perfil local vinculado a `sub`; identidad y roles no confiados al cliente, acceso por propietario | [Constitución](../../../docs/constitution.md), [RF-03/12](../../phase-02-auth-users/spec.md) | Ruta distinta de sesión API-004; HTTP/JSON final pendiente de DF02-07. |
| Cuenta pendiente de confirmación, suspendida o no habilitada no accede al perfil privado aun con JWT | [DF02-01/09](../../../docs/decisiones-pendientes-fase-02.md) | Diseño técnico Supabase/Mongo y fallos parciales sin ratificación. |
| Imagen propia solo aprobada/moderada en S3 privado con URL presignada backend | [BR-007](../../../docs/constitution.md) | No existe integración Media de lectura real; representación `avatarUrl`/nulidad por DF02-05. |
| Firma, issuer, audience, expiración y revocación Redis obligatorias; roles locales | [Constitución](../../../docs/constitution.md), [Redis Bloque 1](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md) | Fallback `session_id` del gate es código candidato, no política constitucional ratificada. |

## Comportamiento implementado candidato

[`UsersController.Me`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/UsersController.cs) lee Bearer único mediante [`BearerTokenReader`](../../../src/UTTTGO.Api/Shared/Contracts/BearerTokenReader.cs) y llama [`OwnProfileService`](../../../src/UTTTGO.Api/Modules/Identity/Application/OwnProfileService.cs). No hay validador de body para este GET sin body; el validador de PATCH en el mismo controlador corresponde a API-007, no a esta ficha. `SessionService.AuthenticateAsync` compartido con [API-004](../api-004-auth-me/spec.md) usa lector JWT y [`RedisJwtGate`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisJwtGate.cs) (firma, issuer, audience, exp, revocación y vínculo `sub`), comprueba confirmación actual en [`SupabaseAuthClient`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/SupabaseAuthClient.cs), lee `users` por sujeto en [`MongoUserRepositories`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoUserRepositories.cs) y permite solo `Active`, perfil consistente y roles locales. DI en [`Program.cs`](../../../src/UTTTGO.Api/Program.cs) conecta adaptadores reales de auth/Mongo/Redis; **no** de Media. [`OwnProfileResponse`](../../../src/UTTTGO.Api/Modules/Identity/Contracts/OwnProfileResponse.cs) proyecta `id,displayName,email,phone,avatarUrl,roles`: `id` ObjectId local serializado como string, `displayName`, `email`, `phone?`, roles y referencia `AvatarMediaAssetId` proceden de Mongo; email/identidad de Supabase solo se usan para verificar coincidencia, no se proyectan como claim. Si hay asset, `IApprovedAvatarReadAccess` recibe ID de asset y dueño local; [`UnconfiguredApprovedAvatarReadAccess`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/UnconfiguredApprovedAvatarReadAccess.cs) devuelve `null`. Solo un doble de Media devuelve URL temporal en pruebas. La proyección omite avatar ante fallo Media y conserva perfil; no publica URL almacenada ni cliente.

## Funcionalidad requerida faltante

Ratificación HTTP/DTO y política `401` frente al `404` propuesto para perfil ausente; diseño de nulidad y lectura controlada DF02-05/TASK-AUTH-004; contrato/adaptador Media para activo **propio Approved** con presigned GET backend y pruebas no productivas. Falta evidencia específica de JWT/Redis/Supabase/Mongo y fallos reales para API-006. No falta implementar otra vez el GET, el guard o la proyección existentes. Media dueña de moderación/subida; API-007 dueña de edición.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir |
|---|---|---|---|
| Entrada/acceso | `GET /api/v1/users/me`, recurso propio, JWT y estado permitido (RF-12/constitución) | Sin body; Bearer único obligatorio; query `id`/`userId` no selecciona recurso | Cotejo directo con Excel y contrato completo DF02-07. |
| Éxito/DTO | Perfil privado propio (RF-03/04/12), roles locales | `200` camelCase `id,displayName,email,phone,avatarUrl,roles`; `phone` y `avatarUrl` nullable, presentes con `null`; sin `accountStatus` de API-004, sin token ni documento Mongo | Ratificar campos opcionales/nulidad, URL temporal de avatar y estrategia Media (DF02-05). |
| Fallos | Error transversal con `correlationId`, fail-closed de auth | `401 UNAUTHORIZED` para Bearer inválido, no confirmado, inactivo, ajeno o **perfil ausente**; `503 AUTH_UNAVAILABLE` para fallo crítico. Media ausente/fallida: `200` con `avatarUrl:null` | `401` candidato contradice `404` de ficha histórica para ausente; antienumeración, HTTP/JSON exactos DF02-07. |

## Seguridad y validaciones

El GET no toma identificadores de query/body, no ejecuta escritura, no expone `supabaseUserId`, estado interno, tokens ni referencia de media. Solo sujeto de JWT **validado** consulta perfil Mongo propio; confirmación actual y email/sujeto coherentes, estado `Active` y roles de `users`, nunca del JWT. `PendingEmailVerification`, `Suspended`, `Deleted` u otro no `Active` → `401` candidato. Redis/JWKS/Supabase/Mongo no disponibles → `503` sin lectura; JWT inválido/revocado y Supabase no confirmado → `401`; perfil ausente → `401` candidato antienumeración. Fallo Media después de autorización → avatar opcional `null`, nunca imagen no moderada. [Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md); URLs presignadas de S3 solo backend y S3 privado. Sin umbrales nuevos ni cambios al middleware.

## Escenarios Given/When/Then

| ID local | Dado (precondiciones) | Cuando (acción) | Entonces (resultado, etiquetar candidato si no aprobado) | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| PRF-01 | JWT autorizado, Supabase confirmado y `users` propio `Active` coherente | GET perfil propio | Candidato `200` con seis campos locales, opcionales `null`, sin estado de sesión | RF-03/04/12, T-AUTH-11 | OwnProfileEndpointTests/ServiceTests con dobles | DTO pendiente DF02-07 |
| PRF-02 | Header ausente/malformado, JWT inválido/expirado/revocado, cuenta no confirmada, no `Active` o perfil ajeno | GET perfil propio incluso con query de otro usuario | Candidato `401` sin identidad ajena ni URL; query ignorado | RF-12, REQ-007 | OwnProfileSecurityTests con dobles; JWT/Redis reales pendientes | Seguridad obligatoria; DF02-09 para diseño |
| PRF-03 | Token/confirmación válidos pero perfil local inexistente | GET perfil propio | Candidato `401 UNAUTHORIZED`, no `404` de propuesta histórica ni de API-004 | RF-03/12, TASK-AUTH-023 | OwnProfileEndpointTests con dobles | DF02-07 antienumeración pendiente |
| PRF-04 | Perfil válido sin avatar propio aprobado, Media sin configurar o Media fallida | GET perfil propio | Candidato `200`, `avatarUrl:null`, sin URL pública | RF-04, BR-007, T-AUTH-17 | OwnProfileServiceTests/EndpointTests con dobles; adaptador nulo real | DF02-05, TASK-AUTH-004 |
| PRF-05 | Perfil válido y asset propio `Approved` | GET perfil propio | Candidato: URL privada temporal generada por backend, solo si Media verifica propiedad/aprobación | RF-04, BR-007 | Solo doble positivo; Media real no existe aquí | DF02-05, contrato Media y pruebas reales |
| PRF-06 | Redis/JWKS, Supabase o Mongo no disponibles | GET perfil propio | Candidato `503 AUTH_UNAVAILABLE`, sin exponer perfil ni detalles | RF-12, REQ-007 | OwnProfileEndpointTests simula fallos | Prueba no productiva autorizada pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales (sin renumerar) | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| RF-03/04/12, TASK-AUTH-023, HU-028/UI-024, PB-021/HU-21 | [Fase 02 spec](../../phase-02-auth-users/spec.md), [task](../../phase-02-auth-users/task.md), [trazabilidad](../../../docs/traceability.md) | PRF-01/02/03 | DEV-API006-01/02/03 |
| RF-04, T-AUTH-17, TASK-AUTH-004, BR-007 | [Fase 02](../../phase-02-auth-users/spec.md), [constitución](../../../docs/constitution.md) | PRF-04/05 | DEV-API006-02/04; Media dueña de aprobación y URLs |
| RF-12, T-AUTH-11, TASK-AUTH-022, REQ-007 | [API-004](../api-004-auth-me/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md) | PRF-02/06 | DEV-API006-03; guard compartido API-004 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| PRF-01 | IMPLEMENTADO_PROBADO con dobles | Proyección existe; aprobación de DTO y verificación con perfil real pendientes | DEV-API006-02/03 |
| PRF-02 | SIN_EVIDENCIA real | Guard reutilizado, pruebas con dobles no validan firma/revocación/estado ante servicios reales | DEV-API006-03; DF02-09 |
| PRF-03 | BLOQUEADO | `401` candidato difiere de `404` de propuesta; antienumeración por aprobar | DEV-API006-02; DF02-07 |
| PRF-04 | IMPLEMENTADO_PROBADO con dobles | Degradación segura de avatar implementada; nulidad final por ratificar | DEV-API006-02; DF02-05 |
| PRF-05 | NO_IMPLEMENTADO | Adaptador Media real no registrado; aprobación/propiedad/presigned solo en doble | DEV-API006-04; DF02-05 y Media |
| PRF-06 | SIN_EVIDENCIA real | Fallos críticos solo probados con dobles, no en entorno no productivo real | DEV-API006-03 |

## Evidencia con nivel y fecha

| Fecha | Nivel (documental/dobles/local real/externa) | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-04 | Dobles históricos | [QA API-006](../../../docs/qa-api-006.md): +7 unitarias, +11 integración WebApplicationFactory, +11 seguridad; entonces 158 totales verdes | No JWT/Redis/Supabase/Mongo/Media reales ni URL presignada. |
| 2026-11-08 | Externa atribuida | [Trazabilidad](../../../docs/traceability.md) comunica en AWS `401` sin token y `200` con perfil válido y avatar `null` | No se reprodujo aquí; no prueba antienumeración, `Approved` ni fallos reales. |
| 2026-11-12 | Inspección de código | [`OwnProfileService`](../../../src/UTTTGO.Api/Modules/Identity/Application/OwnProfileService.cs), [DI](../../../src/UTTTGO.Api/Program.cs), adaptador Media no configurado | No acredita aprobación ni ejecución de pruebas en este cambio. |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas (si hay) | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| DF02-07, TASK-AUTH-010/023 | [Decisiones](../../../docs/decisiones-pendientes-fase-02.md) | Perfil ausente `404` propuesto en ficha original frente a `401` candidato; seis campos por ratificar | DEV-API006-02/03/04 | Equipo API Designer/Identity/seguridad: acta de HTTP/DTO, nulidad y antienumeración; cotejo Excel autorizado. |
| DF02-05, TASK-AUTH-004/023 | [Decisiones](../../../docs/decisiones-pendientes-fase-02.md) | Avatar URL temporal/controlada o representación aprobada distinta | DEV-API006-02/04 | Equipo Users/Media/API Designer/seguridad: acta de lectura, nulidad, propiedad, aprobación y presigned GET; no decidir escritura API-007 aquí. |
| DF02-01/09, TASK-AUTH-012/022 | [Decisiones](../../../docs/decisiones-pendientes-fase-02.md) | Consulta de confirmación por petición es candidata; sin caché aprobada | DEV-API006-03 | Identity/Data Modeler/seguridad: diseño de confirmación/reparación y fallo parcial, prueba sin acceso pendiente. |
| DF02-04, DF02-08, TASK-AUTH-007/008/010 | [Guía](../../../docs/sdd-guia-api-atomica.md#dependencias-transversales-identity) | Revocación `jti` constitucional; fallback `session_id` candidato; aprobación individual no cierra gates | DEV-API006-03/04 | Equipo Identity/seguridad y equipo/delegación: estrategia JWT y actas SDD colectivas; autorización de pruebas no productivas. |

## Criterios de preparación para desarrollo

Alcance técnico: validar guard y proyección existentes y, **solo tras contrato Media**, integrar lectura controlada de avatar. [Estado operativo BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos): una tarea documental READY, ninguna técnica READY. Desbloquean actas DF02-05/07 por API-006, DF02-01/09 en el guard, DF02-04 para JWT sin `jti`, cadena TASK-AUTH-007→008→010, dependencias TASK-AUTH-022/004/023 y autorización Media específica. Evidencia por escenario y entorno, no inferir `VERIFIED` de dobles o despliegue.
