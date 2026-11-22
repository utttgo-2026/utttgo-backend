# API-005 - Eliminar cuenta propia · spec

Estado de aprobación de la ficha: APROBADA
Estado operativo: BLOCKED

## Identificación y autoridad

ID `API-005`; método/ruta `DELETE /api/v1/users/me`, según RF-11, BR-011 y el inventario de `docs/traceability.md`. La autoridad es `docs/constitution.md` -> esta ficha -> `plan.md` -> `task.md`. La aprobación explícita del propietario del 2026-11-22 cierra DEC-003 con las decisiones descritas aquí.

## Estado de aprobación

El contrato HTTP, la anonimización local, el bloqueo por pedidos activos, la pausa de productos, la revocación Redis y el logout global best-effort quedan aprobados. Reviews y mensajes no se implementan en esta ficha; solo se documenta `deletedUserRef` para futuras coordinaciones.

## Responsabilidad única

Coordinar la baja de la cuenta autenticada en Identity. Identity solo modifica `users`; consulta Orders mediante `IActiveOrderReader` y solicita a Catalog la pausa mediante `IAccountDeletionCatalog`. No recibe `userId` del cliente ni accede a colecciones ajenas.

## Fuentes consultadas

| Fuente (enlace local) | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-011, RF-11 | Eliminar información propia; conservar solo datos compartidos necesarios. |
| [Contratos transversales](../../bloque-1-arquitectura/contratos-transversales/spec.md) | Error único y `correlationId` | Formato de errores; no modifica middleware. |
| [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md) | Revocación JWT | Clave `revoked:{id}` y TTL hasta expiración. |

## Contrato aprobado

| Regla | ID/fuente de aprobación expresa | Límite |
|---|---|---|
| `204` sin body tras completar la coordinación local | DEC-003, aprobación del propietario 2026-11-22 | Logout global Supabase es best-effort. |
| `401 UNAUTHORIZED` | API-005 / sesión Identity | Token ausente, inválido, expirado, revocado, sujeto inválido o perfil ausente. |
| `409 ACTIVE_ORDER_CONFLICT` | DEC-003 | Estados `Pending`, `Accepted`, `InPreparation` y `Ready`; no se muta la cuenta. |
| `503 ACCOUNT_DELETION_UNAVAILABLE` | Constitución, Redis fail-closed | Mongo, Orders, Catalog o Redis no disponibles; no se confirma la baja. |
| Cuenta local `Deleted` y anonimizada | BR-011 / DEC-003 | `displayName`, email, teléfono y avatar dejan de identificar al usuario; `deletedUserRef` es UUID aleatorio estable. |

## Comportamiento implementado candidato

`DeleteOwnAccountService` valida la sesión con los gates existentes, consulta pedidos activos, aplica una actualización CAS en `users`, pausa productos del vendedor, revoca el JWT actual en Redis y llama al logout global de Supabase sin hacer fallar el `204` por la respuesta del proveedor. Cada operación local es idempotente; una cuenta ya `Deleted` continúa con los pasos pendientes.

## Funcionalidad requerida faltante

Reviews y mensajes no tienen implementación ni contratos de baja en esta fase. La cuenta conserva únicamente `deletedUserRef` para que sus fichas propietarias futuras anonimicen referencias compartidas. No se borran colecciones de Reviews, Chat ni Orders desde Identity.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir |
|---|---|---|---|
| Método/ruta y auth | `DELETE /api/v1/users/me`, Bearer validado | Implementado en `UsersController` | Ninguna |
| Body/query | Sin body, query ni `userId` | El controlador no lee identificadores del cliente | Ninguna |
| Éxito | `204` sin body | Implementado | Evidencia de integración requiere Docker |
| Errores | `401`, `409`, `503` con contrato único | Implementado | Evidencia de integración requiere Docker |

## Seguridad y validaciones

El sujeto proviene del JWT validado por Identity; el cliente no decide identidad, roles, estado, `deletedUserRef` ni productos. Mongo usa CAS por sujeto, ID local, versión y estado. Orders se consulta antes de cualquier mutación. Redis es fail-closed para revocación. Supabase Admin y `service_role` no se usan. El logout global usa el cliente existente y no registra tokens ni respuestas del proveedor. Errores no exponen PII, sujetos, IDs internos ni secretos.

## Escenarios Given/When/Then

| ID local | Dado (precondiciones) | Cuando (acción) | Entonces (resultado) | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| DEL-01 | Sesión válida, sin pedidos activos y dependencias disponibles | DELETE `/api/v1/users/me` | Perfil anonimizado, productos pausados si aplica, JWT revocado y `204` | RF-11 / BR-011 | Unitarias, integración y seguridad | Aprobado 2026-11-22 |
| DEL-02 | Pedido activo como comprador o vendedor | DELETE `/api/v1/users/me` | `409 ACTIVE_ORDER_CONFLICT`, sin mutaciones | RF-11 | Unitarias e integración | Aprobado 2026-11-22 |
| DEL-03 | Perfil ya `Deleted` tras fallo posterior | Repetir DELETE con sesión todavía válida | Continúa pasos pendientes e idempotentes; `204` si Redis completa | BR-011 | Unitaria e integración | Aprobado 2026-11-22 |
| DEL-04 | JWT ausente, inválido, expirado o revocado | DELETE `/api/v1/users/me` | `401 UNAUTHORIZED`, sin mutaciones | Seguridad | Seguridad | Aprobado 2026-11-22 |
| DEL-05 | Mongo, Orders, Catalog o Redis falla | DELETE o reintento | `503 ACCOUNT_DELETION_UNAVAILABLE`; no confirma la operación | NFR seguridad | Integración con dobles; Docker pendiente | Aprobado 2026-11-22 |
| DEL-06 | Login posterior con perfil local `Deleted` | POST login | Login rechazado; no se emite sesión utilizable | BR-011 | Seguridad | Aprobado 2026-11-22 |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales (sin renumerar) | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| RF-11, BR-011, AC-007, T-AUTH-10 | Constitución y fase 02 | DEL-01..06 | DEV-API005-01..04 |
| API-003 | Logout y Redis | DEL-01, DEL-03, DEL-04 | `IJwtRevocationStore`, `ISupabaseSessionRevoker` |
| Orders | Fase 04 | DEL-02 | `IActiveOrderReader` |
| Catalog | Fase 03 | DEL-01, DEL-03 | `IAccountDeletionCatalog` |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| DEL-01 | SIN_EVIDENCIA | Falta integración con Mongo/Redis en contenedores | DEV-API005-04 |
| DEL-02 | SIN_EVIDENCIA | Falta integración con Orders real aislado | DEV-API005-04 |
| DEL-03 | IMPLEMENTADO_PROBADO | CAS e idempotencia cubiertos por unitarias; integración pendiente | DEV-API005-02/04 |
| DEL-04 | IMPLEMENTADO_PROBADO | Gate Identity y revocación reutilizados; seguridad completa pendiente | DEV-API005-04 |
| DEL-05 | SIN_EVIDENCIA | Docker/Testcontainers no disponible en la ejecución actual | DEV-API005-04 |
| DEL-06 | IMPLEMENTADO_PROBADO | Login rechaza estado `Deleted` en servicio existente | DEV-API005-04 |

## Evidencia con nivel y fecha

| Fecha | Nivel (documental/dobles/local real/externa) | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-22 | documental | Aprobación de DEC-003 y contrato API-005 por el propietario | No acredita ejecución |
| 2026-11-22 | dobles | Build Release y unitarias existentes verdes tras implementación | No acredita Mongo/Redis/Testcontainers |
| pendiente | local real | Integración y seguridad completas con Docker | No ejecutar contra producción |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas (si hay) | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| DEC-003 opción B | Aprobación del propietario 2026-11-22 | Sin Supabase Admin; Mongo Deleted + logout global best-effort | El proveedor puede conservar identidad externa habilitada | Resuelta por propietario |
| Reviews/Chat | BR-011, DEC-005 | Dejar `deletedUserRef` documentado sin implementación | Requiere fichas dueñas futuras | Dueños de Reviews/Chat |
| Docker ausente | Entorno local | Testcontainers cuando Docker esté disponible | Impide evidencia de integración/seguridad | Operación local |

## Criterios de preparación para desarrollo

El alcance técnico está aprobado y el código candidato existe. `READY_FOR_IMPLEMENTATION` requiere build, pruebas unitarias y tareas técnicas; `VERIFIED` requiere evidencia específica de integración y seguridad con Mongo/Redis aislados. La implementación no se considera verificada hasta ejecutar esas suites con Docker.
