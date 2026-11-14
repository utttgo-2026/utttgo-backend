# API-028 — Emitir token de entrega · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-028 `POST /api/v1/orders/{orderId}/delivery-token`, Orders; [trazabilidad](../../../docs/traceability.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

BR-003: efectivo al entregar; §13: `Ready→Delivered`, pago recibido. Token/QR, hash SHA-256, TTL y HTTP son [propuesta fase 04 v0.1](../../phase-04-orders-cash/spec.md), NO contrato aprobado; D-ORD-03/06/10 y TASK-ORD-001..004 sin cerrar. No atribuir BR-007 (imágenes S3) al token.

## Responsabilidad única

Emitir al comprador participante un secreto de entrega para pedido `Ready` sin efectuar pago, entrega ni confirmar stock; la verificación/consumo corresponde exclusivamente a API-029. No exponerlo a vendedor u otras API.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-003/009, §§5/6/13/14 v2.6 | Propiedad, efectivo, estados; no define token ni TTL. |
| [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | API-028, 2026-11-11 | Ruta, token backend para comprador; fase pendiente. |
| [Fase 04 spec](../../phase-04-orders-cash/spec.md), [plan](../../phase-04-orders-cash/plan.md), [task](../../phase-04-orders-cash/task.md) | RF-ORD-23/24/27/31/32, T-ORD-12/13, TASK-ORD-024/030..032, v0.1 | Hash, expiración y reemplazo propuestos, no aprobados. |

## Contrato aprobado

| Regla | ID/fuente de aprobación expresa | Límite |
|---|---|---|
| Entrega implica efectivo presencial recibido | BR-003, [§13](../../../docs/constitution.md) | Emitir token no entrega ni marca pago. |
| Identidad y propiedad se verifican en backend; no loguear tokens | [Constitución §§5/6](../../../docs/constitution.md) | Token criptográfico concreto y quién puede emitirlo pendientes. |

## Comportamiento implementado candidato

No existen módulo Orders, repositorio `orders`, generador de tokens ni tests de entrega. La fase 04 es borrador no aprobado.

## Funcionalidad requerida faltante

Generación criptográfica segura en Orders, persistencia únicamente de hash+expiración en documento de pedido propio; reemplazo condicional del token anterior y coordinación de carreras con API-029. No guardar secreto en Redis, logs, auditoría ni DTO de otras API. Definir el comportamiento si falla auditoría después de guardar hash sin exponer un token inutilizable como válido.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente NO aprobado | Falta decidir |
|---|---|---|---|
| Entrada | POST inventariado, identidad derivada de JWT, comprador propio | `orderId` ObjectId, body vacío; `Ready` obligatorio | ID, body, acceso y política de emisión/reintento. |
| Éxito | No altera §13 ni representa cobro | `201 {orderId,token,expiresAt}`; random alta entropía, solo SHA-256 en `orders`, TTL 900 s configurable; nuevo token invalida anterior | TTL, formato/entropía, hash, versión/CAS D-ORD-06, respuesta si auditoría falla. |
| Error | Error único `correlationId`; Redis fail-closed | `401`, `403 ORDER_FORBIDDEN`, `404 ORDER_NOT_FOUND`, `409 ORDER_STATE_CONFLICT`, `429` (5/300 s), `503 ORDER_UNAVAILABLE` | HTTP/errores exactos, terceros y agotamiento. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Verificar buyerUserId, `Ready` y token JWT/blacklist; rate limit distribuido fail-closed, sin logs/body/token ni errores con secreto, transporte HTTPS; no persistir token en claro ni devolver en GET detalle. Emisión concurrente y confirmación compiten con CAS atómico, no confiar en expiración enviada por cliente. Auditar emisión sin secreto solo tras decisión.

## Escenarios Given/When/Then

| ID local | Dado (precondiciones) | Cuando (acción) | Entonces (resultado) | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| TOK-01 | Comprador propio, pedido Ready | POST token | Se entrega secreto una sola vez al cliente; persistido solo hash/expiración, estado Ready | T-ORD-12 | Unit+integración pendientes | Diseño token propuesto |
| TOK-02 | Token vigente y pedido Ready | POST de nuevo | Anterior inválido, solo último aceptado (propuesta) | T-ORD-13 | Carrera con API-029 pendiente | D-ORD-06 pendiente |
| TOK-03 | Pedido no Ready, vendedor/tercero o JWT revocado | POST | Sin token ni divulgación; estado inalterado | RF-ORD-23/27 | Seguridad pendiente | Propiedad vigente, HTTP pendiente |
| TOK-04 | Redis caído, generación/auditoría falla o emisión compite con entrega | POST | Sin token válido expuesto si no persiste; ningún uso doble | RF-ORD-32 | Pruebas fallo pendientes | Protocolo pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales (sin renumerar) | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| RF-ORD-23/24/27/31/32; T-ORD-12/13 | [Fase 04](../../phase-04-orders-cash/spec.md) | TOK-01..04 | DEV-API028-01..04; API-029 |
| TASK-ORD-001..004/010/011/024/026/030..032 | [Task histórico](../../phase-04-orders-cash/task.md) | TOK-01..04 | DEV-API028-01..04 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| TOK-01 | NO_IMPLEMENTADO | Sin generador ni escritor de hash | DEV-API028-02 |
| TOK-02 | BLOQUEADO | Reemplazo vs consumo concurrente sin protocolo | DEV-API028-01/03; D-ORD-06 |
| TOK-03 | NO_IMPLEMENTADO | Sin guard comprador y estado | DEV-API028-02/04 |
| TOK-04 | BLOQUEADO | Consistencia hash/auditoría/fallos y rate limit no probados | DEV-API028-01/03/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Fase 04](../../phase-04-orders-cash/spec.md), Orders ausente | Token emitido, secreto seguro o aceptación. |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| D-ORD-03/06/10; TASK-ORD-001..004 | [Fase 04 §9](../../phase-04-orders-cash/spec.md) | Token único para API-029, reemplazo CAS y HTTP propuestos; TTL/hash sin aprobar | DEV-API028-01..04 | Propietario/seguridad/Orders: acta de secreto, carreras, auditoría y Sprint 2. |

## Criterios de preparación para desarrollo

Alcance [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta acta y modelo API-023/API-027; TOK-01..04 requieren pruebas aisladas, no registros de token claro ni cobro por emisión.
