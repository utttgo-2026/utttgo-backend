# API-031 — Listar reseñas de vendedor · spec

Estado de aprobación de la ficha: APROBADA por autorización explícita del propietario 2026-11-23
Estado operativo: IMPLEMENTED

## Identificación y autoridad

API-031 `GET /api/v1/sellers/{sellerId}/reviews`, dueño `Reviews` aunque ruta bajo sellers, según [trazabilidad](../../../docs/traceability.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente; no hay spec aprobada fase 08.

## Estado de aprobación

La autorización explícita del propietario ratifica acceso público con rate limit Redis, `page`/`pageSize` (defaults 1/20, máximo 50), orden descendente por `createdAt` y ascendente por `_id`, promedio decimal a dos posiciones y desglose de calificaciones 1..5. La respuesta no expone comprador, pedido ni PII.

## Responsabilidad única

Devolver listado paginado de reseñas atribuibles al `sellerId` real, sin mezclar productos/vendedores, revelar pedidos o publicar ratings no existentes. La creación pertenece a API-030; Sellers API-008 y Catalog API-011/012 consumen métricas por interfaz Reviews, no replican colección ni editan reseñas.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-005/006/009/011, §§4/5/6/14, v2.6 | Productos independientes, paginación, identidad/privacidad; sin DTO Reviews. |
| [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | API-031 fase 08, 2026-11-11 | Ruta y listado paginado, ninguna fase Reviews implementada. |
| [Fase 03](../../phase-03-catalog-offers/spec.md), [API-008](../api-008-seller-public-profile/spec.md) | RF-08/RF-V12 Catalog; perfil vendedor | Calificaciones solo desde Reviews, no inventar puntuación ni contactos. |

## Contrato aprobado

| Regla | ID/fuente de aprobación expresa | Límite |
|---|---|---|
| Listados paginados `page`/`pageSize`, máximo configurable | [Constitución §5](../../../docs/constitution.md) | Defaults, tamaño numérico, orden y total no fijados. |
| Ratings solo desde Reviews; sin datos personales indebidos | [Catalog RF-08](../../phase-03-catalog-offers/spec.md), [constitución §6](../../../docs/constitution.md) | No presupone que el rating tenga escala o sea público. |

## Comportamiento implementado candidato

`src/UTTTGO.Api/Modules/Reviews/` implementa el lector paginado y sus índices. El filtro `sellerId` se aplica en Mongo antes de paginar; no se consulta la colección `sellers`.

## Funcionalidad requerida faltante

El lector usa `sellerId`, índice compuesto y proyección pública mínima. Expone `averageRating` y `ratingBreakdown`; Reviews publica además lectores de métricas para Sellers/Catalog sin compartir la colección.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato NO aprobado | Falta decidir |
|---|---|---|---|
| Entrada | GET inventariado público con sellerId y `page`/`pageSize` acotados | `sellerId` ObjectId, página ≥1, máximo 50 | Seller sin reseñas devuelve página vacía con métricas nulas/cero. |
| Éxito | Solo reseñas del vendedor paginadas; ratings no ficticios | `200 {items,page,pageSize,totalCount,averageRating,ratingBreakdown}` | Orden por fecha descendente e ID ascendente. |
| Error | Contrato error único `correlationId`; Redis fail-closed | `400/429/503` | ID o paginación inválidos → `400`; Redis/Mongo → `429`/`503`. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Filtrar en Mongo por `sellerId` antes de paginar, orden total determinista e índice acorde; nunca confiar en usuario/ratings del query ni devolver BSON. Rate limit distribuido fail-closed en rutas públicas; si autenticada, JWT/blacklist fail-closed. Cualquier caché de ratings requiere invalidación con API-030/borrado y no puede exponer reseñas privadas; preferir no cachear sin acta. Logs sin PII/texto sensible.

## Escenarios Given/When/Then

| ID local | Dado (precondiciones) | Cuando (acción) | Entonces (resultado) | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| LIST-01 | Vendedor con 25 reseñas y otro con reseñas distintas | GET page=2&pageSize=20 | Página propia estable (5 items si contrato de total ratificado), cero ajenas | API-031 | Integración pendiente | Paginación vigente; DTO pendiente |
| LIST-02 | Vendedor sin reseñas/inactivo o ID inexistente | GET | Respuesta vacía o error según política, sin inventar rating | API-031, RF-08 | Unit/seguridad pendientes | Visibilidad/HTTP pendiente |
| LIST-03 | page/pageSize inválidos o Redis/Mongo caídos | GET | Rechazo/error único sin lista ilimitada ni bypass rate limit | API-031 | Integración pendiente | HTTP/defaults pendientes |
| LIST-04 | Reseña creada, duplicada o afectada por borrado de cuenta | GET tras cambio | Lista/ratings consistentes con Reviews aprobadas y política de datos compartidos | API-030/031, BR-011 | Integración con dobles pendiente | DEC-003 y política métricas pendientes |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales (sin renumerar) | Fuente | Escenario local | Tarea DEV local o ficha dependiente |
|---|---|---|---|
| API-031 (sin RF-REV/AC fase 08 publicados) | [Trazabilidad](../../../docs/traceability.md) | LIST-01..04 | DEV-API031-01..04; API-030 |
| RF-08/RF-V12 Catalog, BR-005/009/011 | [Fase 03](../../phase-03-catalog-offers/spec.md), [constitución](../../../docs/constitution.md) | LIST-02/04 | DEV-API031-01/03/04; API-008/005 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea o decisión |
|---|---|---|---|
| LIST-01 | NO_IMPLEMENTADO | Sin lector/índice/GET ni Reviews | DEV-API031-02 |
| LIST-02 | BLOQUEADO | Visibilidad de vendedor y respuesta vacía sin aprobar | DEV-API031-01/02 |
| LIST-03 | NO_IMPLEMENTADO | Validación/rate limit/pruebas ausentes | DEV-API031-02/04 |
| LIST-04 | OTRA_FICHA | Crear reseña API-030 y borrado API-005; métricas sin contrato | DEV-API031-01/03/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto/prueba y resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Trazabilidad](../../../docs/traceability.md), [Catalog](../../phase-03-catalog-offers/spec.md), Reviews ausente | Listado, política pública, ratings aprobados o pruebas. |

## DRIFT y decisiones pendientes

| ID original/DRIFT | Fuente | Alternativas documentadas | Impacto y tareas bloqueadas | Decisión/autoridad necesaria |
|---|---|---|---|---|
| API-031 fase 08 sin spec | [Migración](../../../docs/migracion-sdd.md) | Acceso, visibilidad, DTO, orden, pageSize, métricas/HTTP sin aprobar | DEV-API031-01..04 | Producto/Reviews/Sellers/seguridad, acta y cotejo Excel. |
| DEC-003/D-ORD-09 | [Constitución](../../../docs/constitution.md), [fase 04](../../phase-04-orders-cash/spec.md) | Datos de comprador/vendedor compartidos al eliminar | LIST-04, API-005 | Privacidad/propietario; sin purga improvisada. |

## Criterios de preparación para desarrollo

Alcance [IMPLEMENTED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) por autorización explícita del propietario el 2026-11-23. LIST-01..04 se verifican con pruebas locales de paginación, aislamiento y privacidad.
