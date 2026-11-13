# API-026 — Cancelar pedido propio · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-026 `POST /api/v1/orders/{orderId}/cancel`, módulo `Orders`, según [trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) y [borrador fase 04](../../phase-04-orders-cash/spec.md) v0.1. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

Constitución § 13 confirma `Pending → CancelledByBuyer` y `409` en transición inválida. `Accepted → CancelledByBuyer` **solo si equipo lo aprueba** (D-ORD-07 pendiente; no habilitar). Liberación stock idempotente sin sobreventa es regla funcional, mecanismo D-ORD-02 pendiente. HTTP/repetición `200`, CAS D-ORD-06, `completedAt` D-ORD-08, auditoría y notificación son propuestas v0.1, no aprobación de fase 04.

## Responsabilidad única

Permitir al comprador del pedido cancelar una operación elegible y coordinar liberación **una sola vez** de la reserva en Catalog. No cancela por vendedor (API-027 rechaza), no entrega (API-029), no elimina pedido ni devuelve dinero/pago online.

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-001/002/003/009/010/012, § 13/14 | Comprador cancela Pending, efectivo presencial, `409` inválida. |
| [Fase 04 spec](../../phase-04-orders-cash/spec.md), [plan](../../phase-04-orders-cash/plan.md), [tareas](../../phase-04-orders-cash/task.md) | RF-ORD-07/10/15/17/18/19/20/26/27/29/32; T-ORD-05/06; TASK-ORD-010/011/012/022/026, D-ORD-02/06/07/08/09 | Borrador sin aprobación ni tests; flujo de stock pendiente. |
| [Trazabilidad](../../../docs/traceability.md) | API-026 | Ruta y responsabilidad de cancelación. |

## Contrato aprobado

| Regla | Autoridad | Límite |
|---|---|---|
| Solo comprador cancela `Pending`; transiciones inválidas `409` | [Constitución § 13](../../../docs/constitution.md) | `Accepted` prohibido sin aprobación explícita. |
| Reserva se libera idempotentemente al cancelar y no hay sobreventa | BR-010, [fase 03 RF-10](../../phase-03-catalog-offers/spec.md) | Protocolo Catalog D-ORD-02 abierto. |

## Comportamiento implementado candidato

No existe `Modules/Orders`, controlador POST cancel, `orders`, `orderAuditLogs`, tests ni integración de reserva Catalog. La documentación histórica v0.1 no acredita comportamiento. Notificaciones solo desde Notifications/backend FCM, nunca desde cliente.

## Funcionalidad requerida faltante

Autorizar por `buyerUserId` y estado, escritura condicional sin doble transición/stock, liberación idempotente con contrato `ICatalogProductCommands` de Catalog, auditoría inmutable Orders y `completedAt` propuesto como ancla Chat (BR-012); notificar vía interfaz Notifications cuando exista, sin bloquear transición por FCM externo según política por aprobar. Fallos parciales requieren reconciliación para no dejar cancelado con reserva activa ni reabrir stock dos veces.

## Contrato de entrada/salida

| Elemento | Aprobado | Propuesta v0.1 NO aprobada | Falta decidir |
|---|---|---|---|
| Entrada | JWT comprador del pedido; orderId ruta, sin importe/estado de pago cliente | POST sin cuerpo; ObjectId string, rate limit `order-cancel` 10/60 s | Formato ID, version precondición, body y partición rate limit. |
| Efecto | Pending → CancelledByBuyer; stock liberado una vez | CAS/version, `completedAt` UTC, audit log, notificación; repetición ya cancelado `200` | Orden de CAS/liberación/auditoría, fallos/crash, reintento y D-ORD-08. |
| Salida | Error único `correlationId`, transición inválida `409` | `200 {orderId,status,completedAt,version}`; `401`, `403 ORDER_FORBIDDEN` al participante incorrecto, `404 ORDER_NOT_FOUND` tercero, `409 ORDER_STATE_CONFLICT`, `429`, `503` | HTTP/idempotencia exactos; 404/403 por rol y 409 de CAS D-ORD-06. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Validar JWT/blacklist, comparar comprador con `buyerUserId` persistido, no solo rol Buyer; vendedor/no participante sin acceso. Rate limit Redis fail-closed. `Pending` únicamente salvo decisión D-ORD-07; escritura CAS y clave reserva para liberación idempotente definida por Catalog. Error con `correlationId`, no tokens/PII/stock sensible en logs; no cache de Orders. Datos compartidos tras borrado API-005/DEC-003 no se resuelven aquí.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| CAN-01 | Comprador propio con pedido Pending y reserva | POST cancel | `CancelledByBuyer`; stock liberado una vez; auditoría/fecha según acta | RF-ORD-19, T-ORD-05 | Integración por crear | Transición aprobada, D-ORD-02/08 pendientes |
| CAN-02 | Pedido ya CancelledByBuyer, dos reintentos concurrentes | POST cancel | Sin doble liberación; HTTP idempotente propuesto | RF-ORD-19, T-ORD-06 | Carrera Mongo/Catalog por crear | HTTP/CAS pendientes |
| CAN-03 | Pedido Accepted, Ready o Delivered | POST cancel | `409` sin mutación; Accepted no habilitado sin decisión | RF-ORD-20, § 13 | Unit por crear | Transición constitucional |
| CAN-04 | Vendedor/tercero, JWT revocado o Redis caído | POST cancel | Sin cambio ni fuga de pedido, error de rol/existencia ratificado | RF-ORD-15/27 | Seguridad por crear | HTTP pendiente |
| CAN-05 | Catalog libera pero Mongo/auditoría falla, o viceversa | POST/reintento | No sobreventa ni reserva colgada, recuperación según protocolo | RF-ORD-32, D-ORD-02 | Integración fallos por crear | Consistencia pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| RF-ORD-07/10/15/17/18/19/20/26/27/29/32; T-ORD-05/06 | [Fase 04](../../phase-04-orders-cash/spec.md) | CAN-01/02/03/04/05 | DEV-API026-01/02/03/04/05 |
| TASK-ORD-001..004/010/011/012/022/026, D-ORD-02/06/07/08/09 | [Tareas fase 04](../../phase-04-orders-cash/task.md), [spec](../../phase-04-orders-cash/spec.md) | CAN-01/02/03/05 | DEV-API026-01/02/03/04; API-005 |
| BR-008/010/012, § 13 | [Constitución](../../../docs/constitution.md) | CAN-01/03 | DEV-API026-02/03 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| CAN-01 | NO_IMPLEMENTADO | Sin ruta, estado/auditoría ni comandos Catalog | DEV-API026-02/03 |
| CAN-02 | BLOQUEADO | CAS y replay D-ORD-06/02, HTTP no aprobados | DEV-API026-01/03/04 |
| CAN-03 | BLOQUEADO | Accepted requiere aprobación; Pending permitido pero sin código | DEV-API026-01/02 |
| CAN-04 | NO_IMPLEMENTADO | Sin seguridad/fallo Redis | DEV-API026-04 |
| CAN-05 | BLOQUEADO | Protocolo fallos parciales sin decisión | DEV-API026-01/03/05 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Fase 04 v0.1](../../phase-04-orders-cash/spec.md), Orders/Catalog ausentes | Cancelación ni liberación real, aprobación o auditoría. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Decisión/autoridad |
|---|---|---|---|---|
| D-ORD-02/06/07/08, TASK-ORD-001..004 | [Fase 04 § 9](../../phase-04-orders-cash/spec.md), [tareas](../../phase-04-orders-cash/task.md) | Orden CAS/liberar/reintento; Accepted requiere aprobación expresa, no default; completedAt | DEV-API026-01/02/03/04/05 | Propietario/Catalog/Orders/seguridad aprueban protocolo y fase 04/Sprint 2. |
| D-ORD-09/DEC-003 | [Constitución](../../../docs/constitution.md) | Pedidos compartidos tras borrar cuenta | API-005, no cancelación | Dueños de privacidad/Identity/Orders; no bloquear documentación. |

## Criterios de preparación para desarrollo

Alcance: cancelación comprador Pending, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta acta fase 04, protocolo stock/CAS y Catalog Sprint 2. Cierre: stock liberado una sola vez aun bajo carreras/fallos, autorización por pedido, `409` fuera Pending, evidencia local fechada.
