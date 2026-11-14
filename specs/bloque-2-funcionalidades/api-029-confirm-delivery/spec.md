# API-029 — Confirmar entrega y efectivo · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-029 `POST /api/v1/orders/{orderId}/delivery-confirmations`, Orders según [trazabilidad](../../../docs/traceability.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

§13 constitucional: `Ready→Delivered`, `Delivered` significa efectivo presencial recibido; no se permite marcar entregado sin comprobar pago. Token de comprador, uso único, commit de stock, auditoría, hecho Reviews y códigos son [propuestas fase 04 v0.1](../../phase-04-orders-cash/spec.md) no aprobadas. TASK-ORD-001..004 y D-ORD-02/03/06/08/10 abiertos; no confundir BR-007 (imágenes) con token de entrega.

## Responsabilidad única

Vendedor propietario confirma recepción presencial y entrega del pedido `Ready` mediante secreto válido del comprador (si se aprueba D-ORD-03). Es el único candidato para establecer `Delivered`; no emitir token (API-028), no modificar estado por API-027, no crear reseña (API-030), no realizar cobros electrónicos.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-002/003/008/009/010/012, §§4/5/6/13/14, v2.6 | Efectivo recibido al entregar, propiedad, idempotencia de confirmación. |
| [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | API-029, 2026-11-11 | Ruta y propósito; fase no implementada. |
| [Fase 04 spec](../../phase-04-orders-cash/spec.md), [plan](../../phase-04-orders-cash/plan.md), [task](../../phase-04-orders-cash/task.md) | RF-ORD-24/25/26/27/29/32; T-ORD-14/15/16; TASK-ORD-025/030..032, v0.1 | Token, CAS, auditoría y hechos propuestos, no aprobados. |

## Contrato aprobado

| Regla | ID/fuente de aprobación expresa | Límite |
|---|---|---|
| `Ready→Delivered` y efectivo recibido al entregar; inválidas `409` | [Constitución §13](../../../docs/constitution.md), BR-003 | Exclusividad de esta ruta y token están pendientes D-ORD-03. |
| Confirmación segura al reintentar; rol/propiedad server-side | [Constitución §§5/6](../../../docs/constitution.md), BR-009 | HTTP exacto y política de replay no fijados. |

## Comportamiento implementado candidato

No existen `Modules/Orders`, `Modules/Reviews`, tests de entrega, comando Catalog ni Notifications. El flujo fase 04 es documental.

## Funcionalidad requerida faltante

Orden de efectos recuperable: verificar vendedor, estado, token hash/expiración/uso; consumir una sola vez junto con transición condicional en `orders`, confirmar reserva en Catalog por interfaz idempotente D-ORD-02, auditar en Orders, notificar vía Notifications y exponer hecho autorizado a Reviews mediante contrato acíclico aprobado. Si falla cualquier efecto después de la transición, reintentar/reconciliar sin doble commit ni pérdida del hecho de entrega; no afirmar transacción multi-módulo.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente NO aprobado | Falta decidir |
|---|---|---|---|
| Entrada | POST inventariado; vendedor del pedido; `Ready`, pago Cash presencial | `{token}` generado API-028, nunca identidad/pago declarados por cliente | Formato ID/token, evidencia de cobro y aceptación D-ORD-03. |
| Éxito | Delivered solo tras efectivo recibido | `200 {orderId,status,deliveredAt,version}`; hash consumido con CAS, `completedAt` UTC, commit stock, auditoría, Notifications y hecho Reviews | Orden, reconciliación/replay D-ORD-02/06/08; entrega hecho exactamente una vez. |
| Error | Transición inválida `409`; único `correlationId`, seguridad fail-closed | `400`, `401`, `403`, `404`, `409 ORDER_STATE_CONFLICT/DELIVERY_TOKEN_ALREADY_USED`, `422 DELIVERY_TOKEN_INVALID/EXPIRED`, `429` 20/60 s, `503` | HTTP/códigos/obfuscación y token expirado/consumido. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). JWT/blacklist y vendedor propietario por perfil Sellers, rate limit distribuido fail-closed; no exponer token/hashed token en logs, auditoría ni error. Comparar hash constante cuando aplicable, validar expiración UTC server-side, update condicional Ready+hash vigente+no usado+versión si aprobada; nunca entregar doble ni confirmar reserva dos veces. Notificaciones backend-only y Reviews sin acceso directo a `orders`.

## Escenarios Given/When/Then

| ID local | Dado (precondiciones) | Cuando (acción) | Entonces (resultado) | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| DEL-01 | Ready, vendedor propio, efectivo recibido y token vigente | POST confirmación | Delivered, fechas, token consumido, stock confirmado una vez; hecho Reviews propuesto | T-ORD-14 | Integración pendiente | §13 vigente, token/protocolo pendiente |
| DEL-02 | Token ya usado o confirmaciones concurrentes | POST repetido | Sin segundo Delivered/commit/hecho; respuesta según acta | T-ORD-15 | Carrera pendiente | HTTP replay pendiente |
| DEL-03 | Token expirado o falso; pedido no Ready | POST | Estado Ready inalterado o `409` según causa, sin commit | T-ORD-16 | Unit/integración pendientes | Errores token pendientes |
| DEL-04 | Comprador, tercero, JWT revocado o Redis caído | POST | Sin mutación ni fuga de token/pedido | RF-ORD-27 | Seguridad pendiente | Propiedad vigente, HTTP pendiente |
| DEL-05 | Persistencia o Catalog/Notifications/Reviews falla tras cambio | POST/reintento | Recuperación sin doble stock/hecho ni estado falsamente completado | RF-ORD-32 | Inyección fallo pendiente | D-ORD-02/06 y evento pendientes |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales (sin renumerar) | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| RF-ORD-24/25/26/27/29/32; T-ORD-14/15/16 | [Fase 04](../../phase-04-orders-cash/spec.md) | DEL-01..05 | DEV-API029-01..05; API-028/030 |
| TASK-ORD-001..004/010/011/012/025/026/030..032 | [Task histórico](../../phase-04-orders-cash/task.md) | DEL-01..05 | DEV-API029-01..05 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| DEL-01 | NO_IMPLEMENTADO | Sin confirmación, reserva ni hecho Reviews | DEV-API029-02/03/04 |
| DEL-02/05 | BLOQUEADO | Consumo único/consistencia entre módulos sin acta | DEV-API029-01/03/04/05 |
| DEL-03/04 | NO_IMPLEMENTADO | Sin validador/guard/pruebas | DEV-API029-02/05 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Fase 04](../../phase-04-orders-cash/spec.md), módulos ausentes | Entrega efectiva, contrato aprobado ni pruebas. |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| D-ORD-02/03/06/08/10; TASK-ORD-001..004 | [Fase 04 §9](../../phase-04-orders-cash/spec.md) | Token exclusivo, CAS/commit, `completedAt`, errores aún propuestos | DEV-API029-01..05 | Propietario/seguridad/Orders/Catalog; acta y Sprint 2. |
| Reviews sin spec aprobada | [Migración](../../../docs/migracion-sdd.md) | Hecho push vía contrato acíclico vs lectura directa (prohibida) | DEV-API029-04, API-030 | Orders/Reviews deciden payload/garantía de entrega, sin crear reseña automática. |

## Criterios de preparación para desarrollo

Alcance [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) por D-ORD y protocolo token/stock; DEL-01..05 requieren pruebas aisladas de consumo/commit únicos, auditoría y recuperación, sin tests productivos.
