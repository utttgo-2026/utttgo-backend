# API-036 — Marcar notificación propia como leída · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-036 `PATCH /api/v1/notifications/{notificationId}/read`, Notifications según [trazabilidad](../../../docs/traceability.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

Trazabilidad exige autorización por recurso. Constitución §§5/6: identidad/roles server-side, fallo Redis fail-closed, error con correlationId. BR-008 limita push al backend; marcar lectura no envía FCM. [Migración](../../../docs/migracion-sdd.md) mantiene fase 06 sin contrato aprobado: colección, estado/fecha de lectura, idempotencia, HTTP, cuerpo y retención pendientes.

## Responsabilidad única

Cambiar la marca de lectura de una notificación **perteneciente al usuario autenticado**, sin modificar registros ajenos. No enviar push, no alterar estado de pedido ni historial de chat; GET bandeja corresponde API-035. La operación no confirma recepción FCM ni lectura de chat/pedido.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | API-036, 2026-11-11 | PATCH, propietario; sin fase 06 aprobada. |
| [Constitución](../../../docs/constitution.md) | BR-008/009/011/012, §§4/5/6/8/14 v2.6 | Propiedad, idempotencia push, privacidad y errores. |
| [Fase 07](../../phase-07-chat-realtime/spec.md) | DEC-005, BR-012 | Chat temporal; no conservar contenido por marcar notificación. |

## Contrato aprobado

| Regla | ID/fuente | Límite |
|---|---|---|
| Autorizar por recurso, nunca por rol solamente | [Constitución §6](../../../docs/constitution.md), [trazabilidad API-036](../../../docs/traceability.md) | Código 404/403 para tercero, cuerpo y salida pendientes. |
| FCM únicamente backend, idempotencia de push | BR-008, [Constitución §8](../../../docs/constitution.md) | Marcar leído no es envío ni acuse FCM. |

## Comportamiento implementado candidato

No hay módulo Notifications, tests de notificación, modelo persistente ni endpoint PATCH; API-035 tampoco implementada. Ninguna marca leída demostrada.

## Funcionalidad requerida faltante

Escritor condicional Notifications por `_id` y `recipientUserId` juntos; persistir `readAt`/equivalente solo si la fase 06 lo aprueba. Respuesta repetida sin modificar fecha original si idempotencia ratificada; resolver carrera con purga/expiración y listado API-035. Sin escritura en Users/Orders/Chat ni llamado a FCM. Borrado de cuenta API-005/DEC-003 es otra ficha.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato NO aprobado | Falta decidir |
|---|---|---|---|
| Entrada | PATCH inventariado y JWT del propietario | `notificationId` ObjectId, body vacío | ID/body/estado cuenta y autorización ante notificación inexistente. |
| Éxito | Solo marca del registro propio; no envía push | `200 {notificationId,readAt}` o `204`; update condicional conserva primer readAt | HTTP/DTO, duplicado, fecha UTC, efecto si registro expiró. |
| Error | Contrato único `correlationId`, fail-closed Redis | `400/401/403/404/429/503` candidatos | 404 uniforme tercero/ausente, Redis/Mongo, agotamiento. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). JWT/blacklist, rate limit Redis fail-closed; predicado Mongo `_id+recipientUserId` del perfil local en **misma escritura**, no GET-then-update por ID solo. No confiar en userId del body; evitar fuga de existencia del recurso ajeno. No loguear tokens FCM, contenido sensible o PII; caches de bandeja se invalidan si existen por política aprobada.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| NOTR-01 | Usuario dueño y notificación no leída | PATCH read | Solo su marca cambia; API-035 refleja cambio, sin FCM | API-036 | Integración pendiente | Propiedad vigente; fecha/HTTP pendiente |
| NOTR-02 | Usuario ajeno o ID inexistente | PATCH read | Ningún registro ajeno cambia, sin revelar existencia según acta | API-036, §6 | Seguridad pendiente | HTTP pendiente |
| NOTR-03 | Dos PATCH simultáneos o repetición tras éxito | PATCH read | Una marca estable si política idempotente aprobada | API-036 | Carrera Mongo pendiente | Fecha/replay pendiente |
| NOTR-04 | JWT revocado, Redis/Mongo caídos o registro purgado | PATCH read | Sin bypass, respuesta error/no existente conforme acta, sin resucitar contenido | API-036, BR-012 | Fallos integración pendientes | Retención/HTTP pendientes |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| API-036 (sin RF/AC fase 06 publicados) | [Trazabilidad](../../../docs/traceability.md) | NOTR-01..04 | DEV-API036-01..04 |
| BR-008/009/011/012; DEC-005 | [Constitución](../../../docs/constitution.md) | NOTR-01/02/04 | DEV-API036-02/03/04; API-035/005 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| NOTR-01 | NO_IMPLEMENTADO | Sin modelo/PATCH ni lector API-035 | DEV-API036-02/03 |
| NOTR-02 | NO_IMPLEMENTADO | Falta filtro de propietario y prueba | DEV-API036-02/04 |
| NOTR-03 | BLOQUEADO | Semántica repetición/readAt sin contrato | DEV-API036-01/03 |
| NOTR-04 | OTRA_FICHA | Purga Chat/API-005; degradación Redis y estado propio aquí | DEV-API036-04; API-005/035 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Trazabilidad](../../../docs/traceability.md), Notifications ausente | Marca leída ni seguridad probadas. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad |
|---|---|---|---|---|
| API-036 fase 06 sin spec | [Migración](../../../docs/migracion-sdd.md) | Fecha readAt vs estado boolean, idempotencia, HTTP/DTO, tercero/no existe | DEV-API036-01..04 | Producto/Notifications/seguridad: acta y Excel. |
| DEC-003/005 | [Constitución](../../../docs/constitution.md) | Lectura tras baja/purga, sin contenido residual | NOTR-04, API-005/037 | Privacidad/Chat/Notifications. |

## Criterios de preparación para desarrollo

Alcance [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta bandeja API-035/modelo fase 06 y replay aprobados; NOTR-01..04 requieren pruebas aisladas de propietario, carrera y fallos.
