# API-003 — Cerrar sesión · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-003 `POST /api/v1/auth/logout` según [trazabilidad](../../../docs/traceability.md) y [fase 02](../../phase-02-auth-users/spec.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente en este worktree.

## Estado de aprobación

RF-10, BR de revocación y fallo cerrado son obligatorios; HTTP, cuerpo, renovación y semántica de repetición no están aprobados (TASK-AUTH-003/025). Ficha pendiente, no permiso de programación.

## Responsabilidad única

Revocar la sesión presentada, al menos el JWT actual hasta `exp`; no realiza login (API-002), no borra cuenta (API-005), no implementa recuperación de contraseña ni sustituye el borrado de credenciales/caché del móvil.

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | blacklist JWT, BR-013 | `revoked:{jti}` hasta `exp`, Redis fail-closed. |
| [Fase 02](../../phase-02-auth-users/spec.md), [tareas](../../phase-02-auth-users/task.md) | RF-10, AC-006/010, T-AUTH-04/05/16, TASK-AUTH-003/025 | PDF profesor §§ 10/16; ejemplos de HTTP y refresh no ratificados; sin HU/UI/PB inequívocos. |
| [Trazabilidad](../../../docs/traceability.md), [decisiones](../../../docs/decisiones-pendientes-fase-02.md) | API-003, DF02-04/08 | Gate de sesiones y aprobación. |

## Contrato aprobado

| Regla | Autoridad | Límite |
|---|---|---|
| Revocar `jti` del JWT hasta `exp`, en Redis compartido | Constitución § 3/6, RF-10 | No revoca por sí solo refresh tokens en Supabase. |
| Redis indisponible: no confirmar logout | Constitución § 6.1, AC-006 | Código HTTP por ratificar. |

## Comportamiento implementado candidato

No hay acción logout en [`AuthController.cs`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs). [`RedisJwtGate.cs`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisJwtGate.cs) consulta revocación para el acceso existente; no equivale a un escritor de revocación ni a prueba de logout entre réplicas.

## Funcionalidad requerida faltante

Escritor Redis de revocación y endpoint API-003; coordinación con Supabase y móvil sobre refresh, renovación y credenciales (TASK-AUTH-003), sin implementar flujos del móvil en el backend.

## Contrato de entrada/salida

| Elemento | Aprobado | Candidato histórico | Falta decidir |
|---|---|---|---|
| Entrada | Bearer JWT validado (firma, issuer, audience, `exp`, `jti`); identidad no enviada por cliente | Body `refreshToken` «si aplica» | Presencia del body, manejo de JWT ya revocado y refresh/Supabase. |
| Éxito | Revocación efectiva hasta expiración | `204` sin cuerpo, repetición idempotente `204` | HTTP, secuencia/resultado parcial; no prometer logout global. |
| Error | Redis falla cerrado; error único con `correlationId` | `401` inválido, `503` dependencia | Respuestas exactas para repetición, proveedor caído y token sin `jti`. |

## Seguridad y validaciones

Usar [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md) y [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): comprobar JWT antes de escribir `revoked:{jti}`, TTL hasta `exp` (no indefinido), fail-closed en las dos réplicas; rate limit distribuido, no registrar token/jti/refresh ni cambiar middleware. Sin fallback de sesión alternativo aprobado para token sin `jti`.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| LOG-01 | JWT vigente con `jti` | POST logout | Se revoca hasta `exp`; nueva solicitud en otra réplica no entra | RF-10, AC-006 | Unit e integración Redis compartido por crear | Regla aprobada; endpoint bloqueado |
| LOG-02 | JWT inválido o Redis caído | POST logout | No se confirma revocación ni se abre acceso | T-AUTH-04/16 | Seguridad por crear | HTTP pendiente |
| LOG-03 | JWT ya revocado o refresh emitido | Repite logout/renueva | No puede recuperar acceso según política futura | TASK-AUTH-003/025 | Pruebas multi-réplica/Supabase pendientes | DF02-04 |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | Tarea DEV |
|---|---|---|---|
| RF-10, AC-006, T-AUTH-04/05/16 | [Fase 02](../../phase-02-auth-users/spec.md) | LOG-01/02 | DEV-API003-02/03 |
| AC-010, TASK-AUTH-003/025 | [Fase 02 tareas](../../phase-02-auth-users/task.md) | LOG-03 | DEV-API003-01/04 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| LOG-01 | NO_IMPLEMENTADO | Falta ruta y escritor Redis | DEV-API003-02 |
| LOG-02 | NO_IMPLEMENTADO | Sin prueba de caída o JWT sin `jti` | DEV-API003-03 |
| LOG-03 | BLOQUEADO | Refresh, repetición y móvil sin decisión | DEV-API003-01/04; DF02-04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Inspección documental | `AuthController.cs` sin POST logout; fase 02 RF-10 | Revocación escrita, Supabase o multi-réplica. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| DF02-04, TASK-AUTH-003 | [Decisiones](../../../docs/decisiones-pendientes-fase-02.md) | Refresh en body o gestionado por cliente/Supabase; repetición | DEV-API003-01/02/04 | Identity, móvil y seguridad: acta de sesiones/HTTP. |
| TASK-AUTH-007→008→010 | [Fase 02](../../phase-02-auth-users/task.md) | No suplir aprobación colectiva con código | DEV-API003-02/03/04 | Equipo/delegación con aprobación identificable. |

## Criterios de preparación para desarrollo

Alcance: logout completo, estado [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos). Cerrar DF02-04, contrato y gates antes de ejecutar DEV técnicos; aceptación: revocación efectiva entre réplicas, fallo Redis cerrado, renovación y repetición resueltas sin secretos ni pruebas en producción.
