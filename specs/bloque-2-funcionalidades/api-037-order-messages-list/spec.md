# API-037 — Historial de mensajes del pedido · spec

Estado de aprobación de la ficha: APROBADA explícitamente para API-037
Estado operativo: IMPLEMENTADA EN ESTA RAMA; pendiente verificación CI/PR

## Identificación y autoridad

API-037 `GET /api/v1/orders/{orderId}/messages` según [trazabilidad](../../../docs/traceability.md) y [fase 07](../../phase-07-chat-realtime/spec.md) v0.2. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

BR-012 y DEC-005 aprueban la función: el historial se conserva mientras el pedido está en curso y se elimina dentro de tres días de completarse. El contrato ratificado usa `page/pageSize` (máximo 50), respuesta `items/page/pageSize/totalCount`, orden UTC ascendente y `404 ORDER_NOT_FOUND` para no participantes.

Contrato HTTP ratificado: `GET /api/v1/orders/{orderId}/messages?page=1&pageSize=20` devuelve `200`; requiere Bearer y limita el acceso al comprador o vendedor asignado.

## Responsabilidad única

Leer el historial de mensajes de un pedido a sus participantes mientras el pedido está en curso. No envía mensajes ([API-038](../api-038-send-order-message/spec.md)), no modifica el pedido, no implementa la purga ni expone mensajes de pedidos ajenos.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Trazabilidad](../../../docs/traceability.md) | API-037, corte 2026-11-08 | Método/ruta del inventario; almacenamiento y purga pendientes. |
| [Fase 07](../../phase-07-chat-realtime/spec.md) | DEC-005/BR-012, v0.2 2026-11-25 | Contrato ratificado; purga, SignalR y FCM quedan fuera de esta entrega. |
| [Constitución](../../../docs/constitution.md) | BR-012, DEC-005, §§ 4/5/6/13 v2.6 | Retención, purga, fronteras, seguridad y estados de pedido. |
| [Migración](../../../docs/migracion-sdd.md) | Fila phase-07-chat-realtime | Diseño de purga/almacenamiento y contratos pendientes. |
| [Detalle de pedido](../api-025-order-detail/spec.md) | API-025 | Autorización por pedido; no copiar su contrato. |

## Contrato aprobado

| Regla | ID/fuente | Límite |
|---|---|---|
| Historial temporal: conservar en curso, eliminar ≤ 3 días de completarse | BR-012 y DEC-005 (constitución) | Purga programada posterior; esta entrega persiste en MongoDB. |
| Autorización por recurso, nunca por rol solamente | [Constitución § 6](../../../docs/constitution.md) | `404 ORDER_NOT_FOUND` ante tercero. |

## Comportamiento implementado candidato

La lectura está implementada en `src/UTTTGO.Api/Modules/Chat/` con MongoDB y el contrato de participación publicado por Orders. La purga programada posterior a `completedAt` queda fuera de esta entrega.

## Funcionalidad requerida faltante

Purga verificable dentro de tres días y coordinación DEC-003 con cuenta eliminada quedan pendientes. Orders es dueño de la participación del pedido; Chat no consulta colecciones de Orders.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir |
|---|---|---|---|
| Método/ruta GET del inventario | API-037 (trazabilidad) | Bearer + `{orderId}` + `page/pageSize` (1..50) | Aprobado |
| Participante autorizado ve su historial | BR-012, § 6 | `200` con `{items,page,pageSize,totalCount}` | Aprobado |
| Tercero o pedido ajeno sin contenido | § 6 | `404 ORDER_NOT_FOUND` uniforme | Aprobado |
| Cierre o purga sin residual | BR-012, DEC-005 | Sin contenido tras purga | Disparador, ventana y lectura tras cierre |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): JWT/blacklist y rate limit distribuido fail-closed; autorización por participación en el pedido (comprador/vendedor) verificada mediante contrato del dueño de Orders, nunca por rol ni por parámetro; sin mensajes de otros pedidos; error único con `correlationId`; no loguear contenido de mensajes ni tokens; sin caché residual tras purga.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| MSGL-01 | Participante del pedido y pedido en curso | GET messages | `200` con historial paginado | API-037, BR-012 | Pruebas de endpoint | Aprobado |
| MSGL-02 | Usuario no participante o pedido ajeno | GET messages | `404 ORDER_NOT_FOUND`, sin contenido | API-037, § 6 | Pruebas de seguridad | Aprobado |
| MSGL-03 | Pedido completado con la ventana de tres días vencida o purga aplicada | GET messages | Sin contenido residual | BR-012, DEC-005 | Purga pendiente de diseño | Mecanismo pendiente DEC-005 |
| MSGL-04 | JWT revocado o Redis/Mongo no disponibles | GET messages | Fail-closed sin bypass | API-037, § 6 | Pruebas de seguridad/infra | Aprobado |
| MSGL-05 | Reinicio o dos réplicas con mensajes escritos | GET messages | Historial consultable sin pérdida | BR-012 | Repositorio Mongo | Aprobado |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| API-037 (sin RF/AC fase 07 publicados) | [Trazabilidad](../../../docs/traceability.md) | MSGL-01..05 | DEV-API037-01..04 |
| BR-012, DEC-005 | [Constitución](../../../docs/constitution.md) | MSGL-01/03/04 | DEV-API037-01/02/04 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| MSGL-01 | IMPLEMENTADO | Módulo Chat, contrato y repositorio Mongo | DEV-API037-02/03 |
| MSGL-02 | IMPLEMENTADO | Participación por contrato Orders y `404` uniforme | DEV-API037-03/04 |
| MSGL-03 | BLOQUEADO | Disparador y ventana de purga sin diseñar (DEC-005) | DEV-API037-01/02 |
| MSGL-04 | IMPLEMENTADO | Guard Identity, rate limit Redis y fail-closed | DEV-API037-04 |
| MSGL-05 | IMPLEMENTADO | Almacenamiento Mongo multi-instancia | DEV-API037-02 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-03 | Documental | [Fase 07](../../phase-07-chat-realtime/spec.md) v0.1: borrador funcional de DEC-005 | Contrato, código ni pruebas. |
| 2026-11-11 | Documental | [Trazabilidad](../../../docs/traceability.md) fila API-037 | Implementación ni verificación. |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| DEC-005 técnica | [Constitución](../../../docs/constitution.md), fase 07 pendientes 1–3 | Almacenamiento Mongo/Redis/proceso; disparador por evento o programado; ventana en UTC | DEV-API037-01..04 | Equipo Chat/Orders: diseño y acta; no contradice BR-012. |
| DEC-003 vs chat | [Constitución](../../../docs/constitution.md) | Retención tras baja de cuenta con historial compartido | DEV-API037-04 | Privacidad/Chat: política aprobada; no inventar excepción a BR-011. |

## Criterios de cierre técnico

Contrato, almacén, autorización y pruebas de lectura quedan aprobados para esta entrega. La purga de tres días y DEC-003 siguen como trabajo posterior.
