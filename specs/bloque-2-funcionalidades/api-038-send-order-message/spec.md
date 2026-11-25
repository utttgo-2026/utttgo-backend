# API-038 — Envío de mensaje del pedido · spec

Estado de aprobación de la ficha: APROBADA explícitamente para API-038
Estado operativo: IMPLEMENTADA EN ESTA RAMA; pendiente verificación CI/PR

## Identificación y autoridad

API-038 `POST /api/v1/orders/{orderId}/messages` según [trazabilidad](../../../docs/traceability.md) y [fase 07](../../phase-07-chat-realtime/spec.md) v0.2. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

BR-012 y DEC-005 aprueban la función: chat entre participantes mientras el pedido está en curso, con purga dentro de tres días de completarse. El contrato ratificado acepta únicamente `{content}` sanitizado de 1..1000 caracteres y exige `Idempotency-Key`.

Contrato HTTP ratificado: `POST /api/v1/orders/{orderId}/messages` devuelve `201`; un tercero recibe `404 ORDER_NOT_FOUND` y un pedido terminal `409 ORDER_CHAT_CLOSED`.

## Responsabilidad única

Enviar un mensaje de un participante al historial del pedido en curso. No lee el historial ([API-037](../api-037-order-messages-list/spec.md)), no modifica estados del pedido, no implementa la purga, no notifica fuera del canal acordado ni permite mensajes de no participantes.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Trazabilidad](../../../docs/traceability.md) | API-038, corte 2026-11-08 | Método/ruta del inventario; DTO y límites pendientes. |
| [Fase 07](../../phase-07-chat-realtime/spec.md) | DEC-005/BR-012, v0.2 2026-11-25 | Contrato ratificado; purga, SignalR y FCM quedan fuera de esta entrega. |
| [Constitución](../../../docs/constitution.md) | BR-012, DEC-005, DEC-004, §§ 4/5/6/13 v2.6 | Retención, purga, fronteras, SignalR/FCM y estados. |
| [Migración](../../../docs/migracion-sdd.md) | Fila phase-07-chat-realtime | Diseño de purga/almacenamiento y contratos pendientes. |
| [Detalle de pedido](../api-025-order-detail/spec.md) | API-025 | Autorización por pedido; no copiar su contrato. |

## Contrato aprobado

| Regla | ID/fuente | Límite |
|---|---|---|
| Chat conservado en curso y eliminado ≤ 3 días de completarse | BR-012 y DEC-005 (constitución) | Purga programada posterior; esta entrega persiste en MongoDB. |
| Autorización por recurso, nunca por rol solamente | [Constitución § 6](../../../docs/constitution.md) | `404 ORDER_NOT_FOUND` ante tercero. |
| Envíos push solo desde backend con FCM (si se notifica) | BR-008, DEC-004 | Notificación de mensajes pendiente de decisión. |

## Comportamiento implementado candidato

El envío está implementado en `src/UTTTGO.Api/Modules/Chat/` con persistencia Mongo, idempotencia por clave y autorización mediante Orders/Identity. SignalR/FCM y la purga programada quedan fuera de esta entrega.

## Funcionalidad requerida faltante

SignalR/FCM, purga verificable y coordinación DEC-003 quedan pendientes. Orders es dueño de la participación y del estado del pedido.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir |
|---|---|---|---|
| Método/ruta POST del inventario | API-038 (trazabilidad) | Bearer + `{orderId}` + `{content}` + `Idempotency-Key` | Aprobado |
| Participante envía y queda en el historial | BR-012, § 6 | `201` con `id,authorId,content,createdAt` | Aprobado |
| Tercero o pedido cerrado sin envío | § 6, § 13 | `404 ORDER_NOT_FOUND` o `409 ORDER_CHAT_CLOSED` | Aprobado |
| Fallo externo sin mensaje fantasma | § 6 | `503` sin persistir parcial | Política de reintento |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): JWT/blacklist y rate limit distribuido fail-closed (más estricto por participante); autorización por participación verificada contra el dueño de Orders; sanitización de contenido sin loguearlo; sin HTML/inyección renderizable; error único con `correlationId`; tokens y contenido fuera de logs.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| MSGS-01 | Participante del pedido y pedido en curso | POST messages | `201` con mensaje persistido | API-038, BR-012 | Pruebas de endpoint | Aprobado |
| MSGS-02 | Usuario no participante o pedido ajeno | POST messages | `404 ORDER_NOT_FOUND`, sin escritura | API-038, § 6 | Pruebas de seguridad | Aprobado |
| MSGS-03 | Pedido completado (máquina de estados) | POST messages | `409 ORDER_CHAT_CLOSED`, sin escribir | BR-012, § 13 | Pruebas de integración | Aprobado |
| MSGS-04 | JWT revocado, Redis/Mongo no disponibles o límite de tasa agotado | POST messages | Fail-closed o `429` con error único; sin bypass | API-038, § 6 | Pruebas de fallos | Aprobado |
| MSGS-05 | Reintento o dos réplicas con el mismo envío | POST messages | Un solo mensaje efectivo; conflicto si cambia contenido | BR-012 | Pruebas unitarias/repositorio | Aprobado |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| API-038 (sin RF/AC fase 07 publicados) | [Trazabilidad](../../../docs/traceability.md) | MSGS-01..05 | DEV-API038-01..04 |
| BR-012, DEC-005, DEC-004 | [Constitución](../../../docs/constitution.md) | MSGS-01/03/04 | DEV-API038-01/03/04 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| MSGS-01 | IMPLEMENTADO | Módulo Chat, contrato y repositorio Mongo | DEV-API038-02/03 |
| MSGS-02 | IMPLEMENTADO | Participación por contrato Orders y `404` uniforme | DEV-API038-03/04 |
| MSGS-03 | IMPLEMENTADO | Estados terminales rechazados sin escritura | DEV-API038-03/04 |
| MSGS-04 | IMPLEMENTADO | Rate limit Redis y fail-closed | DEV-API038-04 |
| MSGS-05 | IMPLEMENTADO | Índice único e idempotencia por participante | DEV-API038-02/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-03 | Documental | [Fase 07](../../phase-07-chat-realtime/spec.md) v0.1: borrador funcional de DEC-005 | Contrato, código ni pruebas. |
| 2026-11-11 | Documental | [Trazabilidad](../../../docs/traceability.md) fila API-038 | Implementación ni verificación. |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| DEC-005 técnica | [Constitución](../../../docs/constitution.md), fase 07 pendientes 1–3 | Almacén y transacción de escritura; idempotencia por clave; purga por evento o programado | DEV-API038-01..04 | Equipo Chat/Orders: diseño y acta; sin contradecir BR-012. |
| Notificación de mensajes | [Constitución](../../../docs/constitution.md) DEC-004 | Derivar a FCM, solo lectura web, o ninguna | DEV-API038-01/04 | Negocio/Chat: decisión explícita; BR-008 si hay push. |

## Criterios de cierre técnico

Contrato de envío, límites, idempotencia, autorización y cierre de pedido están aprobados para esta entrega. SignalR/FCM, purga y DEC-003 requieren trabajo posterior.
