# API-014 — Editar producto propio · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-014 `PATCH /api/v1/products/{productId}`, módulo `Catalog`, según [trazabilidad](../../../docs/traceability.md) y [migración](../../../docs/migracion-sdd.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

BR-006/009 y RF-04 confirman edición de datos, ingredientes, precio y stock por vendedor propietario; propuesta histórica limitada a descripción/imagen está superada. Sprint 2 pendiente TASK-CATALOG-002; payload, actor Admin, concurrencia/HTTP no aprobados.

## Responsabilidad única

Actualizar datos comerciales permitidos del producto de un vendedor, validando propiedad. No edita ofertas, no publica imagen sin Media, no cambia identidad/vendedor, no pausa/reactiva (API-015), no recalcula precio de pedidos ya creados (Orders).

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-006/007/009/010, DEC-016/019 | Vendedor propietario modifica precio/stock; Orders define snapshot. |
| [Fase 03 spec](../../phase-03-catalog-offers/spec.md), [tareas](../../phase-03-catalog-offers/task.md) | RF-04/09/10/11, TASK-CATALOG-002 | Sprint 2 propuesto; media fase 05. |
| [Trazabilidad](../../../docs/traceability.md) | API-014, T-CAT-02, TASK-CATALOG-013 histórico | Método/ruta; actor Admin y payload por conciliar. |

## Contrato aprobado

| Regla | Autoridad | Límite |
|---|---|---|
| Solo vendedor propietario puede solicitar cambiar información/ingredientes/precio/existencias | BR-006/009, RF-04 | Actor Admin, transiciones y JSON sin aprobar. |
| Precio decimal MXN, stock entero no negativo | [Fase 03 § 4.3](../../phase-03-catalog-offers/spec.md) | Órdenes existentes y reserva concurrente pertenecen a Orders. |
| Imágenes Sprint 2 excluidas | DEC-019.e | Media futura requiere activo `Approved`. |

## Comportamiento implementado candidato

No existe `src/UTTTGO.Api/Modules/Catalog/` ni `PATCH /products/{productId}`, DTO/validador, repositorio `products` o pruebas de producto. `PATCH /sellers/me` existente es edición de perfil de negocio, no de producto.

## Funcionalidad requerida faltante

Edición segura con verificación de propiedad y actualización atómica de campos permitidos sobre `products`; control de concurrencia/version y coherencia con reservas Orders por aprobar; invalidación de lectura API-011/012. Contratos de categoría API-019..022 y Media fase 05 dependientes, sin implementarlos aquí.

## Contrato de entrada/salida

| Elemento | Aprobado | Candidato | Falta decidir |
|---|---|---|---|
| PATCH + `productId` | JWT válido y vendedor propietario derivado de perfil local | ObjectId hex candidato, no ratificado | Formato ID; si Admin actúa y en qué alcance. |
| Body | Edición de información, ingredientes, precio y existencias validada; sin sellerId/roles/estado | Campos `name,description,ingredients,price,stock` del modelo histórico, no whitelist aprobada | Omisión/null, rangos, categoría, imagen, version/precondición y stock reservado. |
| Respuesta | Producto modificado solo si propio | `200` con DTO, `409` en CAS son candidatos | HTTP/DTO, recurso ausente/ajeno, error de conflicto y retry. |
| Fallos | Error único con `correlationId`, rate limit fail-closed | `400/401/403/404/409/429/503` candidatos | Mapeo preciso, caída Sellers/Mongo/Redis, invalidación. |

## Seguridad y validaciones

Aplicar [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). JWT/blacklist, rol local y propiedad `products.sellerId` coincidente con perfil Seller resuelto por interfaz; precio decimal y stock no negativo, nunca campos privilegiados, URL o media no aprobada. Actualizar con filtro propietario + versión si se ratifica; Redis fail-closed, cache de lecturas invalidada; Orders valida stock/precio en compra.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| UPD-01 | Seller dueño de producto | PATCH campos válidos | Solo su producto cambia; precio decimal/stock válido | RF-04 | Unit/integración por crear | Regla confirmada, DTO pendiente |
| UPD-02 | Seller diferente o Buyer | PATCH productId ajeno | Sin mutación del recurso ajeno, error ratificado | BR-006/009 | Seguridad por crear | Propiedad aprobada |
| UPD-03 | Dos ediciones o reserva concurrentes | PATCH precio/stock | No hay pérdida ni sobreventa según protocolo acordado | RF-04/10 | Mongo/Orders por probar | Política pendiente |
| UPD-04 | Body inválido/imagen no aprobada o Redis caído | PATCH | Rechazo sin escritura ni publicación insegura | BR-007, RF-11 | Seguridad por crear | Media excluida |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| RF-04/09/10/11, TASK-CATALOG-002 | [Fase 03](../../phase-03-catalog-offers/spec.md) | UPD-01/02/03/04 | DEV-API014-01/02/03/04 |
| T-CAT-02, TASK-CATALOG-013 histórico | [Trazabilidad](../../../docs/traceability.md) | UPD-01/03 | DEV-API014-02/03 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| UPD-01/02 | NO_IMPLEMENTADO | Sin PATCH/autorizar recurso | DEV-API014-02/04 |
| UPD-03 | BLOQUEADO | Stock/Orders y CAS sin decisión | DEV-API014-01/03; Orders |
| UPD-04 | NO_IMPLEMENTADO | Sin validador/Redis ni pruebas | DEV-API014-02/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Inspección documental | [Fase 03](../../phase-03-catalog-offers/spec.md), ausencia de Catalog y tests productos | PATCH operativo ni protocolo de concurrencia. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| TASK-CATALOG-002, RF-04 | [Trazabilidad](../../../docs/traceability.md) | Edición completa confirmada vs descripción/imagen histórica; actor Admin y payload sin ratificar | DEV-API014-01/02 | Producto/API/seguridad: contrato Sprint 2. |
| DEC-013, RF-10 | [Memoria](../../../memoria.md), [fase 03](../../phase-03-catalog-offers/spec.md) | Concurrencia stock con pedidos/snapshot | DEV-API014-03 | Catalog/Orders: interfaz/reserva aprobada antes de mutación concurrente. |

## Criterios de preparación para desarrollo

Alcance: edición de producto propio, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta Sprint 2 y política de stock. Aceptación: propiedad, CAS/reservas según acta, sin alterar pedido ajeno, cache coherente y tests no productivos verdes.
