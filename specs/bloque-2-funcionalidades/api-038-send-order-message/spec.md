# API-038 — Envío de mensaje del pedido · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-038 `POST /api/v1/orders/{orderId}/messages` según [trazabilidad](../../../docs/traceability.md) y [fase 07](../../phase-07-chat-realtime/spec.md) v0.1. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

BR-012 y DEC-005 aprueban la función: chat entre los participantes mientras el pedido está en curso, con purga dentro de tres días de completarse (constitución §§ 3/16); la regla no se reabre. Contrato HTTP, DTO, validación de contenido, límite de mensajes, notificación derivada y almacenamiento **no están aprobados**: la [fase 07](../../phase-07-chat-realtime/spec.md) es borrador v0.1 sin gate y [migración](../../../docs/migracion-sdd.md) registra contratos pendientes. Aprobación parcial funcional no valida HTTP/DTO.

## Responsabilidad única

Enviar un mensaje de un participante al historial del pedido en curso. No lee el historial ([API-037](../api-037-order-messages-list/spec.md)), no modifica estados del pedido, no implementa la purga, no notifica fuera del canal acordado ni permite mensajes de no participantes.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Trazabilidad](../../../docs/traceability.md) | API-038, corte 2026-11-08 | Método/ruta del inventario; DTO y límites pendientes. |
| [Fase 07](../../phase-07-chat-realtime/spec.md) | DEC-005/BR-012, v0.1 2026-11-03 | Función confirmada; borrador sin plan/task ni pruebas. |
| [Constitución](../../../docs/constitution.md) | BR-012, DEC-005, DEC-004, §§ 4/5/6/13 v2.6 | Retención, purga, fronteras, SignalR/FCM y estados. |
| [Migración](../../../docs/migracion-sdd.md) | Fila phase-07-chat-realtime | Diseño de purga/almacenamiento y contratos pendientes. |
| [Detalle de pedido](../api-025-order-detail/spec.md) | API-025 | Autorización por pedido; no copiar su contrato. |

## Contrato aprobado

| Regla | ID/fuente | Límite |
|---|---|---|
| Chat conservado en curso y eliminado ≤ 3 días de completarse | BR-012 y DEC-005 (constitución) | Mecanismo y ventana de purga sin diseñar. |
| Autorización por recurso, nunca por rol solamente | [Constitución § 6](../../../docs/constitution.md) | Código `403`/`404` ante tercero pendiente de acta. |
| Envíos push solo desde backend con FCM (si se notifica) | BR-008, DEC-004 | Notificación de mensajes pendiente de decisión. |

## Comportamiento implementado candidato

No existe `src/UTTTGO.Api/Modules/Chat/` ni repositorio, controlador, SignalR ni pruebas de chat (módulos actuales: Admin, Identity, Sellers). Ningún envío de mensajes está implementado; la lectura (API-037) tampoco.

## Funcionalidad requerida faltante

Contrato HTTP/DTO (autor, contenido, timestamp servidor), validación de longitud/caracteres, límite de tasa por participante, idempotencia ante reintento, entrega multi-instancia (SignalR pendiente en fase 07 v0.1), derivación a notificación si se aprueba, definición de «pedido completado», purga verificable y coordinación DEC-003. Orders es dueño de la participación del pedido.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir |
|---|---|---|---|
| Método/ruta POST del inventario | API-038 (trazabilidad) | Bearer + `{orderId}` + cuerpo con texto | DTO exacto, límites de tamaño |
| Participante envía y queda en el historial | BR-012, § 6 | `201` con mensaje persistido | Código de estado, campo de identidad |
| Tercero o pedido cerrado sin envío | § 6, § 13 | `403`/`404` o `409` uniforme | Código ante pedido completado |
| Fallo externo sin mensaje fantasma | § 6 | `503` sin persistir parcial | Política de reintento |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): JWT/blacklist y rate limit distribuido fail-closed (más estricto por participante); autorización por participación verificada contra el dueño de Orders; sanitización de contenido sin loguearlo; sin HTML/inyección renderizable; error único con `correlationId`; tokens y contenido fuera de logs.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| MSGS-01 | Participante del pedido y pedido en curso | POST messages | `201` con mensaje persistido (candidato) | API-038, BR-012 | Integración pendiente | Función aprobada; HTTP pendiente |
| MSGS-02 | Usuario no participante o pedido ajeno | POST messages | Sin mensaje; `403`/`404` uniforme pendiente | API-038, § 6 | Seguridad pendiente | Código pendiente de acta |
| MSGS-03 | Pedido completado (máquina de estados) | POST messages | Envío rechazado sin escribir (`409` candidato) | BR-012, § 13 | Estados pendientes | HTTP pendiente |
| MSGS-04 | JWT revocado, Redis/Mongo no disponibles o límite de tasa agotado | POST messages | Fail-closed o `429` con error único; sin bypass | API-038, § 6 | Fallos pendientes | Fail-closed constitucional |
| MSGS-05 | Reintento o dos réplicas con el mismo envío | POST messages | Un solo mensaje efectivo; sin duplicados (candidato) | BR-012 | Idempotencia pendiente | DEC-005 técnica |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| API-038 (sin RF/AC fase 07 publicados) | [Trazabilidad](../../../docs/traceability.md) | MSGS-01..05 | DEV-API038-01..04 |
| BR-012, DEC-005, DEC-004 | [Constitución](../../../docs/constitution.md) | MSGS-01/03/04 | DEV-API038-01/03/04 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| MSGS-01 | NO_IMPLEMENTADO | Sin módulo Chat ni contrato HTTP/DTO | DEV-API038-01/02/03 |
| MSGS-02 | BLOQUEADO | Código de respuesta ante tercero sin acta | DEV-API038-01/03 |
| MSGS-03 | BLOQUEADO | Contrato de estado de pedido de Orders sin cerrar | DEV-API038-01/03 |
| MSGS-04 | NO_IMPLEMENTADO | Rate limit por participante y degradación sin probar | DEV-API038-04 |
| MSGS-05 | BLOQUEADO | Idempotencia y entrega multi-instancia sin diseñar | DEV-API038-01/02 |

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

## Criterios de preparación para desarrollo

Alcance [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos): contrato de envío, límites, idempotencia y purga sin aprobar; MSGS-01..05 requieren acta DEC-005 y gate de fase 07 antes de código. Faltan pruebas de envío, tercero, cierre de pedido, fallos e idempotencia.
