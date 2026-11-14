# API-011 — Buscar productos · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-011 `GET /api/v1/products`, módulo `Catalog`, según [trazabilidad](../../../docs/traceability.md) y [migración](../../../docs/migracion-sdd.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente en este worktree.

## Estado de aprobación

BR-005/006 y DEC-019 aprueban búsqueda comparativa de publicaciones independientes y retiran API-016 sin redirección. Sprint 2 de [fase 03](../../phase-03-catalog-offers/spec.md) § 4.2 no está aprobado: parámetros, esquema, DTO y HTTP siguen pendientes (TASK-CATALOG-002).

## Responsabilidad única

Devolver listado paginado de productos propios de diferentes vendedores filtrables para comparación. No devuelve ofertas compartidas, no crea productos (API-013), no ejecuta compra (API-023), ni convierte API-016 en alias. API-012 aporta el detalle individual.

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-004/005/006/009, DEC-019 | Comparación por producto, sin menú ni ofertas; retiro API-016. |
| [Fase 03 spec](../../phase-03-catalog-offers/spec.md), [tareas](../../phase-03-catalog-offers/task.md) | RF-07/08/09/13, TASK-CATALOG-002 | Propuesta Sprint 2 sin contrato aprobado. |
| [Trazabilidad](../../../docs/traceability.md) | API-011; T-CAT-03 y TASK-CATALOG-012 históricos | Método/ruta; parámetros precisos pendientes. |

## Contrato aprobado

| Regla | Autoridad | Límite |
|---|---|---|
| Comparar publicaciones independientes de diferentes vendedores con ingredientes/precio y calificaciones disponibles | BR-005/006, DEC-019 | Sin equivalencia exacta ni ruta de ofertas. |
| Listados paginados; `page`/`pageSize` con máximo configurable | [Constitución](../../../docs/constitution.md) § 5 | Orden, filtros y máximo numérico no fijados. |

## Comportamiento implementado candidato

No existe `src/UTTTGO.Api/Modules/Catalog/`, controlador, DTO, servicios, repositorio ni tests de productos. La colección `products` es prevista, no implementada; Sellers existente solo ofrece perfil público y activación.

## Funcionalidad requerida faltante

Consulta Mongo `products` con filtros texto/ingredientes/precio/calificaciones disponibles, proyección por contrato de Sellers/Reviews, paginación e invalidación de caché. Categorías (API-019..022) y escritura (API-013..015) son dependencias sin documentar/implementar todavía; Reviews no se inventa.

## Contrato de entrada/salida

| Elemento | Aprobado | Candidato histórico | Falta decidir |
|---|---|---|---|
| GET público | Lectura de catálogo público, sin datos privados; rate limit distribuido | `GET /api/v1/products` con filtros | Acceso exacto y política de rate limit. |
| Query | `page`, `pageSize` y máximo configurable; comparación texto/ingredientes/precio/calificaciones disponibles | Ningún nombre/rango/orden de filtros aprobado | Nombres, combinaciones, normalización, límites de precio, orden y semántica de ratings faltantes. |
| Respuesta | Productos independientes paginados, vendedor identificable; no PII | DTO/HTTP sin implementar | Campos, total/metadata, estados visibles, datos ausentes, enlaces de imagen aprobada. |
| Error | Contrato único con `correlationId`; rate limit general fail-closed; cache fail-open | `400/429/503` son opciones, no contrato ratificado | Códigos y degradación al faltar Sellers/Reviews. |

## Seguridad y validaciones

Aplicar [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md) y [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Solo productos publicables (categoría/seller/estado coherentes) según decisión, sin PII; filtros acotados, regex escapada si se aprueba búsqueda textual; rating por interfaz Reviews, no acceso directo a colección ajena. Redis de caché fail-open, rate limit fail-closed. Stock visible no sustituye verificación de Orders; imágenes solo aprobadas por Media fase 05.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| LST-01 | Dos vendedores con productos homónimos publicables | GET filtrado | Ambos productos propios aparecen por separado y paginados, sin ofertas | BR-005, RF-07 | Integración por crear | Función aprobada; contrato pendiente |
| LST-02 | Ingredientes/precios/ratings disponibles | GET con filtros ratificados | Comparación usa precio/ingredientes propios y solo ratings reales | RF-07/08 | Unit e integración por crear | Filtros pendientes |
| LST-03 | Publicación pausada/categoría inactiva, o Redis caído | GET | Visibilidad según regla ratificada; caché falla abierta, rate limit cerrado | RF-09/13 | Seguridad e integración por crear | Política pendiente |
| LST-04 | Query inválida o paginación excesiva | GET | Error único sin filtrar datos o sobrecargar consulta | Constitución § 5 | Prueba por crear | Límites pendientes |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| RF-07/08/09/13, TASK-CATALOG-002 | [Fase 03](../../phase-03-catalog-offers/spec.md) | LST-01/02/03/04 | DEV-API011-01/02/03/04 |
| T-CAT-03, TASK-CATALOG-012 (referencia histórica) | [Trazabilidad](../../../docs/traceability.md) | LST-01/02 | DEV-API011-02/03 |
| DEC-019, BR-005/006 | [Constitución](../../../docs/constitution.md) | LST-01 | DEV-API011-02 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| LST-01 | NO_IMPLEMENTADO | Sin módulo/consulta paginada | DEV-API011-02 |
| LST-02 | BLOQUEADO | Query/ratings sin contrato Reviews ni aprobación | DEV-API011-01/03 |
| LST-03 | NO_IMPLEMENTADO | Caché, rate limit, estados de Catalog pendientes | DEV-API011-03/04 |
| LST-04 | BLOQUEADO | Límites/HTTP sin ratificar | DEV-API011-01/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Inspección documental | [Fase 03 § 4.2](../../phase-03-catalog-offers/spec.md), ausencia de `Modules/Catalog` y tests de producto | Endpoint, comportamiento real o aprobación Sprint 2. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| TASK-CATALOG-002 | [Tareas fase 03](../../phase-03-catalog-offers/task.md) | Filtros, orden, paginación y degradación Reviews por aprobar | DEV-API011-01/02/03/04 | Producto/API y dueños Catalog/Reviews: acta Sprint 2. |
| DEC-019 | [Constitución](../../../docs/constitution.md) | API-016 retirada sin alias | DEV-API011-02 | No reabrir ruta ni índice único global. |

## Criterios de preparación para desarrollo

Alcance: lectura comparativa, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) para programación hasta acta Sprint 2 y contrato por API. Una ficha completa no equivale a autorización. Aceptación: listados separados y paginados, filtros aprobados, rate limit cerrado, caché degradable, pruebas sin producción.
