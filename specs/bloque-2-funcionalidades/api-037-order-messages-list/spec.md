# API-037 — Historial de mensajes del pedido · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-037 `GET /api/v1/orders/{orderId}/messages` según [trazabilidad](../../../docs/traceability.md) y [fase 07](../../phase-07-chat-realtime/spec.md) v0.1. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

BR-012 y DEC-005 aprueban la función: el historial se conserva mientras el pedido está en curso y se elimina dentro de tres días de completarse (constitución §§ 3/16); esa regla no se reabre. Contrato HTTP, DTO, paginación, almacenamiento y disparador de purga **no están aprobados**: la [fase 07](../../phase-07-chat-realtime/spec.md) es borrador v0.1 con gate `spec → plan → task` sin abrir y [migración](../../../docs/migracion-sdd.md) registra contratos pendientes. Aprobación parcial funcional no valida HTTP/DTO.

## Responsabilidad única

Leer el historial de mensajes de un pedido a sus participantes mientras el pedido está en curso. No envía mensajes ([API-038](../api-038-send-order-message/spec.md)), no modifica el pedido, no implementa la purga ni expone mensajes de pedidos ajenos.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Trazabilidad](../../../docs/traceability.md) | API-037, corte 2026-11-08 | Método/ruta del inventario; almacenamiento y purga pendientes. |
| [Fase 07](../../phase-07-chat-realtime/spec.md) | DEC-005/BR-012, v0.1 2026-11-03 | Función confirmada; borrador sin plan/task ni pruebas. |
| [Constitución](../../../docs/constitution.md) | BR-012, DEC-005, §§ 4/5/6/13 v2.6 | Retención, purga, fronteras, seguridad y estados de pedido. |
| [Migración](../../../docs/migracion-sdd.md) | Fila phase-07-chat-realtime | Diseño de purga/almacenamiento y contratos pendientes. |
| [Detalle de pedido](../api-025-order-detail/spec.md) | API-025 | Autorización por pedido; no copiar su contrato. |

## Contrato aprobado

| Regla | ID/fuente | Límite |
|---|---|---|
| Historial temporal: conservar en curso, eliminar ≤ 3 días de completarse | BR-012 y DEC-005 (constitución) | Disparador y mecanismo de purga pendientes de diseño. |
| Autorización por recurso, nunca por rol solamente | [Constitución § 6](../../../docs/constitution.md) | Código `403`/`404` ante tercero pendiente de acta. |

## Comportamiento implementado candidato

No existe `src/UTTTGO.Api/Modules/Chat/` ni colección, repositorio, controlador o pruebas de chat (módulos actuales: Admin, Identity, Sellers). Ninguna lectura de mensajes está implementada; API-038 tampoco.

## Funcionalidad requerida faltante

Almacenamiento duradero multi-instancia, paginación, definición de «pedido completado» (fase 07 pendiente 1), ancla de tres días y purga verificable (pendientes 2 y 3), coordinación DEC-003 con cuenta eliminada (pendiente 4) y contrato HTTP/DTO/errores. Orders es dueño de la participación del pedido; Chat no consulta colecciones de Orders.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir |
|---|---|---|---|
| Método/ruta GET del inventario | API-037 (trazabilidad) | Bearer + `{orderId}` + paginación por query | Parámetros exactos y límites |
| Participante autorizado ve su historial | BR-012, § 6 | `200` paginado con mensajes | DTO de mensaje, orden y página |
| Tercero o pedido ajeno sin contenido | § 6 | `403` o `404` uniforme | Código y regla de no-divulgación |
| Cierre o purga sin residual | BR-012, DEC-005 | Sin contenido tras purga | Disparador, ventana y lectura tras cierre |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md): JWT/blacklist y rate limit distribuido fail-closed; autorización por participación en el pedido (comprador/vendedor) verificada mediante contrato del dueño de Orders, nunca por rol ni por parámetro; sin mensajes de otros pedidos; error único con `correlationId`; no loguear contenido de mensajes ni tokens; sin caché residual tras purga.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| MSGL-01 | Participante del pedido y pedido en curso | GET messages | `200` con historial paginado (candidato) | API-037, BR-012 | Integración pendiente | Función aprobada; HTTP pendiente |
| MSGL-02 | Usuario no participante o pedido ajeno | GET messages | Sin mensajes; `403`/`404` uniforme pendiente | API-037, § 6 | Seguridad pendiente | Código pendiente de acta |
| MSGL-03 | Pedido completado con la ventana de tres días vencida o purga aplicada | GET messages | Sin contenido residual | BR-012, DEC-005 | Purga pendiente de diseño | Mecanismo pendiente DEC-005 |
| MSGL-04 | JWT revocado o Redis/Mongo no disponibles | GET messages | Fail-closed sin bypass | API-037, § 6 | Fallos pendientes | Fail-closed constitucional |
| MSGL-05 | Reinicio o dos réplicas con mensajes escritos | GET messages | Historial consultable sin pérdida (candidato) | BR-012 | Almacenamiento pendiente | DEC-005 técnica |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| API-037 (sin RF/AC fase 07 publicados) | [Trazabilidad](../../../docs/traceability.md) | MSGL-01..05 | DEV-API037-01..04 |
| BR-012, DEC-005 | [Constitución](../../../docs/constitution.md) | MSGL-01/03/04 | DEV-API037-01/02/04 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| MSGL-01 | NO_IMPLEMENTADO | Sin módulo Chat ni contrato HTTP/DTO | DEV-API037-01/03 |
| MSGL-02 | BLOQUEADO | Código de respuesta ante tercero sin acta | DEV-API037-01/03 |
| MSGL-03 | BLOQUEADO | Disparador y ventana de purga sin diseñar (DEC-005) | DEV-API037-01/02 |
| MSGL-04 | NO_IMPLEMENTADO | Guard y degradación sin implementar ni probar | DEV-API037-04 |
| MSGL-05 | BLOQUEADO | Almacenamiento duradero por decidir | DEV-API037-01/02 |

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

## Criterios de preparación para desarrollo

Alcance [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos): contrato de mensajes, almacén y purga sin aprobar; MSGL-01..05 requieren acta DEC-005 y gate de fase 07 antes de código. Faltan pruebas de lectura, tercero, fallos y purga.
