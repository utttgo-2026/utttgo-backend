# API-040 — Cambiar estado de cuenta de usuario · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-040 `PATCH /api/v1/admin/users/{userId}/status`, módulo `Admin`, según [trazabilidad](../../../docs/traceability.md), [phase-02](../../phase-02-auth-users/spec.md) (RF-08/09/14, T-AUTH-13, AC-005/AC-009) y la matriz de migración. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). La ruta está en el inventario; el contrato HTTP/DTO sigue pendiente de ratificación.

Diseño consultado: [be-api-design](../../../skills/be-api-design.md), [be-architecture](../../../skills/be-architecture.md) y [be-memory](../../../skills/be-memory.md). Las decisiones DEC-018..021 vigentes no se reabren.

## Estado de aprobación

RF-08 (suspender), RF-09 (reactivar) y RF-14 (ruta única, auditoría y sin cambio de rol ni borrado) están aprobados funcionalmente en phase-02. El contrato HTTP/DTO propuesto aquí, los códigos exactos, el tratamiento del mismo estado, la revocación de sesiones y la consistencia entre Identity/Redis/auditoría no están aprobados. Por ello la ficha permanece `PENDIENTE/BLOCKED` y no autoriza código.

## Responsabilidad única

Suspender o reactivar una cuenta existente desde un actor `Admin` local y `Active`. El actor no puede operar sobre su propia cuenta. La ruta no cambia roles, no elimina cuentas, no lista usuarios y no modifica colecciones de otros módulos. API-005 conserva la responsabilidad de eliminación y API-039 la de listado.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | §§ 4, 6, 14 y 15; v2.7 | Fronteras modulares, autorización, Redis fail-closed, errores y pruebas; no ratifica el DTO de API-040. |
| [Phase-02 general](../../phase-02-auth-users/spec.md) | v1.5, RF-08/09/14, T-AUTH-13, AC-005/AC-009 | Función de suspensión/reactivación; HTTP, DTO y evidencia siguen pendientes. |
| [Trazabilidad](../../../docs/traceability.md) | API-040, corte disponible | Método/ruta y tarea futura; no implementación. |
| [API-005](../api-005-delete-users-me/spec.md) y [API-039](../api-039-list-users/spec.md) | Fichas relacionadas | Límites de eliminación y listado; no se incorporan a esta ruta. |
| [Contratos transversales](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md) | Bloque 1 | Contratos reutilizables; no se copian ni se consideran evidencia de API-040. |

## Contrato aprobado

| Regla | ID/fuente de aprobación expresa | Límite |
|---|---|---|
| Suspender marca `Suspended` y bloquea operaciones autenticadas | RF-08, phase-02 | El mecanismo de sesiones y login debe ratificarse. |
| Reactivar restaura operaciones | RF-09, phase-02 | Se propone exigir sesión nueva; HTTP/DTO pendientes. |
| Ruta única con auditoría y sin cambio de rol ni cuenta | RF-14, T-AUTH-13 | No aprueba códigos, mismo estado ni consistencia técnica. |
| Redis de seguridad y rate limit fail-closed | Constitución §6.3 | No define el contrato específico de revocación de todas las sesiones. |

## Comportamiento implementado candidato

No existe endpoint de mutación de API-040 ni contrato de escritura administrativa desde Identity. `Admin` solo contiene el lector de API-039; no hay servicio, DTO, revocación específica, auditoría ni pruebas de suspensión/reactivación. El rol Admin existente se verifica mediante perfil local en el flujo de lectura, pero eso no implementa esta mutación.

## Funcionalidad requerida faltante

Faltan el PATCH y su DTO cerrado, validación de `accountStatus`, guardas de Admin local `Active`, objetivo existente y distinto, contrato de Identity para escritura y revocación de sesiones en Redis, rechazo de login mientras `Suspended`, reactivación con sesión nueva, auditoría fail-closed, rate limit de mutación y pruebas de AC-005/AC-009. No se adjudican a API-040 borrado, cambio de rol ni recuperación de contraseña.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir |
|---|---|---|---|
| Método/ruta | PATCH Admin con `{userId}` | `PATCH /api/v1/admin/users/{userId}/status` | Ratificación de contrato completo. |
| Autenticación | JWT validado; rol local | Bearer JWT; actor local `Admin` y `Active` | Contrato de Identity y política de sesión. |
| Cuerpo | No ratificado | JSON cerrado: `{ "accountStatus": "Active" | "Suspended" }` | Si el mismo destino es `409` o idempotente. |
| Estados | RF-08/09 | Solo `Active` y `Suspended` | Confirmar enum estable y semántica de transición. |
| Éxito | Cambio de estado aprobado funcionalmente | `200` con `{ "userId": "string", "accountStatus": "..." }` | DTO y momento exacto de auditoría/revocación. |
| Rechazos | Error único constitucional | `400/401/403/404/409/429/503` con códigos propuestos abajo | Ratificar códigos y visibilidad de objetivo. |

Propuesta de errores: `400 VALIDATION_ERROR` para cuerpo/estado inválido; `401 UNAUTHORIZED` para JWT ausente, inválido, expirado o revocado; `403 FORBIDDEN` para actor no Admin/Active o cuenta propia; `404 USER_NOT_FOUND` para objetivo inexistente/no visible; `409 ACCOUNT_STATUS_UNCHANGED` para mismo estado; `429 RATE_LIMIT_EXCEEDED`; `503 DEPENDENCY_UNAVAILABLE` para Redis, Identity, MongoDB o auditoría no disponibles. No se propone HTTP fuera de esta tabla.

La respuesta de error sigue el contrato constitucional:

```json
{
  "correlationId": "string",
  "status": 403,
  "code": "FORBIDDEN",
  "message": "Descripción para el desarrollador",
  "details": []
}
```

## Seguridad y validaciones

Aplicar [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md) y [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Validar firma, issuer, audience, expiración y blacklist Redis; si Redis no responde, rechazar fail-closed. Leer rol y `accountStatus` del perfil local, nunca del JWT o cuerpo. Rechazar actor propio, objetivo inexistente, cuerpo con campos extra, roles, identidad o instrucción de borrado. El límite distribuido también es fail-closed.

Al suspender, Identity debe revocar las sesiones del objetivo mediante una interfaz/contrato y Redis (`revoked:{jti}`, TTL hasta `exp`). Login debe consultar el estado local y rechazar `Suspended`; reactivar permite solo una sesión nueva y no restaura tokens revocados. La mutación solo responde éxito tras estado, revocación requerida y auditoría; el orden y la atomicidad exactos quedan abiertos. Auditar actor, objetivo, estado previo/nuevo, UTC y `correlationId`, sin tokens ni PII innecesaria. Logs sin secretos. No cambiar middleware.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| ADMS-01 | Admin local `Active` y objetivo existente con estado distinto | PATCH con destino permitido | Candidato: `200`, estado actualizado, revocación si suspende y auditoría | RF-08/09/14, AC-005 | Test de endpoint e integración pendientes | Función aprobada; contrato pendiente |
| ADMS-02 | Token ausente/inválido/revocado o actor sin Admin/Active | PATCH | `401` o `403`, sin escritura | T-AUTH-13, Constitución §6 | Seguridad pendiente | Fail-closed constitucional |
| ADMS-03 | Admin apunta a su propio `userId` | PATCH | `403`, sin estado ni auditoría de mutación | T-AUTH-13, CL-16 | Seguridad pendiente | Regla funcional; HTTP candidato |
| ADMS-04 | Objetivo inexistente, estado inválido o campos extra | PATCH | `404` o `400`, sin escritura | RF-14, AC-005 | Integración pendiente | HTTP candidato |
| ADMS-05 | Objetivo ya tiene el destino | PATCH | Candidato: `409`, sin revocación ni auditoría de mutación | RF-14 | Decisión de idempotencia pendiente | HTTP pendiente |
| ADMS-06 | Redis/Identity/MongoDB/auditoría caídos o rate limit excedido | PATCH | `503` o `429`, sin cambio parcial | Constitución §6.3, RF-14 | Fallos pendientes | Fail-closed constitucional |
| ADMS-07 | Usuario suspendido con sesión previa y luego reactivado | Login/operación antes y después | Bloqueado mientras `Suspended`; tras `Active`, opera solo con sesión nueva | RF-08/09, AC-009 | Integración/seguridad pendientes | Efecto aprobado; mecanismo pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| RF-08, RF-09, RF-14 | [Phase-02](../../phase-02-auth-users/spec.md) | ADMS-01/05/07 | DEV-API040-01..05; Identity/login dependientes |
| T-AUTH-13, CL-16 | [Phase-02](../../phase-02-auth-users/spec.md) | ADMS-02/03 | DEV-API040-02..04 |
| AC-005, AC-009 | [Phase-02](../../phase-02-auth-users/spec.md) | ADMS-01/04/07 | DEV-API040-04/05 |
| RNF seguridad, errores y Redis | [Constitución](../../../docs/constitution.md) §§6, 14, 15 | ADMS-02/06 | DEV-API040-03/04 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| ADMS-01 | NO_IMPLEMENTADO | Sin endpoint, DTO ni escritura de Identity | DEV-API040-01/02 |
| ADMS-02/03 | NO_IMPLEMENTADO | Guardas de actor Admin activo y cuenta propia sin mutación ni pruebas | DEV-API040-02/04 |
| ADMS-04 | BLOQUEADO | Códigos y visibilidad de objetivo por ratificar | DEV-API040-01 |
| ADMS-05 | BLOQUEADO | Idempotencia/mismo estado sin decisión | DEV-API040-01 |
| ADMS-06 | NO_IMPLEMENTADO | Auditoría, revocación y rate limit de mutación faltantes | DEV-API040-03/04 |
| ADMS-07 | BLOQUEADO | Login suspendido, reactivación y sesiones antiguas sin contrato | DEV-API040-01/03/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| Pendiente | Pendiente | No se ejecutaron curl ni tests para API-040; la ficha es documental | No acredita implementación, Redis, Identity, login, auditoría ni AC-005/AC-009. |
| 2026-11-11 | Documental | [Trazabilidad](../../../docs/traceability.md): API-040 como tarea futura | No acredita endpoint ni pruebas. |
| 2026-11-04 | Documental | [Phase-02](../../phase-02-auth-users/spec.md) v1.5: RF-08/09/14 y T-AUTH-13 | No acredita HTTP/DTO ni evidencia de aceptación. |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| Contrato HTTP/DTO | Phase-02 y trazabilidad | DTO cerrado propuesto; mismo estado `409` o idempotente | DEV-API040-01/02/04 | Diseño/API y propietario de fase 02 |
| Revocación de sesiones | RF-08, Constitución §6.3 | Enumerar sesiones mediante Identity y escribir blacklist Redis; alcance exacto por cerrar | DEV-API040-01/03/04 | Identity + Seguridad |
| Login durante suspensión | RF-08/09 | Rechazo por perfil antes de emitir sesión; contrato de login y error por confirmar | DEV-API040-01/04/05 | Identity/API-002 |
| Consistencia estado/Redis/auditoría | RF-14 | Transacción, outbox o compensación segura; siempre sin `200` parcial | DEV-API040-01/03 | Architecture + Identity + persistencia |
| Auditoría | RF-14 | Dueño, colección/retención e idempotencia pendientes | DEV-API040-03/04 | Architecture + propietario de auditoría |
| Rate limit de mutación | Constitución §6.3 | Política, ventana y partición por decidir | DEV-API040-03/04 | Seguridad/operación |
| DEC-003 eliminación | Constitución §16, API-005 | Mantener separada de API-040 | DEV-API040-01 | No ampliar esta ruta; autoridad de privacidad |

## Criterios de preparación para desarrollo

Alcance técnico: PATCH Admin propio de estado, con contrato de Identity/Redis, login bloqueado al suspendido y auditoría; estado operativo [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos). `READY_FOR_IMPLEMENTATION` requiere ratificación HTTP/DTO, decisiones de sesiones/consistencia y autorización identificable; esta ficha no la otorga. Faltan acta del contrato, política de auditoría/rate limit, pruebas ADMS-01..07 y evidencia no productiva de AC-005/AC-009. No se autoriza código.
