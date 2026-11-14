# API-033 — Marcar producto favorito · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-033 `PUT /api/v1/favorites/{productId}`, Favorites según [trazabilidad](../../../docs/traceability.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

Inventario se refiere explícitamente a **producto**, no oferta. Constitución BR-001/005/006/009: no carrito, productos independientes y propiedad/identidad del servidor. [Migración](../../../docs/migracion-sdd.md) no registra spec Favorites aprobada: existencia/visibilidad del producto, reintentos, indexación, HTTP y DTO siguen pendientes.

## Responsabilidad única

Guardar referencia de producto elegido como favorito del usuario autenticado. No reservar stock, comprar, alterar publicación/precio ni modificar lista de otro usuario. Lectura API-032; eliminación API-034; validación del producto por contrato Catalog, sin escribir `products`.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | API-033, 2026-11-11 | PUT por productId; fase 09 pendiente. |
| [Constitución](../../../docs/constitution.md) | BR-001/005/006/009/011, §§4/5/6/14 v2.6 | Sin carrito, identidad/roles server-side, borrado propio. |
| [Catalog](../../phase-03-catalog-offers/spec.md) | DEC-019 Sprint 2; BR-006 | Cada producto pertenece a vendedor; modelo aun no implementado. |

## Contrato aprobado

| Regla | ID/fuente | Límite |
|---|---|---|
| El target es `productId` de producto independiente, no oferta | [Trazabilidad API-033](../../../docs/traceability.md), BR-006 | No fija visibilidad/elegibilidad o respuesta. |
| Actor no puede asignar userId/rol ni alterar producto | BR-009, [Constitución §5](../../../docs/constitution.md) | Política de favorito repetido pendiente. |

## Comportamiento implementado candidato

No hay `Modules/Favorites`, `Modules/Catalog`, pruebas Favorite ni persistencia. No está acreditado PUT real.

## Funcionalidad requerida faltante

Escritor Favorites con referencia `userId+productId`, validación de producto vía lector Catalog, protección contra duplicados/races según política acordada (índice único compuesto candidato), sin reservar stock ni crear carrito. Si Catalog cambia visibilidad tras guardar favorito, lectura API-032 decide presentación; baja API-034 y borrado de cuenta API-005. No asociar a `offerId`.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato NO aprobado | Falta decidir |
|---|---|---|---|
| Entrada | PUT `/api/v1/favorites/{productId}`; identidad backend | JWT y productId ObjectId; body vacío | ID, body, cuenta activa, producto activo/inactivo/no visible, favorito propio. |
| Éxito | Referencia usuario-producto, sin compra | Upsert atómico de par; `200/201/204` candidatos | Semántica PUT repetido, DTO, fecha y HTTP. |
| Error | Contrato único `correlationId`, rate limit fail-closed | `400/401/404/409/429/503` candidatos | Catalog ausente/no visible, conflicto/timeout y respuestas exactas. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). JWT/blacklist, userId del perfil local, rate limit Redis fail-closed, validar ID y visibilidad vía Catalog según acta; upsert/índice para no duplicar si se ratifica unicidad. No actualizar `products` ni mostrar existencia de no visibles. Logs sin PII/token, caché privada no adoptada sin invalidación.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| FAVA-01 | Usuario y producto elegible | PUT productId | Queda referencia propia, sin compra/stock | API-033, BR-001 | Integración pendiente | Elegibilidad pendiente |
| FAVA-02 | Mismo usuario y dos PUT simultáneos al producto | PUT repetido | Un favorito si política unicidad aprobada, HTTP ratificado | API-033 | Carrera Mongo pendiente | Upsert/HTTP pendiente |
| FAVA-03 | Producto inexistente/retirado o ID inválido | PUT | Sin favorito inconsistente; error conforme acta | API-033, BR-006 | Catalog doble pendiente | Visibilidad pendiente |
| FAVA-04 | JWT revocado, Redis/Catalog caído o usuario distinto | PUT | No altera datos ajenos ni crea referencia sin validación | API-033, §6 | Seguridad pendiente | Fallos/HTTP pendientes |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| API-033 (sin RF/AC Favorites publicados) | [Trazabilidad](../../../docs/traceability.md) | FAVA-01..04 | DEV-API033-01..04 |
| BR-001/006/009/011 | [Constitución](../../../docs/constitution.md) | FAVA-01/03/04 | DEV-API033-02/03/04; API-032/034/005 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| FAVA-01 | NO_IMPLEMENTADO | Sin PUT/modelo | DEV-API033-02/03 |
| FAVA-02 | BLOQUEADO | Idempotencia/índice/HTTP sin acta | DEV-API033-01/03 |
| FAVA-03 | BLOQUEADO | Catalog Sprint 2 y elegibilidad no aprobados | DEV-API033-01/02 |
| FAVA-04 | NO_IMPLEMENTADO | Sin guard/degradación/pruebas | DEV-API033-02/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Trazabilidad](../../../docs/traceability.md), Favorites ausente | PUT implementado ni contrato aprobado. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad |
|---|---|---|---|---|
| API-033 fase 09 | [Migración](../../../docs/migracion-sdd.md) | Unicidad/reintento, visibilidad y HTTP sin decisión | DEV-API033-01..04 | Producto/Favorites/Catalog/seguridad; acta y Excel. |
| DEC-003/BR-011 | [Constitución](../../../docs/constitution.md) | Eliminación de favoritos propios al borrar cuenta | API-005 | Identity/Favorites/privacidad; no implementarla aquí. |

## Criterios de preparación para desarrollo

Alcance [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta spec Favorites y lector Catalog aprobados; FAVA-01..04 exigen tests locales de reintentos/propiedad, sin crear carrito.
