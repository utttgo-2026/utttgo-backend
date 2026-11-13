# API-019 — Listar categorías · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-019 `GET /api/v1/categories`, módulo `Catalog`, según [trazabilidad](../../../docs/traceability.md) y [migración](../../../docs/migracion-sdd.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

RF-12 conserva categorías administradas y ocultación sin borrar productos; Sprint 2 no aprobado. La constitución exige paginar listados; orden, DTO, slug, visibilidad y HTTP siguen por conciliar. No adoptar `/api/v1/products/categories`, sustituida por esta ruta.

## Responsabilidad única

Devolver categorías visibles en un listado público paginado para navegación/filtrado de productos. No crea/edita (API-020/021), no desactiva (API-022), no devuelve productos ni revela datos de Admin.

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | § 5, § 16 API-019..022 | Paginación y DTO/slug/orden/desactivación pendientes. |
| [Fase 03](../../phase-03-catalog-offers/spec.md), [plan](../../phase-03-catalog-offers/plan.md) | RF-12, Sprint 2 § 6.3 | `categories` previstas, aún no creadas ni aprobadas. |
| [Trazabilidad](../../../docs/traceability.md) | API-019; T-CAT-05/TASK-CATALOG-011 históricos | Ruta pública y paginación; no son tareas cerradas de v2.1. |

## Contrato aprobado

| Regla | Fuente expresa | Límite |
|---|---|---|
| Listados con `page` + `pageSize`, máximo configurable | [Constitución § 5](../../../docs/constitution.md) | Máximo y orden por definir. |
| Ocultar categoría no borra ni modifica productos de vendedor | [Fase 03 RF-12](../../phase-03-catalog-offers/spec.md), constitución § 16 | Política de proyección al ocultar por ratificar. |

## Comportamiento implementado candidato

No existe `src/UTTTGO.Api/Modules/Catalog/`, endpoint `/categories`, colección `categories` ni pruebas `Category*Tests`; categoría activa es dependencia futura del POST de producto API-013.

## Funcionalidad requerida faltante

Repositorio lector de `categories` del módulo Catalog, proyección paginada/orden estable, rate limit distribuido y caché opcional; excluir categorías desactivadas según contrato. Escrituras pertenecen API-020/021/022, no se implementan aquí.

## Contrato de entrada/salida

| Elemento | Aprobado | Candidato NO aprobado | Falta decidir |
|---|---|---|---|
| GET público | Ruta inventariada `/api/v1/categories` | Sin JWT como lectura pública propuesta | Política de acceso y rate limit. |
| Query | `page`, `pageSize`, máximo configurable | Orden por campo `order` del modelo propuesto | Máximo numérico, valores por defecto, filtros y estabilidad/paginación. |
| Éxito | Listado paginado de categorías visibles | `id,name,slug,order` son posibilidades, no DTO aprobado | Campos de respuesta, slug/normalización, total y orden; categoría inactiva. |
| Error | Error único `correlationId`; rate limit fail-closed, caché fail-open | `400/429/503` candidatos | HTTP preciso para query inválida/fallos. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Validar page/pageSize/límite, ordenar determinísticamente y no exponer campos internos/PII; Redis de limitación fail-closed y de caché fail-open; invalidar caché después de cambios API-020/021/022 según política. Lectura de `categories` solo por Catalog.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| CAT-01 | Categorías activas | GET paginado | Respuesta pública con orden/paginación aprobados | RF-12, constitución § 5 | Integración por crear | Función confirmada; DTO pendiente |
| CAT-02 | Categoría desactivada con productos | GET | No borra productos; visibilidad según contrato aprobado | RF-12 | Mongo integración por crear | Política pendiente |
| CAT-03 | page/pageSize inválidos, Redis o Mongo caído | GET | Error único; cache falla abierta, rate limit cerrado | Constitución § 6 | Seguridad por crear | Límites/HTTP pendientes |
| CAT-04 | Cambio de categoría API-020/021/022 | GET posterior | Caché/orden reflejan actualización según política sin datos obsoletos | RF-12 | Integración por crear | Invalidación pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| RF-12 | [Fase 03](../../phase-03-catalog-offers/spec.md) | CAT-01/02/03/04 | DEV-API019-01/02/03 |
| T-CAT-05, TASK-CATALOG-011 históricos | [Trazabilidad](../../../docs/traceability.md) | CAT-01/02 | DEV-API019-02/03 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| CAT-01 | NO_IMPLEMENTADO | Sin colección, lector ni endpoint | DEV-API019-02 |
| CAT-02 | BLOQUEADO | Visibilidad de inactivas no ratificada | DEV-API019-01/02 |
| CAT-03 | NO_IMPLEMENTADO | Límites y degradación sin pruebas | DEV-API019-02/03 |
| CAT-04 | OTRA_FICHA | Requiere escrituras/invalidez de API-020/021/022 | DEV-API019-03; API-020/021/022 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Inspección documental | [Fase 03 § 6.3](../../phase-03-catalog-offers/spec.md); `Modules/Catalog` ausente | GET ni datos reales. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| API-019..022 Sprint 2 | [Constitución § 16](../../../docs/constitution.md) | DTO, slug, orden, paginación, ocultación | DEV-API019-01/02/03 | Producto/API/Catalog: acta Sprint 2 y cotejo Excel. |
| `/products/categories` histórico | [Trazabilidad](../../../docs/traceability.md) | Ruta sustituida por API-019 | DEV-API019-02 | No añadir alias. |

## Criterios de preparación para desarrollo

Alcance: listado público de categorías, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) por contrato Sprint 2. Aceptación: paginación/orden acordados, desactivada no borra productos, degradación Redis segura, suites locales con pruebas fechadas.
