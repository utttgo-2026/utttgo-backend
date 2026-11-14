# API-024 — Listar mis pedidos · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-024 `GET /api/v1/orders/mine`, módulo `Orders`, según [trazabilidad](../../../docs/traceability.md) y [migración](../../../docs/migracion-sdd.md); [constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente. [Fase 04](../../phase-04-orders-cash/spec.md) v0.1 es borrador, no aprobación.

## Estado de aprobación

Constitución § 5 exige `page`/`pageSize` y máximo configurable; privacidad exige solo pedidos propios. El borrador propone `role=buyer|seller` (D-ORD-05 **pendiente**); [tareas fase 04](../../phase-04-orders-cash/task.md) expresamente mantienen lectura **solo comprador** sin aprobación de extensión. HTTP, filtros/status, índices y alcance vendedor requieren acta TASK-ORD-001..004.

## Responsabilidad única

Listar pedidos del comprador autenticado con paginación, sin filtrar pedidos ajenos. Si se aprueba D-ORD-05, podrá incluir pedidos del vendedor participante mediante filtro de esta misma ruta; no crear endpoint alternativo, no devolver detalle completo (API-025) ni modificar estado.

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-001/002/003/009, § 5/6 | Paginación y privacidad/propiedad; efectivo sin pagos online. |
| [Fase 04 spec](../../phase-04-orders-cash/spec.md), [tareas](../../phase-04-orders-cash/task.md) | RF-ORD-08/09/15/30/31/32, T-ORD-19, TASK-ORD-010/013/021/026, D-ORD-05 | Borrador v0.1; listado Seller aún no aprobado. |
| [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | API-024 | GET comprador, método/ruta inventariados. |

## Contrato aprobado

| Regla | Fuente expresa | Límite |
|---|---|---|
| Listados paginados con `page`/`pageSize` y máximo configurable | [Constitución § 5](../../../docs/constitution.md) | Default y máximo numérico no aprobados. |
| Actor nunca ve pedidos de terceros; no confiar roles del query | [Constitución § 6](../../../docs/constitution.md), BR-009 | D-ORD-05 define si vendedor también es actor de este GET. |

## Comportamiento implementado candidato

No existe `src/UTTTGO.Api/Modules/Orders/`, repositorio `orders`, acción GET ni `Order*Tests`. No hay pedidos para listar; fase 04 v0.1 no tiene pruebas ejecutadas.

## Funcionalidad requerida faltante

Lector paginado Orders por `buyerUserId` derivado del perfil Identity, índice por comprador/orden estable, DTO mínimo sin PII ajena ni `orderAuditLogs`. Si D-ORD-05 aprueba vendedor, resolver `sellerId` del actor mediante Sellers y filtrar por participante; no leer `sellers` directamente ni crear ruta nueva. Reglas de cuenta eliminada/compartidos D-ORD-09 y DEC-003 siguen separadas.

## Contrato de entrada/salida

| Elemento | Aprobado | Propuesta v0.1 NO aprobada | Falta decidir |
|---|---|---|---|
| Entrada | GET Bearer, `page`/`pageSize` acotados, identidad de servidor | `page≥1` default 1, `pageSize` default 20/máx 50, `status` opcional, `role=buyer|seller` opcional D-ORD-05 | Default/máximo, filtro status, role/seller, orden estable y estado cuenta. |
| Éxito | Solo pedidos propios paginados | `200 {items,page,pageSize,total}`, items orderId/productId/productName/quantity/total MXN/status/role/createdAt | DTO, estado permitido, metadatos/visibilidad históricos; sin snapshots ajenos. |
| Error | Único con `correlationId`, rate limit Redis fail-closed | `400 VALIDATION_ERROR`, `401`, `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE` | HTTP final, fallos identidad/Sellers/Mongo. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Filtro Mongo por sujeto `buyerUserId` antes de paginar, no filtrar en memoria después; si seller habilitado, resolver Seller propio y aplicar `sellerId` del perfil, no query arbitrario. JWT/blacklist y rate limit fail-closed; no cachear pedidos personales. Orden estable, índices de lecturas reales y DTO sin documentos BSON directos ni contacto ajeno.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| MINE-01 | Comprador autenticado con 25 pedidos propios y otros ajenos | GET page 2/pageSize 20 | Devuelve 5 propios y total 25, sin ajenos (valores de prueba propuestos) | RF-ORD-30, T-ORD-19 | Mongo integración por crear | Paginación aprobada, defaults pendientes |
| MINE-02 | Usuario no vendedor solicita `role=seller` | GET con filtro | No se muestran pedidos de otro vendedor ni se expone nueva ruta | RF-ORD-15/30, D-ORD-05 | Seguridad por crear | Extensión BLOCKED |
| MINE-03 | Paginación/status inválidos o JWT revocado | GET | Error único o rechazo sin fuga de pedidos | RF-ORD-27/30/31 | Unit/seguridad por crear | HTTP pendiente |
| MINE-04 | Redis/Mongo indisponible o cuenta eliminada | GET | Fail-closed para rate limit; privacidad de compartidos según DEC-003 | RF-ORD-32, D-ORD-09 | Integración por crear | Borrado compartido pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| RF-ORD-08/09/15/27/30/31/32; T-ORD-19 | [Borrador fase 04](../../phase-04-orders-cash/spec.md) | MINE-01/02/03/04 | DEV-API024-01/02/03/04 |
| TASK-ORD-001..004/010/013/021/026, D-ORD-05/09 | [Tareas fase 04](../../phase-04-orders-cash/task.md), [spec](../../phase-04-orders-cash/spec.md) | MINE-01/02/04 | DEV-API024-01/02/03; API-005 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| MINE-01 | NO_IMPLEMENTADO | Sin orders, lector/index, GET/DTO | DEV-API024-02 |
| MINE-02 | BLOQUEADO | Alcance vendedor D-ORD-05 no aprobado | DEV-API024-01/03 |
| MINE-03 | NO_IMPLEMENTADO | Sin validación/guard/pruebas | DEV-API024-02/04 |
| MINE-04 | OTRA_FICHA | Política compartidos D-ORD-09/DEC-003; fallos de lectura pendientes aquí | DEV-API024-04; API-005 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Fase 04 v0.1](../../phase-04-orders-cash/spec.md), Orders ausente | Aprobación, paginación real o privacidad probada. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Decisión/autoridad |
|---|---|---|---|---|
| D-ORD-05, TASK-ORD-001..004 | [Fase 04 § 9](../../phase-04-orders-cash/spec.md), [tareas](../../phase-04-orders-cash/task.md) | Solo comprador o participante vendedor con `role`; no ruta externa sin ficha | DEV-API024-01/02/03/04 | Propietario/API/Orders ratifican alcance/HTTP, gate Catalog. |
| D-ORD-09/DEC-003 | [Constitución](../../../docs/constitution.md), [fase 04](../../phase-04-orders-cash/spec.md) | Datos compartidos tras borrado cuenta | API-005 | Privacidad por decidir; no inventar eliminación parcial. |

## Criterios de preparación para desarrollo

Alcance: lectura propia comprador, extensión vendedor solo tras D-ORD-05; [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta aprobación fase 04 y modelo Orders. Aceptación: paginación estable y filtros por identidad antes de query, sin exposición ajena.
