# API-012 — Detalle de producto · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-012 `GET /api/v1/products/{productId}` del módulo `Catalog`, según [trazabilidad](../../../docs/traceability.md) y [migración](../../../docs/migracion-sdd.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

BR-006 y RF-03 confirman producto de vendedor único; DTO, visibilidad y HTTP no están aprobados. Sprint 2 de [fase 03](../../phase-03-catalog-offers/spec.md) es propuesta; TASK-CATALOG-002/012 siguen pendientes.

## Responsabilidad única

Obtener detalle público de un producto individual asociado a un vendedor. No busca todos los vendedores (API-011), no muestra ofertas (API-016 retirada), no reserva stock ni calcula pedido (API-023), no devuelve información privada de vendedor.

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-004/005/006/007/009, DEC-019 | Producto propio, imágenes moderadas, sin ofertas. |
| [Fase 03 spec](../../phase-03-catalog-offers/spec.md), [tareas](../../phase-03-catalog-offers/task.md) | RF-03/07/08/09/13, TASK-CATALOG-002 | Modelo previsto sin contrato Sprint 2 aprobado. |
| [Trazabilidad](../../../docs/traceability.md) | API-012, T-CAT-03/05 y TASK-CATALOG-012 históricos | Método/ruta y detalle; campos/HTTP por revisar. |

## Contrato aprobado

| Regla | Autoridad | Límite |
|---|---|---|
| Producto pertenece a un vendedor individual, sin `offerId` ni producto maestro | BR-006, RF-03 | No fija proyección/estados públicos. |
| Imágenes solo con Media aprobado y presigned GET backend | BR-007, constitución § 7 | Sprint 2 inicial sin imágenes; ninguna URL persistente pública. |

## Comportamiento implementado candidato

No existe `src/UTTTGO.Api/Modules/Catalog/`, endpoint, POCO de `products`, DTO ni pruebas `Product*Tests` en los proyectos. Código Sellers existente no proporciona detalle de producto.

## Funcionalidad requerida faltante

Lectura de `products` por ID, proyección pública con vendedor mediante interfaz de Sellers, visibilidad de categoría mediante dueño Catalog (API-019..022), ratings mediante Reviews si se aprueba; presigned GET de Media solo cuando exista media aprobada en fase 05. No crear Media desde Catalog.

## Contrato de entrada/salida

| Elemento | Aprobado | Candidato | Falta decidir |
|---|---|---|---|
| GET público con `productId` | Producto identificable; no identidad enviada por cliente | ID Mongo `ObjectId` 24 hex es opción coherente, no aprobada | Formato exacto, acceso y validación. |
| `200` | Datos propios del producto/vendedor sin PII ni ofertas | Nombre, descripción, ingredientes, precio decimal MXN, existencias y estado en modelo propuesto | DTO, imagen ausente, rating ausente, estado/stock visibles y semántica de categoría inactiva. |
| Error | `correlationId` y rate limit fail-closed | `400/404/429/503` candidatos | HTTP para ID malformado, producto pausado/eliminado, fallos Sellers/Redis; caching. |

## Seguridad y validaciones

Aplicar [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Validar ID antes de consulta, no exponer `ownerUserId`/PII ni devolver imagen no moderada; no usar DTO Mongo directamente. Rate limit distribuido fail-closed, cache de lectura fail-open; precio/stock mostrados no autorizan compra: Orders revalida categoría, publicación y existencias.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| DET-01 | Producto publicable de vendedor conocido | GET por productId | Detalle individual y vendedor, sin PII/ofertas | RF-03/07 | Integración por crear | Función confirmada, DTO pendiente |
| DET-02 | Producto pausado/eliminado o categoría inactiva | GET | Visibilidad y HTTP según contrato ratificado, no afirmar comprabilidad | RF-09 | Seguridad por crear | Estado/HTTP pendiente |
| DET-03 | ID inválido/ausente o Redis caído | GET | Error único, sin consulta abusiva y rate limit fail-closed | RF-13 | Integración por crear | HTTP pendiente |
| DET-04 | Reviews o Media ausentes | GET | No inventar rating ni exponer imagen sin aprobación | RF-08, BR-007 | Unit y seguridad por crear | Integraciones posteriores |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | Tarea o dependencia |
|---|---|---|---|
| RF-03/07/08/09/13, TASK-CATALOG-002 | [Fase 03](../../phase-03-catalog-offers/spec.md) | DET-01/02/03/04 | DEV-API012-01/02/03 |
| T-CAT-03/05, TASK-CATALOG-012 (históricos) | [Trazabilidad](../../../docs/traceability.md) | DET-01/02 | DEV-API012-02/03 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| DET-01 | NO_IMPLEMENTADO | Sin GET, repo ni DTO | DEV-API012-02 |
| DET-02 | BLOQUEADO | Visibilidad/HTTP sin definir | DEV-API012-01/03 |
| DET-03 | NO_IMPLEMENTADO | Rate limit y pruebas ID/caída ausentes | DEV-API012-02/03 |
| DET-04 | OTRA_FICHA | Lectores Ratings/Media de módulos dueños faltantes | DEV-API012-01/03; Reviews/Media |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Inspección documental | [Fase 03](../../phase-03-catalog-offers/spec.md), `Modules/Catalog` y tests ausentes | GET funcional ni contrato aprobado. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| TASK-CATALOG-002/012 | [Trazabilidad](../../../docs/traceability.md) y [fase 03 tareas](../../phase-03-catalog-offers/task.md) | Detalle de publicación pausada/categoría oculta, HTTP, DTO y rating | DEV-API012-01/02/03 | Producto/API: acta Sprint 2 y contrato individual. |
| DEC-019.e | [Constitución](../../../docs/constitution.md) | Sin imágenes Sprint 2; fase 05 independiente | DEV-API012-02 | No incluir `mediaAssetId` inventado ni URL pública. |

## Criterios de preparación para desarrollo

Alcance: detalle individual, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta aprobación Sprint 2 y modelo de API-013. Aceptación: detalle sin datos privados, semántica de estados acordada, pruebas localizadas con Mongo/Redis no productivos.
