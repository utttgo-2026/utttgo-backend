# API-004 — Sesión propia · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-004, `GET /api/v1/auth/me`, sin body. Ruta identificada en [trazabilidad](../../../docs/traceability.md) y [migración](../../../docs/migracion-sdd.md); el Excel original no está en este worktree: **cotejo directo con Excel pendiente de fuente**. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md).

## Estado de aprobación

La fase 02 v1.5 autorizó implementación local incremental, **no** el contrato HTTP/DTO íntegro ni el cierre de TASK-AUTH-022/021/012. DF02-07 y la cadena DF02-08, TASK-AUTH-007/008/010 siguen pendientes. La presencia de adaptadores y el despliegue atribuido en trazabilidad no constituyen acta de aprobación de esta ficha.

## Responsabilidad única

Consultar identidad, roles y estado de la **sesión propia**; API-006 ofrece perfil editable por otra ruta. API-001 registro/OTP, API-002 login y activación, API-003 logout/revocación, refresh y recuperación son dependencias ajenas, no funcionalidades de API-004. El acceso privado exige identidad confirmada y perfil local habilitado.

## Fuentes consultadas

| Fuente (enlace local) | ID original y fecha | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) y [Bloque 1 Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md) | BR de seguridad, REQ-007/008 | JWT válido, revocación `revoked:{jti}`, fail-closed; no ratifica fallback `session_id`. |
| [Fase 02 spec](../../phase-02-auth-users/spec.md), [plan](../../phase-02-auth-users/plan.md), [task](../../phase-02-auth-users/task.md) | RF-12, CL-14, T-AUTH-11, TASK-AUTH-022/021/012; 2026-11-04 | Propuesta de HTTP/DTO, gate de confirmación; tareas aún abiertas. |
| [Decisiones](../../../docs/decisiones-pendientes-fase-02.md), [trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | DF02-01/04/07/08/09 | Alcance funcional parcial y gates; Excel ausente. |
| [QA API-004](../../../docs/qa-api-004.md) y [guía](../../../docs/sdd-guia-api-atomica.md) | 2026-11-04 / estándar atómico | Pruebas con dobles; no certifican servicios reales. |

## Contrato aprobado

| Regla | ID/fuente de aprobación expresa | Límite |
|---|---|---|
| Ruta de sesión distinta de perfil `GET /api/v1/users/me`; identidad/roles no se confían al cliente | [RF-12 y trazabilidad](../../phase-02-auth-users/spec.md) | Método/ruta documentados; cotejo con Excel pendiente. |
| Sin acceso privado de cuenta pendiente aun con JWT; rol desde perfil local | [DF02-01/09](../../../docs/decisiones-pendientes-fase-02.md), [constitución](../../../docs/constitution.md) | Diseño técnico de confirmación y reconciliación por ratificar. |
| Validación de firma/issuer/audience/exp; revocación Redis `revoked:{jti}` y fail-closed | [constitución](../../../docs/constitution.md), [Bloque 1](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md) | No aprueba códigos HTTP específicos ni fallback sin `jti`. |

## Comportamiento implementado candidato

[`AuthController.Me`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs) lee Bearer mediante [`BearerTokenReader`](../../../src/UTTTGO.Api/Shared/Contracts/BearerTokenReader.cs); [`SessionService`](../../../src/UTTTGO.Api/Modules/Identity/Application/SessionService.cs) exige sujeto validado, gate JWT/Redis, identidad Supabase confirmada y perfil Mongo local consistente y `Active`. [`ValidatedJwtSubjectReader`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/ValidatedJwtSubjectReader.cs) lee `sub` no confiable **antes** de invocar el gate; no lo acepta sin verificación. [`RedisJwtGate`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisJwtGate.cs) verifica firma (JWKS o secreto configurado, mutuamente exclusivos), issuer, audience, expiración, vínculo `sub` y consulta Redis; usa `jti` o, si falta, `session_id` como clave de revocación **candidata**. [`SupabaseAuthClient`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/SupabaseAuthClient.cs) consulta `/auth/v1/user` por petición; [`MongoUserRepositories`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoUserRepositories.cs) lee `users` por `supabaseUserId`. [Program.cs](../../../src/UTTTGO.Api/Program.cs) registra estos adaptadores. No se promueve un pendiente desde API-004: `PendingEmailVerification`, `Suspended`, `Deleted` y cualquier estado diferente de `Active` reciben `401` candidato.

## Funcionalidad requerida faltante

No se acredita aún aprobación del HTTP/DTO completo, de la semántica antienumeración para perfil ausente, de la estrategia sin `jti` ni pruebas específicas de JWT/Redis/Supabase/Mongo reales y fallo parcial en entorno no productivo. El guard existe; no crear tarea para reimplementarlo. OTP/activación corresponden a API-001/002, logout a API-003; no incorporarlos a esta ruta.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir |
|---|---|---|---|
| Método y entrada | `GET /api/v1/auth/me`, identidad propia; validación constitucional | Bearer único, sin body; no toma identidad del query | Cotejo Excel y condiciones completas de acceso. |
| Éxito | Identidad/rol/estado local sin token (RF-12) | `200` JSON camelCase [`SessionResponse`](../../../src/UTTTGO.Api/Modules/Identity/Contracts/SessionResponse.cs): `id,email,displayName,roles,accountStatus`; sin phone/avatar ni JWT | Campos/nulidad y HTTP finales en DF02-07. |
| Error y dependencias | Error compartido con `correlationId`; fail-closed | `401 UNAUTHORIZED` para Bearer inválido, revocado, no confirmado o perfil no activo/inconsistente; `404 PROFILE_NOT_FOUND` si no existe perfil después de validar identidad; `503 AUTH_UNAVAILABLE` si dependencia falla; [`ApiError`](../../../src/UTTTGO.Api/Shared/Contracts/ApiError.cs) | Ratificar `404` vs `401` por antienumeración, códigos y cuerpo exactos; no copiar API-002. |

## Seguridad y validaciones

Propiedad por `sub` validado y perfil local, sin roles del JWT. Solo `Active` con ID, nombre, email Supabase coincidente, `sub` consistente y roles locales no vacíos. La excepción no cancelada en el servicio produce `Unavailable` → `503`; Redis inaccesible nunca concede sesión, Supabase ausente/no confirmado → `401`, Mongo ausente → `404` candidato y Mongo inaccesible → `503`. No cachear perfiles por inferencia. [Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md) rigen error, datos y degradación; no registrar JWT, `jti` ni datos sensibles. No modificar middleware sin aprobación.

## Escenarios Given/When/Then

| ID local | Dado (precondiciones) | Cuando (acción) | Entonces (resultado, etiquetar candidato si no aprobado) | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| SES-01 | JWT válido no revocado, correo confirmado y perfil propio `Active` coherente | Se consulta la sesión | Candidato: `200` con cinco campos locales sin tokens | RF-12, T-AUTH-11 | SessionServiceTests y SessionEndpointTests con dobles; externo atribuido | Contrato DTO pendiente DF02-07 |
| SES-02 | Bearer ausente/malformado o JWT inválido/expirado/revocado | Se consulta la sesión | Candidato: `401 UNAUTHORIZED`, sin identidad | RF-12, CL-14 | SessionSecurityTests con dobles; JWT/Redis reales pendientes | Seguridad constitucional; prueba real pendiente |
| SES-03 | Supabase no confirma o perfil `PendingEmailVerification`, `Suspended`, `Deleted` u otro no `Active`, o identidad inconsistente | Se consulta la sesión | Candidato: `401`, sin promover estado ni revelar roles | RF-12, DF02-01/09 | SessionServiceTests y SessionSecurityTests con dobles | Confirmación y transición técnica pendientes DF02-09 |
| SES-04 | JWT y Supabase válidos; no existe perfil local | Se consulta la sesión | Candidato: `404 PROFILE_NOT_FOUND`; antienumeración por revisar | CL-14, RF-12 | SessionEndpointTests con dobles | DF02-07 pendiente |
| SES-05 | Redis, JWKS, Supabase o Mongo no disponible | Se consulta la sesión | Candidato: `503 AUTH_UNAVAILABLE`, nunca acceso por fallo | RF-12, REQ-007 | SessionServiceTests/SessionEndpointTests con dobles; pruebas reales pendientes | Política fail-closed; evidencia real pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales (sin renumerar) | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| RF-12, CL-14, T-AUTH-11, TASK-AUTH-022 | [Fase 02](../../phase-02-auth-users/spec.md) y [task](../../phase-02-auth-users/task.md) | SES-01/02/04 | DEV-API004-01/02/03 |
| DF02-01/09, TASK-AUTH-012/021 | [Decisiones](../../../docs/decisiones-pendientes-fase-02.md) | SES-03/05 | DEV-API004-03; API-001/002 |
| REQ-007/008, DF02-04 | [Bloque 1 Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md) | SES-02/05 | DEV-API004-03; API-003 para escritura de revocación |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| SES-01 | IMPLEMENTADO_PROBADO con dobles | DTO existente; ratificación y cotejo de fuente primaria ausentes | DEV-API004-01/02 |
| SES-02 | SIN_EVIDENCIA real | JWT/revocación reales y estrategia sin `jti` no probados en esta ficha | DEV-API004-03; DF02-04 |
| SES-03 | BLOQUEADO | Guard implementado; diseño de consulta/transición técnica y fallo parcial pendientes | DEV-API004-03; DF02-09 |
| SES-04 | BLOQUEADO | `404` candidato sin decisión antienumeración | DEV-API004-02; DF02-07 |
| SES-05 | SIN_EVIDENCIA real | Fallos reales de dependencias en entorno no productivo no acreditados por dobles | DEV-API004-03 |

## Evidencia con nivel y fecha

| Fecha | Nivel (documental/dobles/local real/externa) | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-04 | Dobles, histórico | [QA API-004](../../../docs/qa-api-004.md): +13 unitarias, +12 integración WebApplicationFactory, +8 seguridad; 129 totales verdes entonces | No firma JWT, Redis ni Supabase/Mongo externos reales hoy. |
| 2026-11-08 | Externa atribuida | [Trazabilidad](../../../docs/traceability.md) atribuye AWS `401` sin token y `200` con sesión válida | No acredita cada rama, antienumeración, aprobación ni pruebas reproducidas aquí. |
| 2026-11-12 | Inspección de código | [`SessionService`](../../../src/UTTTGO.Api/Modules/Identity/Application/SessionService.cs), adaptadores y DI presentes | No sustituye actas ni ejecución de tests en esta intervención. |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas (si hay) | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| DF02-07, TASK-AUTH-010/022 | [Decisiones](../../../docs/decisiones-pendientes-fase-02.md) | `404` candidato o `401` para perfil ausente; DTO de sesión por ratificar | DEV-API004-02/03 | Equipo API Designer, Identity y Security Reviewer: acta específica HTTP/DTO/antienumeración, cotejo Excel autorizado. |
| DF02-04, REQ-007 | [Decisiones](../../../docs/decisiones-pendientes-fase-02.md) | `jti` constitucional; fallback `session_id` presente pero no ratificado | DEV-API004-03 | Identity/móvil/seguridad: estrategia conforme a constitución para JWT sin `jti`; revocación escritor en API-003. |
| DF02-01/09, TASK-AUTH-012 | [Decisiones](../../../docs/decisiones-pendientes-fase-02.md) | Confirmación actual por petición candidata, sin caché aprobada | DEV-API004-03 | Equipo Identity, Data Modeler y seguridad: acta de fuente/frecuencia, transición/reparación y prueba de fallo parcial. |
| DF02-08, TASK-AUTH-007/008/010 | [Guía](../../../docs/sdd-guia-api-atomica.md#dependencias-transversales-identity) | Aprobación individual no sustituye delegación/equipo | DEV-API004-03 | Actas de cadena SDD cerrada, autorización de pruebas técnicas no productivas. |

## Criterios de preparación para desarrollo

Alcance técnico: ratificación de contrato y verificación de guard API-004 ya existente, **no** reimplementación de login/OTP/logout. [Estado operativo BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos): código candidato presente y lectura documental autorizada, sin tarea técnica READY. Para desbloquear: acta DF02-07 por API-004, DF02-04/09 en lo pertinente, TASK-AUTH-007→008→010 y 022/021/012 según dependencias, cotejo de Excel autorizado y plan de pruebas no productivas. `VERIFIED` requiere evidencia por escenario/entorno; ruta desplegada no basta.
