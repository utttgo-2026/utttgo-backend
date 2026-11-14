# API-040 — Cambiar estado de cuenta de usuario · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-040 `PATCH /api/v1/admin/users/{userId}/status` según [trazabilidad](../../../docs/traceability.md) y [phase-02](../../phase-02-auth-users/spec.md) v1.5 (RF-08/09/14, T-AUTH-13, AC-005/AC-009). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

La **función** está aprobada en [phase-02](../../phase-02-auth-users/spec.md): RF-08 (suspender ⇒ `Suspended` y bloquear operaciones), RF-09 (reactivar ⇒ `Active` y restaurar operaciones) y RF-14 (ruta única, auditoría, sin cambio de rol ni de cuenta ajena a la política). El **contrato HTTP/DTO** (cuerpo, códigos exactos, idempotencia ante mismo estado) **no está aprobado**; T-AUTH-13 y AC-005/AC-009 siguen abiertos y la ficha original permanece «Propuesta a validar».

## Responsabilidad única

Suspender o reactivar **una cuenta** desde el rol Admin mediante la ruta única de RF-14. No cambia roles, no elimina cuentas (corresponde [API-005](../api-005-delete-users-me/spec.md)), no lista (corresponde [API-039](../api-039-list-users/spec.md)), no decide desactivación definitiva (DEC-003) ni toca colecciones de otros módulos.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Trazabilidad](../../../docs/traceability.md) | API-040, corte 2026-11-08 | Método/ruta; «tarea futura por definir», sin implementación. |
| [Phase-02 general](../../phase-02-auth-users/spec.md) | v1.5, RF-08/09/14, CL-16, T-AUTH-13, AC-005, AC-009 | Función aprobada; contrato HTTP y evidencia sin cerrar. |
| [Phase-02 plan](../../phase-02-auth-users/plan.md) | v1.5, § API-040 | Suspensión/reactivación no es atajo de eliminación API-005. |
| [API-005](../api-005-delete-users-me/spec.md) | API-005 | Borde de eliminación de cuenta; no confundir con estado. |
| [Constitución](../../../docs/constitution.md) | §§ 4/5/6, DEC-003 v2.6 | Fronteras, autorización por recurso, datos compartidos pendientes. |

## Contrato aprobado

| Regla | ID/fuente | Límite |
|---|---|---|
| Suspende marcando `Suspended` y bloquea operaciones autenticadas | RF-08 (phase-02) | Efecto downstream en sesiones por definir. |
| Reactiva restaurando `Active` y sus operaciones | RF-09 (phase-02) | Mismo criterio de bloqueo al suspendido. |
| Ruta única con auditoría; sin cambio de rol ni cuenta propia | RF-14, T-AUTH-13 (phase-02) | HTTP/DTO exactos pendientes de ratificación. |
| Desactivación definitiva condicionada | RF-14, DEC-003 | No implementar borrado administrativo aquí. |

## Comportamiento implementado candidato

No existe endpoint de cambio de estado: `src/UTTTGO.Api/Modules/Admin/` solo implementa el lector de API-039 (`AdminUsersController.cs` con `GET`); no hay servicio de mutación, contrato de escritura desde Identity ni pruebas de suspensión/reactivación. El rol Admin ya se verifica con perfil local en el flujo de lectura.

## Funcionalidad requerida faltante

Contrato HTTP/DTO (cuerpo con `accountStatus` destino, códigos ante inexistente/mismo estado/cuenta propia), contrato de escritura publicado por el dueño de `users` (Identity), efecto sobre sesiones y operaciones autenticadas del suspendido, auditoría obligatoria de la mutación, rate limit de mutación, verificación de valores de estado en el modelo real y evidencia para AC-005/AC-009.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir |
|---|---|---|---|
| Método/ruta PATCH del inventario | API-040 (trazabilidad), RF-14 | Bearer Admin + `{userId}` + cuerpo `accountStatus` | DTO exacto y validación |
| Suspendido pierde operaciones autenticadas | RF-08 | `200` con estado actualizado | Mecanismo de bloqueo por sesión |
| Reactivado restaura operaciones | RF-09 | `200` con estado actualizado | Reingreso sin token antiguo |
| Rol/cuenta propia/ajena a política rechazados | T-AUTH-13, CL-16 | `400`/`403`/`404` uniforme sin escritura | Códigos exactos por caso |
| Mutación con dependencias caídas | § 6 | `503` sin cambio parcial | Orden transaccional |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): JWT/blacklist y rate limit distribuido fail-closed; rol `Admin` del perfil local, no del JWT; autorización por recurso (no operar sobre cuenta propia según T-AUTH-13); auditoría obligatoria de actor/objetivo/estado previo→nuevo/UTC/`correlationId` sin PII; sin cambio de rol ni borrado; error único con `correlationId`.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| ADMS-01 | Admin local `Active` y cuenta objetivo con estado distinto al destino | PATCH con `accountStatus` válido | `200` con estado actualizado y auditoría registrada (candidato) | RF-14 | Sin implementación | Función aprobada; HTTP pendiente |
| ADMS-02 | Admin que intenta cambiar rol, operar su propia cuenta o cuenta ajena a política | PATCH | Rechazo uniforme (`400`/`403` candidato) sin escritura | T-AUTH-13, CL-16 | Sin implementación | Código pendiente de acta |
| ADMS-03 | Token ausente/inválido/revocado, sin rol local Admin o con perfil no `Active` | PATCH | `401`/`403` sin escritura | § 6 | Sin implementación | Fail-closed constitucional |
| ADMS-04 | `userId` inexistente, `accountStatus` destino inválido o igual al actual | PATCH | `404`/`400`/`200` idempotente por decidir, sin estados parciales | RF-14 | Sin implementación | HTTP pendiente |
| ADMS-05 | Redis/Mongo/auditoría caídos o rate limit excedido | PATCH | `429`/`503` fail-closed sin cambio parcial | § 6, RF-14 | Sin implementación | Fail-closed constitucional |
| ADMS-06 | Cuenta suspendida con sesión previa; luego reactivada | Intenta operar autenticada en cada fase | Bloqueada mientras `Suspended`; operativa tras `Active` | RF-08, RF-09 | Sin implementación | Función aprobada; efecto pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| RF-08, RF-09, RF-14, T-AUTH-13, CL-16 | [Phase-02 general](../../phase-02-auth-users/spec.md) | ADMS-01..06 | DEV-API040-01..05 |
| AC-005, AC-009 | [Phase-02 general](../../phase-02-auth-users/spec.md) | ADMS-01/02 | DEV-API040-04/05 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| ADMS-01 | NO_IMPLEMENTADO | Sin endpoint ni contrato de escritura en Admin/Identity | DEV-API040-01/02 |
| ADMS-02 | NO_IMPLEMENTADO | Guardas de rol/cuenta propia sin implementar ni probar | DEV-API040-03 |
| ADMS-03 | NO_IMPLEMENTADO | Autorización de mutación sin implementar | DEV-API040-02/04 |
| ADMS-04 | BLOQUEADO | Códigos ante inexistente/mismo estado sin acta | DEV-API040-01 |
| ADMS-05 | NO_IMPLEMENTADO | Auditoría y tasa de mutación sin implementar | DEV-API040-03/04 |
| ADMS-06 | BLOQUEADO | Efecto de `Suspended` sobre sesiones/operaciones sin diseñar | DEV-API040-01/03 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-11 | Documental | [Trazabilidad](../../../docs/traceability.md) fila API-040: «tarea futura por definir» | Implementación ni pruebas. |
| 2026-11-04 | Documental | [Phase-02 general](../../phase-02-auth-users/spec.md) v1.5: RF-08/09/14 y T-AUTH-13 | Contrato HTTP ni evidencia AC-005. |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| Contrato HTTP/DTO de API-040 | [Phase-02 general](../../phase-02-auth-users/spec.md), trazabilidad | Cuerpo `accountStatus` único vs enum adicional; mismo estado ⇒ `200`/`409` | DEV-API040-01/02/04 | Diseño de fase-02: ratificar DTO y códigos. |
| Efecto de `Suspended` | RF-08, CL-16 | Invalidar sesión al instante vs rechazo en cada request; reingreso tras `Active` | DEV-API040-01/03 | Identity/sesiones: mecanismo y prueba AC-006 si aplica. |
| DEC-003 desactivación definitiva | [Constitución](../../../docs/constitution.md) | Mantener separada de API-040 (único camino viable) | DEV-API040-01 | Privacidad: diseño de borrado aparte; no ampliar esta ruta. |

## Criterios de preparación para desarrollo

Alcance [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos): función aprobada (RF-08/09/14) pero contrato HTTP/DTO sin ratificar y sin implementación; ADMS-01..06 requieren acta de contrato y efecto de `Suspended` antes de código. Faltan pruebas de mutación, guardas T-AUTH-13 y evidencia de AC-005/AC-009.
