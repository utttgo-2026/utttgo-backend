# API-031 — Listar reseñas de vendedor · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-031 `GET /api/v1/sellers/{sellerId}/reviews`, dueño `Reviews` aunque ruta bajo sellers, según [trazabilidad](../../../docs/traceability.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente; no hay spec aprobada fase 08.

## Estado de aprobación

Método/ruta y paginación figuran en trazabilidad; constitución §5 exige `page`/`pageSize` con máximo configurable, §4 prohíbe acceso a colecciones ajenas. Visibilidad pública/autenticada, filtros/orden, rating agregado, datos del autor, DTO/HTTP y estado de vendedor no aprobados; no inferir de API-008 ni de código ausente.

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

No existen `src/UTTTGO.Api/Modules/Reviews/`, repositorio de reseñas, endpoint API-031 ni pruebas Review; API-030 tampoco implementada. La ruta Sellers no autoriza leer `sellers` directamente desde Reviews.

## Funcionalidad requerida faltante

Lector Reviews con filtro `sellerId` derivado de reseñas creadas desde compras entregadas; validar existencia/visibilidad de vendedor mediante contrato Sellers si el acta lo requiere. Índice por sellerId y orden estable justificado por paginación, proyección sin buyerUserId/orderId/PII salvo decisión expresa. Métricas/ratings que consumen Catalog/Sellers mediante interfaz Reviews propia, no una colección compartida; política de reseñas tras borrado DEC-003 pendiente.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato NO aprobado | Falta decidir |
|---|---|---|---|
| Entrada | GET inventariado con sellerId y `page`/`pageSize` acotados | ID tipo ObjectId, página ≥1; auth pública o JWT según política | Formato ID, defaults/máximo, acceso/inactivo/inexistente. |
| Éxito | Solo reseñas del vendedor paginadas; ratings no ficticios | `200 {items,page,pageSize,total}` y orden por fecha+ID como candidato | DTO/campos/orden, filtros, rating promedio/conteo, reseñas vacías. |
| Error | Contrato error único `correlationId`; Redis/blacklist fail-closed cuando aplique | `400/401/404/429/503` candidatos | Código ID inválido/ausente, rate limit público, Seller inactivo y estado Mongo. |

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

Alcance [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta contrato Reviews aprobado y reseñas API-030; LIST-01..04 necesitan pruebas locales de paginación/aislamiento/privacidad sin valores inventados.
