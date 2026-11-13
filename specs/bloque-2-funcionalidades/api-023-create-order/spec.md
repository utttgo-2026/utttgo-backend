# API-023 — Crear pedido directo · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-023 `POST /api/v1/orders`, módulo `Orders`, según [trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) y [borrador fase 04](../../phase-04-orders-cash/spec.md) v0.1 no aprobado. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Excel no disponible para cotejo directo en este worktree.

## Estado de aprobación

BR-001/002/003/009/010 y constitución § 13 son vigentes: una compra directa, un producto/un vendedor, efectivo, precio vigente × cantidad server-side. D-ORD-01/02/04/06/10 y TASK-ORD-001..004 siguen pendientes; la propuesta v0.1 (incluidos HTTP/rate limit/snapshot) no aprueba contrato Orders ni autoriza código. Sprint 2 de Catalog sin aprobar.

## Responsabilidad única

Crear pedido del comprador autenticado para un solo `productId`, verificar disponibilidad actual y coordinar reserva única de stock mediante Catalog. No crea carrito, no acepta `offerId`, no gestiona pago online, no modifica precio/stock directamente ni entrega pedido (API-029).

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-001/002/003/009/010, § 13/14 | Dinero decimal MXN, efectivo, identidad y total server-side. |
| [Fase 04 spec](../../phase-04-orders-cash/spec.md), [plan](../../phase-04-orders-cash/plan.md), [tareas](../../phase-04-orders-cash/task.md) | RF-ORD-01/02/03/10..14/27/28/31/32; T-ORD-01..04/17/18; TASK-ORD-010/011/012/013/020/026 | Solo borrador v0.1, D-ORD abiertas; no pruebas. |
| [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | API-023, RF-09/10 Catalog, TASK-CATALOG-016 histórico | Ruta; Catalog dueño de stock, sin ficha aprobada Sprint 2. |

## Contrato aprobado

| Regla | Autoridad | Límite |
|---|---|---|
| Una compra = un producto de un vendedor, sin carrito; solo efectivo presencial | BR-001/002/003 | Estado `Pending` definido en § 13; precio/stock snapshot pendiente. |
| Total = precio vigente elegido × cantidad, calculado por backend, `decimal` MXN | BR-009/010 | Momento atómico del precio/reserva D-ORD-01/02 por decidir. |
| Disponibilidad exige categoría, publicación y stock; no sobreventa | RF-09/10 [fase 03](../../phase-03-catalog-offers/spec.md), BR-009 | Contrato Catalog para reserva no aprobado. |

## Comportamiento implementado candidato

No existe `src/UTTTGO.Api/Modules/Orders/`, controlador, `orders`, `orderAuditLogs`, DTO, repositorio ni tests de pedidos. `Modules/Catalog` tampoco existe; ninguna reserva real ni prueba HTTP acreditada.

## Funcionalidad requerida faltante

Servicio Orders que lea producto/precio y categoría mediante interfaces Catalog y vendedor activo mediante Sellers, comprador activo mediante Identity, calcule total decimal y reserve sin sobreventa; escritor `orders` + auditoría de Orders. Diseñar reconciliación de inserción fallida/reserva huérfana e idempotencia D-ORD-02/04; no implementar repositorio Catalog en Orders.

## Contrato de entrada/salida

| Elemento | Aprobado | Propuesta v0.1 NO aprobada | Falta decidir |
|---|---|---|---|
| Entrada | Bearer, identidad local; `productId` y cantidad positiva; nunca confiar en precio, total, sellerId, estado/pago del cliente | JSON `{productId,quantity}`, cantidad 1..MaxQuantity; header opcional `Idempotency-Key` UUID | Límite cantidad, ID, rechazo vs ignorar extras, clave/replay D-ORD-04. |
| Éxito | Un pedido, total server-side y pago Cash | `201` con orderId/productId/sellerId/productName/quantity/unitPrice/total/currency MXN/paymentMethod Cash/status Pending/createdAt; snapshot D-ORD-01 | HTTP/DTO, momento precio-reserva, fallo entre pasos. |
| Error | Único `{correlationId,status,code,message}`, Redis fail-closed, no stock negativo | `400 VALIDATION_ERROR`, `401`, `403 ACCOUNT_NOT_ACTIVE`, `404 PRODUCT_NOT_FOUND`, `409 INSUFFICIENT_STOCK`, `422 PRODUCT_NOT_AVAILABLE`, `429`, `503 ORDER_UNAVAILABLE` | HTTP/semántica exacta, compensación e idempotencia. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). JWT/blacklist y perfil activo, rate limit distribuido fail-closed, whitelist que no permite total/estado/identidad arbitrarios; verificar stock/category/seller actuales al reservar, no solo caché API-011. Orders solo escribe `orders` y `orderAuditLogs`, precio decimal/UTC; auditoría inmutable sin datos financieros/tokens. Política D-ORD-09/DEC-003 de eliminación compartida pendiente, no borrar pedidos aquí.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| ORD-01 | Comprador activo, un producto vendible stock suficiente | POST productId y cantidad | Un `Pending`, total backend Cash y una reserva; snapshot propuesto | RF-ORD-10/11, T-ORD-01/02 | Unit/integración por crear | BR aprobadas; D-ORD-01/02 pendientes |
| ORD-02 | Stock insuficiente/pausa o categoría inactiva | POST | Sin pedido ni reserva parcial/venta | RF-ORD-12/13, T-ORD-03/04 | Concurrencia Mongo por crear | HTTP pendiente |
| ORD-03 | Dos POST simultáneos mismo comprador/clave | POST | No duplicar pedido/reserva según idempotencia ratificada | RF-ORD-28, T-ORD-18 | Integración por crear | D-ORD-04 pendiente |
| ORD-04 | Inserción Mongo falla tras reserva o Redis cae | POST | No éxito falso ni reserva huérfana; se compensa/reconcilia | RF-ORD-27/32, T-ORD-17 | Test fallos por crear | D-ORD-02 pendiente |
| ORD-05 | Body con precio/total/sellerId o JWT revocado | POST | No se acepta autoridad del cliente ni se escribe pedido | RF-ORD-14, BR-009 | Seguridad por crear | HTTP pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| RF-ORD-01/02/03/10/11/12/13/14/27/28/31/32; T-ORD-01/02/03/04/17/18 | [Borrador fase 04](../../phase-04-orders-cash/spec.md) | ORD-01/02/03/04/05 | DEV-API023-01/02/03/04/05 |
| TASK-ORD-001..004/010/011/012/013/020/026 | [Tareas fase 04](../../phase-04-orders-cash/task.md) | ORD-01/03/04 | DEV-API023-01/02/03/04 |
| BR-001/002/003/009/010 | [Constitución](../../../docs/constitution.md) | ORD-01/02/05 | DEV-API023-02/04 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| ORD-01 | NO_IMPLEMENTADO | Sin Orders, Catalog reader/commands ni DTO | DEV-API023-02/03 |
| ORD-02 | BLOQUEADO | Stock/categoría/seller y rechazo HTTP pendientes Sprint 2 | DEV-API023-01/03 |
| ORD-03 | BLOQUEADO | D-ORD-04/índice único sin ratificar | DEV-API023-01/04 |
| ORD-04 | BLOQUEADO | D-ORD-02 y compensación/reconciliación | DEV-API023-01/03/05 |
| ORD-05 | NO_IMPLEMENTADO | Falta whitelist, guard y seguridad | DEV-API023-02/05 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Fase 04 v0.1](../../phase-04-orders-cash/spec.md), [migración](../../../docs/migracion-sdd.md), Orders/Catalog ausentes | Aprobación, tests, reserva ni endpoint real. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Decisión/autoridad |
|---|---|---|---|---|
| D-ORD-01/02/04/06/10 | [Fase 04 § 9.2](../../phase-04-orders-cash/spec.md) | Snapshot vs momento precio; reserva/retry, clave idempotente, CAS, error | DEV-API023-01/02/03/04/05 | Propietario + Catalog/Orders/seguridad: acta expresa. |
| TASK-ORD-001..004 | [Tareas fase 04](../../phase-04-orders-cash/task.md) | Gate Sprint 2 y fase 04; no autoriza código por trío borrador | DEV-API023-01/02/03/04/05 | Producto y equipos confirman contrato Catalog y aprobación fase 04. |
| DEC-003 / D-ORD-09 | [Constitución](../../../docs/constitution.md), [fase 04](../../phase-04-orders-cash/spec.md) | Datos compartidos al borrar cuenta | Otra ficha API-005 | Política privacidad pendiente; no bloquear documentación/creación por handler de borrado. |

## Criterios de preparación para desarrollo

Alcance: compra directa, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta decisiones Orders, contrato de stock Catalog y aprobación Sprint 2/fase 04. Cierre: un pedido/reserva por operación autorizada, sin pagos online/duplicados/sobreventa ni reservas huérfanas, evidencia local fechada.
