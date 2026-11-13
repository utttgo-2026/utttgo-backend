# API-015 — Cambiar disponibilidad de producto · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-015 `PATCH /api/v1/products/{productId}/availability`, módulo `Catalog`, según [trazabilidad](../../../docs/traceability.md) y [migración](../../../docs/migracion-sdd.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

RF-05 y BR-006/009 confirman que dueño pausa publicación y deja de ser comprable; no equiparar pausa con stock cero/`SoldOut`. Sprint 2 de [fase 03](../../phase-03-catalog-offers/spec.md) pendiente TASK-CATALOG-002; payload, reactivo, estados y HTTP requieren acta.

## Responsabilidad única

Cambiar disponibilidad de publicación propia sin modificar existencias/precio (API-014), sin borrar producto (ficha pública pendiente TASK-CATALOG-004) ni confirmar pedido (Orders API-023). Categoría y vendedor siguen siendo condiciones independientes de comprabilidad.

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-004/006/009, DEC-016/019 | Pausa por dueño, sin horarios; baja lógica no equivale a pausa. |
| [Fase 03 spec](../../phase-03-catalog-offers/spec.md), [tareas](../../phase-03-catalog-offers/task.md) | RF-05/09/10, TASK-CATALOG-002/004 | Sprint 2 propuesto; estado y ruta de eliminación por decidir. |
| [Trazabilidad](../../../docs/traceability.md) | API-015, T-CAT-02/05, TASK-CATALOG-014 histórico | Método/ruta; conciliar `Available`/`SoldOut` y reactivación. |

## Contrato aprobado

| Regla | Autoridad | Límite |
|---|---|---|
| Solo dueño pausa, producto deja de ser comprable | BR-006/009, RF-05 | Estado/HTTP concretos pendientes. |
| Comprabilidad exige publicación y categoría activas y stock suficiente; Orders revalida | RF-09, constitución § 13 | Sin horario/menú; no reservar stock aquí. |
| Baja de producto Sprint 2 es lógica (`Deleted`) | DEC-019 | No añade ruta DELETE ni permite reactivar borrado. |

## Comportamiento implementado candidato

No existe `src/UTTTGO.Api/Modules/Catalog/`, controlador de disponibilidad, DTO, máquina de estados, repositorio de productos ni pruebas. `PATCH /sellers/me` edita vendedor, no estado de sus publicaciones.

## Funcionalidad requerida faltante

Máquina de estados de publicación (pausa y eventual reactivación) aprobada; escritura condicional por dueño sobre `products`, invalidación cache API-011/012 y contrato de lectura/revalidación por Orders. No implementar reserva ni eliminación pública en esta API.

## Contrato de entrada/salida

| Elemento | Aprobado | Candidato histórico | Falta decidir |
|---|---|---|---|
| Actor y ruta | JWT Seller propietario; `productId` | ObjectId hex candidato no ratificado | Validación ID/actor Admin, error para ajeno. |
| Body | Intención de pausar publicación; no alterar stock ni precio | `Available`/`SoldOut` aparecen en material previo | Campo/valor(es), reactivación, idempotencia, relación `Paused`/`SoldOut` y baja `Deleted`; no inventar enum. |
| Respuesta | Tras pausa el producto no se compra | `200/204` y DTO candidatos | HTTP/transiciones, versión/retry, respuesta si stock 0 o categoría oculta. |
| Error | Contrato único `correlationId`, fail-closed Redis | `400/401/403/404/409/429/503` candidatos | Mapeo transiciones inválidas y fallo de invalidación. |

## Seguridad y validaciones

Aplicar [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). JWT/blacklist, rol local, Seller dueño vía interfaz; filtro `_id` + `sellerId` y versión si aprobado; nunca aceptar sellerId/stock/precio arbitrarios en body; Redis rate limit fail-closed, cache fail-open. Orders comprueba categoría/estado/stock en transacción propia; pausa no modifica pedidos existentes sin diseño Orders.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| AVL-01 | Seller dueño, producto activo con stock | Solicita pausa | Publicación no comprable; stock y precio sin cambios | RF-05/09 | Integración por crear | Regla aprobada; contrato pendiente |
| AVL-02 | Producto pausado o stock 0 | Solicita reactivación según contrato futuro | Resultado según transición ratificada; no confundir pausa/agotamiento | RF-05/09 | Unit y seguridad por crear | Reactivación pendiente |
| AVL-03 | Otro vendedor o producto eliminado | PATCH disponibilidad | Sin mutar producto ajeno/Deleted; error ratificado | BR-006, DEC-019 | Seguridad por crear | HTTP pendiente |
| AVL-04 | Pedido concurrente, Redis caído o cache anterior | Pausa | Orders revalida y cache no presenta comprabilidad obsoleta según política | RF-09/10 | Integración por crear | Protocolo Orders pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| RF-05/09/10, TASK-CATALOG-002/004 | [Fase 03](../../phase-03-catalog-offers/spec.md) | AVL-01/02/03/04 | DEV-API015-01/02/03/04 |
| T-CAT-02/05, TASK-CATALOG-014 histórico | [Trazabilidad](../../../docs/traceability.md) | AVL-01/02 | DEV-API015-02/03 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| AVL-01 | NO_IMPLEMENTADO | Sin PATCH ni estado/publicación | DEV-API015-02 |
| AVL-02 | BLOQUEADO | Reactivación y SoldOut sin semántica aprobada | DEV-API015-01/03 |
| AVL-03 | NO_IMPLEMENTADO | Falta autorización por recurso y CAS | DEV-API015-02/04 |
| AVL-04 | BLOQUEADO | Invalidez de cache y carrera con Orders sin contrato | DEV-API015-01/03/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Inspección documental | [Fase 03](../../phase-03-catalog-offers/spec.md) RF-05/09 y ausencia de Catalog | Pausa real o prohibición de pedidos. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| TASK-CATALOG-002, RF-05 | [Trazabilidad](../../../docs/traceability.md) | Estado pausado distinto de `SoldOut`; reactivar según stock/categoría | DEV-API015-01/02/03 | Producto/API/Catalog: acta Sprint 2, transición/HTTP. |
| RF-09/10, TASK-CATALOG-004 | [Fase 03](../../phase-03-catalog-offers/spec.md) | Órdenes concurrentes y baja lógica separada | DEV-API015-03/04 | Catalog/Orders: contrato de disponibilidad, ficha DELETE por aparte. |

## Criterios de preparación para desarrollo

Alcance: pausa/reactivación según contrato futuro, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta aprobación Sprint 2 y máquina de estados. Aceptación: pausa propia, stock intacto, lectura/cache coherente, carrera con Orders cubierta sin inventar disponibilidad temporal.
