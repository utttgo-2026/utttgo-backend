# API-030 — Crear reseña vinculada a pedido · spec

Estado de aprobación de la ficha: APROBADA por autorización explícita del propietario 2026-11-23
Estado operativo: IMPLEMENTED

## Identificación y autoridad

API-030 `POST /api/v1/orders/{orderId}/review`, módulo dueño `Reviews` aunque ruta bajo orders, según [trazabilidad](../../../docs/traceability.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente; no existe spec histórica de fase 08 aprobada.

## Estado de aprobación

La autorización explícita del propietario (2026-11-23) ratifica este contrato mínimo: `rating` entero de 1 a 5, `comment` opcional de máximo 1000 caracteres, una reseña por pedido, respuesta `201`, duplicado `409 REVIEW_ALREADY_EXISTS` y lectura de entrega mediante el contrato publicado por Orders. La reseña se deriva de `buyerUserId`, `sellerId` y `productId`; el cliente no puede enviarlos.

## Responsabilidad única

Registrar reseña del comprador para pedido `Delivered`, asociada al vendedor/producto realmente implicados; Reviews es dueño de la reseña y de sus métricas derivadas. No marcar entrega, cobrar, modificar pedido ni permitir a cliente fijar sellerId/productId/identidad o rating agregado. API-029 confirma entrega; API-031 lee reseñas; Catalog/Sellers consumen métricas por contrato Reviews.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-002/003/005/006/009/011, §§4/5/6/13/14 v2.6 | Pedido único, propiedad, comparación con calificaciones disponibles; borrado compartido pendiente. |
| [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | API-030, fase 08 pendiente | Ruta, comprador y pedido entregado; sin RF Reviews publicados. |
| [Fase 03](../../phase-03-catalog-offers/spec.md), [fase 04](../../phase-04-orders-cash/plan.md) | RF-08/RF-V12 Catalog; hecho entregado candidato v0.1 | Ratings solo desde Reviews; no dependencia inversa ni lectura directa Orders. |

## Contrato aprobado

| Regla | ID/fuente de aprobación expresa | Límite |
|---|---|---|
| Pedido `Delivered` equivale a efectivo recibido | [Constitución §13](../../../docs/constitution.md) | No define momento/plazo de reseña. |
| Propiedad por recurso, identidad/rol del servidor; calificaciones reales de Reviews | [Constitución §§4/5/6](../../../docs/constitution.md), [Catalog RF-08](../../phase-03-catalog-offers/spec.md) | Elegibilidad y forma de rating/reseña requieren acta Reviews. |

## Comportamiento implementado candidato

`src/UTTTGO.Api/Modules/Reviews/` implementa el endpoint, servicio, repositorio Mongo, índice único por `orderId`, rate limit Redis y pruebas. Orders publica `IDeliveredOrderReader`; Reviews no lee la colección `orders`.

## Funcionalidad requerida faltante

El contrato `IDeliveredOrderReader` acredita entrega, comprador, vendedor y producto. El índice único `orderId_unique` impide duplicados concurrentes; el contenido no se registra en logs y no se expone `buyerUserId` en la respuesta pública.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato NO aprobado (sin payload inventado) | Falta decidir |
|---|---|---|---|
| Entrada | Ruta POST inventariada, JWT, comprador del pedido entregado | `rating` 1..5 y `comment` opcional ≤1000 | No se aceptan sellerId/productId/buyerId/rating agregado del cliente. |
| Éxito | Reseña atribuida al pedido/seller/producto reales, no a IDs de cliente | `201` con `reviewId`, `sellerId`, `productId`, `rating`, `comment`, `createdAt` | No expone documento Mongo ni comprador. |
| Error | Error único con `correlationId`, propiedad y revocación fail-closed | `400/401/403/404/409/429/503` según escenario | Duplicado → `409 REVIEW_ALREADY_EXISTS`; fallos de dependencia → `503`. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). JWT/blacklist, comprador real y `Delivered` verificados contra evidencia Orders autorizada, no estado o sellerId del body; limitar tamaño/normalizar texto solo según acta. Rate limit distribuido fail-closed; no publicar reseñas si falla elegibilidad. Concurrencia: índice/condición para impedir duplicados si se aprueba política una por pedido; sin inventar cardinalidad. No cachear datos sensibles ni loguear contenido/PII; reseñas compartidas sujetas a DEC-003.

## Escenarios Given/When/Then

| ID local | Dado (precondiciones) | Cuando (acción) | Entonces (resultado) | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| REV-01 | Comprador propio, entrega acreditada y contrato de payload ratificado | POST reseña | Reseña atribuida a vendedor/producto del pedido, no a IDs de cliente | API-030 | Unit+integración pendientes | Entrada/HTTP pendientes |
| REV-02 | Pedido Pending o comprador ajeno | POST reseña | No se publica, no se revela pedido ajeno | API-030, §13 | Seguridad pendiente | Semántica HTTP pendiente |
| REV-03 | Dos POST simultáneos o retry de mismo pedido | POST | No duplicar si se aprueba cardinalidad; respuesta/replay según acta | API-030 | Carrera Mongo pendiente | Unicidad aún sin aprobar |
| REV-04 | Hecho de entrega ausente/tardío, Redis caído o cuenta borrada | POST | Fail-closed sin reseña impropia; reconciliación/privacidad conforme acta | API-030, BR-011 | Integración de fallos pendiente | D-ORD-09/DEC-003 pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales (sin renumerar) | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| API-030 (sin RF-REV/AC de fase 08 publicados) | [Trazabilidad](../../../docs/traceability.md) | REV-01..04 | DEV-API030-01..04; API-029 |
| RF-08/RF-V12 Catalog; BR-002/003/009/011 | [Fase 03](../../phase-03-catalog-offers/spec.md), [constitución](../../../docs/constitution.md) | REV-01/04 | DEV-API030-02/03; API-031/API-005 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| REV-01 | BLOQUEADO | Sin contrato Reviews/payload ni hecho Orders | DEV-API030-01/02 |
| REV-02 | NO_IMPLEMENTADO | Sin guard comprador/estado ni tests | DEV-API030-02/04 |
| REV-03 | BLOQUEADO | Política de duplicados/cardinalidad sin aprobar | DEV-API030-01/03 |
| REV-04 | OTRA_FICHA | Entrega API-029 y borrado API-005; fallo Redis pertenece también a esta ruta | DEV-API030-04; API-029/005 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md), ausencia Reviews | Contrato de rating, tests, elegibilidad real. |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| API-030 fase 08 sin spec | [Migración](../../../docs/migracion-sdd.md) | Payload, escala, texto, cardinalidad, plazo, HTTP aún sin alternativas aprobadas | DEV-API030-01..04 | Producto/Reviews/seguridad: acta de contrato, cotejo Excel. |
| D-ORD-03/02 y hecho entregado | [Fase 04](../../phase-04-orders-cash/plan.md) | Hecho acíclico propuesto, sin mecanismo de entrega acordado | DEV-API030-01/02/04 | Orders/Reviews acuerdan garantía/reintento; no lectura directa. |
| DEC-003/D-ORD-09 | [Constitución](../../../docs/constitution.md), [fase 04](../../phase-04-orders-cash/spec.md) | Redacción/eliminación de datos compartidos | REV-04, API-005 | Propietario/privacidad; no borrado unilateral. |

## Criterios de preparación para desarrollo

Alcance [IMPLEMENTED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) por autorización explícita del propietario el 2026-11-23. REV-01..04 se verifican con pruebas locales aisladas y sin calificaciones inventadas.
