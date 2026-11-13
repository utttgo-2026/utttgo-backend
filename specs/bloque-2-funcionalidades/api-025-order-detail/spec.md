# API-025 — Consultar detalle de pedido · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-025 `GET /api/v1/orders/{orderId}`, módulo `Orders`, según [trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) y [fase 04](../../phase-04-orders-cash/spec.md) v0.1 (borrador). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo Excel directo pendiente de fuente.

## Estado de aprobación

Propiedad por recurso y error sin divulgación rigen por constitución § 6/14. El borrador v0.1 propone detalle solo al comprador o vendedor participante y `404` al tercero (RF-ORD-09/15, T-ORD-08). DTO, exposición del buyerUserId al vendedor, redacción de datos al borrar cuenta, HTTP/ratelimit siguen pendientes de aprobación TASK-ORD-001..004 y D-ORD-09/10.

## Responsabilidad única

Leer un pedido individual por participante autorizado sin exponer pedidos ajenos. No lista (API-024), no cancela (API-026), no cambia estado (API-027), no entrega token (API-028) ni confirma cobro (API-029).

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-001/002/003/009/010/011, § 5/6/13/14 | Pago Cash, propiedad por recurso y datos compartidos por diseñar. |
| [Fase 04 spec](../../phase-04-orders-cash/spec.md), [tareas](../../phase-04-orders-cash/task.md) | RF-ORD-09/11/15/27/31/32, T-ORD-08, TASK-ORD-010/013/021/026, D-ORD-09/10 | DTO/HTTP propuestos no aprobados. |
| [Trazabilidad](../../../docs/traceability.md) | API-025 | GET participante, ruta inventariada. |

## Contrato aprobado

| Regla | Fuente expresa | Límite |
|---|---|---|
| Un pedido corresponde a un producto/una venta en efectivo | BR-001/002/003 | No fija campos de DTO de detalle. |
| JWT/blacklist y autorización por recurso; evitar revelar pedidos ajenos | [Constitución § 6/14](../../../docs/constitution.md) | HTTP 404 para tercero es propuesta v0.1 compatible, requiere acta. |

## Comportamiento implementado candidato

No existe `src/UTTTGO.Api/Modules/Orders/`, `OrderDocument`, GET detalle ni tests de Orders. Borrador fase 04 redactado sin validación funcional ni aprobación; los GET de Identity/Sellers no exponen pedidos.

## Funcionalidad requerida faltante

Lector Orders que busque por `_id` y confirme `buyerUserId` del perfil Identity o `sellerId` del vendedor propio vía contrato Sellers, devuelva DTO por actor sin documento Mongo ni PII indebida. `IOrderReader` futuro para Chat/Admin es responsabilidad dueña Orders distinta de GET público; no crear acceso cross-module. Tratamiento de datos compartidos en API-005/DEC-003 sigue independiente.

## Contrato de entrada/salida

| Elemento | Aprobado | Propuesta v0.1 NO aprobada | Falta decidir |
|---|---|---|---|
| Entrada | Bearer válido y `orderId` de la ruta; propiedad verificada por servidor | ObjectId como string | Formato de ID y código ante inválido, autorización vendedor, cuenta desactivada. |
| Éxito | Datos del pedido propio, sin Mongo directo | `200` con orderId, productId/name, sellerId, buyerUserId, quantity, unitPrice/total decimal MXN, currency, Cash, status, pickupPoint, fechas y version | Exposición buyerUserId/pickupPoint por actor y tras baja de cuenta; snapshot D-ORD-01. |
| Error | Único `correlationId`, rate limit fail-closed | `401`, `404 ORDER_NOT_FOUND` para ausente/tercero, `429`, `503 ORDER_UNAVAILABLE` | HTTP para ID malformado y fallo en perfil/Sellers. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). No confiar en buyer/seller del query; identidad JWT/rol local, filtro por participante real en Orders, no filtrar solo por rol; ocultar existencia a tercero; no devolver deliveryTokenHash, token, `reservationId`, auditoría ni documento Mongo. No cachear pedidos, Redis rate limit fail-closed; logs sin PII/identificadores secretos.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| DETAIL-01 | Comprador dueño y pedido existente | GET por orderId | Solo detalle propio con Cash/total snapshot si ratificado | RF-ORD-09/31 | Integración por crear | DTO pendiente |
| DETAIL-02 | Vendedor dueño y pedido del producto propio | GET por orderId | Solo información de pedido que necesita, sin datos ajenos adicionales | RF-ORD-09/15 | Seguridad por crear | Campos por actor pendientes |
| DETAIL-03 | Usuario tercero o pedido inexistente | GET | `404` candidato indistinguible, sin fuga de existencia | RF-ORD-15, T-ORD-08 | Seguridad por crear | HTTP por ratificar |
| DETAIL-04 | JWT revocado, rate limit Redis caído o pedido afectado por borrado cuenta | GET | Sin lectura no autorizada; política de datos compartidos pendiente | RF-ORD-27/32, D-ORD-09 | Integración por crear | DEC-003 pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| RF-ORD-09/11/15/27/31/32, T-ORD-08 | [Fase 04](../../phase-04-orders-cash/spec.md) | DETAIL-01/02/03/04 | DEV-API025-01/02/03/04 |
| TASK-ORD-001..004/010/013/021/026, D-ORD-09/10 | [Tareas fase 04](../../phase-04-orders-cash/task.md), [spec](../../phase-04-orders-cash/spec.md) | DETAIL-01/03/04 | DEV-API025-01/02/04; API-005 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| DETAIL-01 | NO_IMPLEMENTADO | Sin repositorio/DTO/GET Orders | DEV-API025-02 |
| DETAIL-02 | BLOQUEADO | Contrato de exposición al Seller por ratificar | DEV-API025-01/03 |
| DETAIL-03 | NO_IMPLEMENTADO | Sin guard propietario/404 ni prueba | DEV-API025-02/03 |
| DETAIL-04 | OTRA_FICHA | Borrado DEC-003; falla Redis y seguridad faltan aquí | DEV-API025-04; API-005 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Fase 04 v0.1](../../phase-04-orders-cash/spec.md), Orders ausente | DTO aprobado ni lectura real segura. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Decisión/autoridad |
|---|---|---|---|---|
| TASK-ORD-001..004, D-ORD-01/10 | [Fase 04](../../phase-04-orders-cash/spec.md) | Snapshot/datos de actor y HTTP 404 vs 403 para tercero | DEV-API025-01/02/03/04 | Producto/seguridad/Orders ratifican DTO, gate Catalog. |
| D-ORD-09/DEC-003 | [Constitución](../../../docs/constitution.md) | Retención/redacción de pedidos compartidos tras borrado | API-005, DETAIL-04 | Producto/privacidad deciden; no inventar política en GET. |

## Criterios de preparación para desarrollo

Alcance: GET detalle de participante, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta acta Orders y modelo API-023. Aceptación: comprador/vendedor propios, tercero indistinguible de ausente, DTO mínimo, sin tokens ni PII innecesaria.
