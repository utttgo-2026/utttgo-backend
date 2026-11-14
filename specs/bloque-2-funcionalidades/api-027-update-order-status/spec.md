# API-027 — Cambiar estado de pedido · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-027 `PATCH /api/v1/orders/{orderId}/status`, Orders, según [trazabilidad](../../../docs/traceability.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

La máquina de estados de constitución §13 es vigente; el HTTP/DTO, CAS, auditoría y emisión de notificaciones del [borrador fase 04 v0.1](../../phase-04-orders-cash/spec.md) no están aprobados. TASK-ORD-001..004 y D-ORD-02/06/08/10 abiertos; no autoriza código.

## Responsabilidad única

Vendedor propietario cambia `Pending→Accepted|Rejected`, `Accepted→InPreparation`, `InPreparation→Ready`. No permite `Delivered` (API-029) ni `CancelledByBuyer` (API-026); no gestiona emisión de token (API-028). Al rechazar debe coordinar liberación de reserva sin doble efecto mediante contrato Catalog pendiente.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-002/003/008/009/010/012, §§4/5/6/13/14, v2.6 | Transiciones, propiedad, efectivo, notificación solo backend. |
| [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | API-027, 2026-11-11 | Ruta, fase pendiente. |
| [Fase 04 spec](../../phase-04-orders-cash/spec.md), [plan](../../phase-04-orders-cash/plan.md), [task](../../phase-04-orders-cash/task.md) | RF-ORD-16/17/18/21/22/26/27/29/32, T-ORD-07/09/10/11/20, TASK-ORD-023; v0.1 | Solo propuesta, no aprobación/implementación. |

## Contrato aprobado

| Regla | ID/fuente de aprobación expresa | Límite |
|---|---|---|
| Estados y aristas citadas; transición inválida `409` | [Constitución §13](../../../docs/constitution.md) | No fija DTO, versión ni código de error específico. |
| Identidad/rol no confiados al cliente; propiedad por recurso | [Constitución §§5/6](../../../docs/constitution.md) | Acceso de Admin propietario del borrador requiere ratificación. |
| Push solo desde backend, efectivo al entregar | BR-003/008 | Política de notificación y fallo parcial no aprobada. |

## Comportamiento implementado candidato

No existen `src/UTTTGO.Api/Modules/Orders/`, endpoints ni tests de Orders. El trío de fase 04 es documental; Catalog Sprint 2 tampoco implementado.

## Funcionalidad requerida faltante

Escritor condicional Orders y auditoría propia; resolución de propietario Sellers vía interfaz; liberar reserva por contrato Catalog al rechazar; publicar evento a Notifications por interfaz, sin escribir sus colecciones. Definir recuperación si transición persistida y liberación/auditoría/notificación falla, sin inventar transacción entre módulos.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato vigente | Falta decidir |
|---|---|---|---|
| Entrada | Ruta inventariada, JWT y vendedor propietario (§6) | `{status,version}`; ObjectId, `version` para CAS D-ORD-06 | Formato ID/version, Admin, extras/body, rate limit `order-write` 30/60 s. |
| Éxito | Solo aristas §13, no `Delivered` aquí | `200 {orderId,status,version,updatedAt}`; `Rejected` fija `completedAt` D-ORD-08, libera reserva D-ORD-02, audita y notifica | HTTP/DTO, reintento, secuencia/compensación, fecha y entregabilidad. |
| Error | `409` transición inválida; error único con `correlationId` | `400`, `401`, `403 ORDER_FORBIDDEN`, `404 ORDER_NOT_FOUND`, `409 ORDER_STATE_CONFLICT/ORDER_VERSION_CONFLICT`, `429`, `503` | HTTP y códigos exactos, colisiones de versión y tratamiento de tercero. |

## Seguridad y validaciones

Aplicar [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Resolver sellerId del perfil local y comparar con pedido: rol solo insuficiente; JWT/blacklist y limitador distribuidos fail-closed. Comparar estado+versión en actualización condicional si se aprueba D-ORD-06; no cachear pedidos. Nunca confiar en estado actual del cliente ni emitir FCM desde Orders; logs sin token/PII.

## Escenarios Given/When/Then

| ID local | Dado (precondiciones) | Cuando (acción) | Entonces (resultado) | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| ST-01 | Vendedor propio, pedido Pending | PATCH Accepted o Rejected | Una transición válida; Rejected terminal, libera una vez (protocolo pendiente) | RF-ORD-21/22 | Unit+integración pendientes | §13 vigente; efectos D-ORD abiertos |
| ST-02 | Pedido Accepted o InPreparation propio | PATCH siguiente estado | Accepted→InPreparation o InPreparation→Ready, sin saltos; notificación propuesta | T-ORD-20 | Unit+doble Notifications pendientes | Aristas vigentes, notificación propuesta |
| ST-03 | Pending con InPreparation, o Ready con Delivered | PATCH | `409`, sin cambiar estado ni confirmar efectivo | T-ORD-09/10 | Integración pendiente | §13 vigente |
| ST-04 | Comprador, vendedor ajeno, JWT revocado o Redis caído | PATCH | Rechazo sin divulgación/mutación | T-ORD-07, RF-ORD-27 | Seguridad pendiente | Propiedad vigente; HTTP pendiente |
| ST-05 | Dos escrituras con misma versión; Catalog o auditoría fallan tras Rejected | PATCH concurrente/reintento | Un solo cambio/liberación, recuperación sin doble efecto (propuesta) | T-ORD-11, RF-ORD-32 | Fallos/carrera pendientes | D-ORD-02/06 pendientes |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales (sin renumerar) | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| RF-ORD-16/17/18/21/22/26/27/29/32; T-ORD-07/09/10/11/20 | [Fase 04](../../phase-04-orders-cash/spec.md) | ST-01..05 | DEV-API027-01..04 |
| TASK-ORD-001..004/010/011/012/013/023/026/030..032 | [Task histórico](../../phase-04-orders-cash/task.md) | ST-01..05 | DEV-API027-01..04; API-023/026/029 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| ST-01/02 | NO_IMPLEMENTADO | Sin escritor, auditoría ni publicación de hecho | DEV-API027-02/03 |
| ST-03/04 | NO_IMPLEMENTADO | Sin guard ni pruebas negativas | DEV-API027-02/04 |
| ST-05 | BLOQUEADO | D-ORD-02/06: protocolo de concurrencia y fallo parcial | DEV-API027-01/03/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Fase 04](../../phase-04-orders-cash/spec.md), ausencia de Orders | Aprobación ni tests reales. |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| D-ORD-02/06/08/10; TASK-ORD-001..004 | [Fase 04 §9](../../phase-04-orders-cash/spec.md) | Reserva idempotente/CAS/version/fecha terminal propuestos; HTTP exacto abierto | DEV-API027-01..04 | Propietario, Orders/Catalog/seguridad: acta y gate Sprint 2. |
| D-ORD-03 | [Fase 04 §9](../../phase-04-orders-cash/spec.md) | Exclusividad token en API-029 propuesta | ST-03, API-029 | Aprobar flujo de entrega sin habilitar Delivered por este PATCH. |

## Criterios de preparación para desarrollo

Alcance técnico [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta acta fase 04 y comandos Catalog; ST-01..05 requieren pruebas locales sin doble liberación ni bypass de estado/propiedad. Documentar no implica aprobación.
