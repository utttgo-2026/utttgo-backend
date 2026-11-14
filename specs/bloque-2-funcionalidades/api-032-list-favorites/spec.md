# API-032 — Listar favoritos propios · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-032 `GET /api/v1/favorites`, módulo `Favorites`, según [trazabilidad](../../../docs/traceability.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

La [migración](../../../docs/migracion-sdd.md) mantiene Favorites sin spec histórica aprobada. Constitución §§4–6 exige frontera modular, identidad no confiada al cliente, listados `page`/`pageSize` con máximo configurable y rate limit distribuido fail-closed. Ruta y paginación figuran en trazabilidad; DTO, alcance público, orden, productos dados de baja y HTTP requieren decisión expresa.

## Responsabilidad única

Leer favoritos del usuario autenticado sin revelar listas de otros. Favoritos son referencias a productos independientes de vendedores (BR-006), no carrito ni ofertas; añadir/quitar corresponden a API-033/034. Catalog es dueño de productos y estado; no leer su colección desde Favorites.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-001/005/006/009/011, §§4/5/6/14 v2.6 | Sin carrito, productos propios, paginación/seguridad y borrado de cuenta. |
| [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | API-032, 2026-11-11 | GET paginado; sin fase Favorites aprobada. |
| [Catalog fase 03](../../phase-03-catalog-offers/spec.md) | DEC-019 Sprint 2, RF-09 | Productos/estado son de Catalog; fase pendiente. |

## Contrato aprobado

| Regla | ID/fuente de aprobación expresa | Límite |
|---|---|---|
| `page`/`pageSize`, máximo configurable; no acceso cruzado a colecciones | [Constitución §§4/5](../../../docs/constitution.md) | Tamaño, orden/total y DTO no aprobados. |
| Identidad y autorización por recurso server-side | [Constitución §§5/6](../../../docs/constitution.md) | No hay política de presentación de producto inactivo. |

## Comportamiento implementado candidato

No hay `src/UTTTGO.Api/Modules/Favorites/` ni tests `*Favorite*`; tampoco Catalog Sprint 2 implementado. No hay evidencia de endpoint, colección o contrato HTTP.

## Funcionalidad requerida faltante

Lector Favorites propio paginado por `userId` derivado del perfil local, índice para consulta/orden estable por decidir, DTO sin documento Mongo. Si la respuesta incluye productos, obtener datos a través de interfaz Catalog; no replicar precio/stock/categorías ni presentar producto oculto como comprable. Borrado de favoritos del usuario al eliminar cuenta (BR-011) debe coordinarse con API-005; no resolver datos compartidos aquí.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato NO aprobado | Falta decidir |
|---|---|---|---|
| Entrada | GET `/api/v1/favorites`, `page`/`pageSize` limitados, sujeto server-side | JWT propio; `page≥1`, `pageSize` default/máximo configurables | Requerir cuenta Active, defaults, filtros y acceso sin token. |
| Salida | Solo favoritos propios paginados, sin carrito | `200 {items,page,pageSize,total}` con productId; detalles opcionales vía Catalog | DTO, orden, productos retirados/inactivos, página vacía y conteo. |
| Error | Error único `correlationId`, blacklist/rate limit fail-closed | `400/401/429/503` posibles | Códigos, fallo Catalog/Mongo y degradación de enriquecimiento. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). JWT/blacklist, `userId` del backend, filtro Mongo antes de paginar, límite en servidor y rate limit Redis fail-closed; sin caché personal sin política/invalidez aprobada. No usar IDs del cliente para elegir lista ajena; respuesta sin PII, logs sin tokens/identificadores sensibles.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| FAVL-01 | Dos usuarios con favoritos diferentes | GET page=1 | Solo referencias propias, orden/página conforme acta | API-032 | Integración pendiente | Paginación vigente; DTO pendiente |
| FAVL-02 | Favoritos con producto retirado o pausado | GET | Política de visualización aún por ratificar, nunca compra de producto no vendible | BR-006, API-032 | Catalog doble pendiente | BLOCKED visibilidad |
| FAVL-03 | page/pageSize inválido, JWT revocado o Redis caído | GET | Sin lista ilimitada ni datos de terceros; error único | §§5/6, API-032 | Seguridad pendiente | HTTP pendiente |
| FAVL-04 | Cuenta borrada o Favorites vacío | GET posterior | Sin datos propios residuales tras baja; respuesta vacía según contrato | BR-011, API-032 | Integración pendiente | API-005/política baja pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario local | DEV o dependencia |
|---|---|---|---|
| API-032 (sin RF/AC Favorites publicados) | [Trazabilidad](../../../docs/traceability.md) | FAVL-01..04 | DEV-API032-01..04 |
| BR-001/006/009/011; DEC-019 Sprint 2 | [Constitución](../../../docs/constitution.md), [Catalog](../../phase-03-catalog-offers/spec.md) | FAVL-02/04 | DEV-API032-02/03/04; API-005/033/034 |

## Matriz de brechas

| Escenario local | Clasificación | Brecha concreta | Tarea/decisión |
|---|---|---|---|
| FAVL-01/03 | NO_IMPLEMENTADO | Sin lector/guard/paginación/tests | DEV-API032-02/04 |
| FAVL-02 | BLOQUEADO | Visibilidad de producto Catalog sin contrato | DEV-API032-01/03 |
| FAVL-04 | OTRA_FICHA | Borrado pertenece API-005; vacío aquí sin implementar | DEV-API032-04; API-005 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto/resultado | Qué NO acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Trazabilidad](../../../docs/traceability.md), ausencia Favorites | Endpoint, pruebas o DTO aprobado. |

## DRIFT y decisiones pendientes

| ID/DRIFT | Fuente | Alternativas | Impacto/tareas | Decisión/autoridad |
|---|---|---|---|---|
| API-032 fase 09 sin spec | [Migración](../../../docs/migracion-sdd.md) | Referencias solas vs detalles Catalog, visibles vs retirados, paginación/HTTP | DEV-API032-01..04 | Producto/Favorites/Catalog/seguridad: acta y cotejo Excel. |
| BR-011/DEC-003 | [Constitución](../../../docs/constitution.md) | Purga propia al borrar; efectos compartidos pendientes | FAVL-04, API-005 | Identity/Favorites/privacidad. |

## Criterios de preparación para desarrollo

Alcance [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta ficha Favorites y contrato Catalog; FAVL-01..04 requieren pruebas locales de aislamiento/paginación/sin compra implícita.
