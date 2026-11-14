# API-034 — Quitar producto favorito · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-034 `DELETE /api/v1/favorites/{productId}`, Favorites según [trazabilidad](../../../docs/traceability.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

La [migración](../../../docs/migracion-sdd.md) no registra fase Favorites aprobada. DELETE por productId está inventariado; constitucionalmente sin carrito (BR-001), identidad no se confía al cliente (BR-009), y eliminación de cuenta borra información propia (BR-011). HTTP para no existente, necesidad de validar existencia Catalog, índices y efecto sobre historial siguen sin aprobar.

## Responsabilidad única

Eliminar solamente la relación de favorito **del usuario autenticado** para el producto identificado. No borrar producto Catalog, pedido, publicación ni favorito de otro usuario; lista API-032 y alta API-033 son independientes. No depende de que Catalog siga publicando producto si ya hay relación, salvo decisión distinta ratificada.

## Fuentes consultadas

| Fuente | ID original y versión/fecha | Alcance y límite |
|---|---|---|
| [Trazabilidad](../../../docs/traceability.md), [migración](../../../docs/migracion-sdd.md) | API-034 fase 09, 2026-11-11 | DELETE por productId, sin contrato detallado. |
| [Constitución](../../../docs/constitution.md) | BR-001/006/009/011, §§4/5/6/14 v2.6 | Producto independiente; identidad del servidor, borrado propio. |
| [Catalog](../../phase-03-catalog-offers/spec.md) | Sprint 2 DEC-019 | Producto pertenece Catalog, baja lógica pendiente. |

## Contrato aprobado

| Regla | ID/fuente | Límite |
|---|---|---|
| DELETE target es productId, no offerId | [Trazabilidad](../../../docs/traceability.md), BR-006 | Éxito/no existente y validación producto pendientes. |
| Usuario no puede eliminar recurso ajeno, producto ni pedido | BR-009, [Constitución §§4/6](../../../docs/constitution.md) | Semántica idempotente/HTTP por decidir. |

## Comportamiento implementado candidato

No hay módulo Favorites, repositorio ni pruebas Favorite. El endpoint y borrado de relaciones no están implementados.

## Funcionalidad requerida faltante

DELETE condicional en colección Favorites por `userId` servidor + `productId` ruta, sin tocar `products` ni necesitar datos sensibles; decidir si idempotente aun cuando ya no exista o producto fue ocultado. Coordinar API-032/033 sobre lectura/alta concurrente y purga de favoritos al eliminar cuenta en API-005, sin borrar datos de otros usuarios.

## Contrato de entrada/salida

| Elemento | Aprobado (fuente) | Candidato NO aprobado | Falta decidir |
|---|---|---|---|
| Entrada | DELETE `/api/v1/favorites/{productId}` y actor servidor | JWT propio, ObjectId string, body vacío | Formato ID/body/estado cuenta, si consulta Catalog. |
| Éxito | Retira solo relación propia | `204` incluso si ausente, borrado idempotente por par usuario-producto | HTTP en ausente, reintento y carrera PUT/DELETE. |
| Error | Único `correlationId`, Redis fail-closed | `400/401/404/429/503` candidatos | HTTP final, producto inexistente y fallo Mongo/Redis. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). JWT/blacklist y rate limit Redis fail-closed; filtrar en escritura por actor y productId, no borrar por productId solo. No confiar userId/body ni divulgar si otro usuario marcó ese producto. Eliminar caché propia si se aprueba, no cachear PII sin política. Error `correlationId`, logs sin tokens/PII.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| FAVR-01 | Dos usuarios favorecen mismo producto | Usuario A DELETE | Se elimina solo par A/producto, par B intacto y producto intacto | API-034, BR-009 | Mongo integración pendiente | Regla propiedad vigente; HTTP pendiente |
| FAVR-02 | Favorito ausente o DELETE concurrente repetido | DELETE dos veces | Sin efectos adicionales, respuesta conforme acta | API-034 | Carrera pendiente | Idempotencia/HTTP pendientes |
| FAVR-03 | Producto retirado después de guardarse | DELETE propio | Puede quitar referencia sin alterar Catalog, según política ratificada | API-034 | Catalog doble pendiente | Consulta Catalog pendiente |
| FAVR-04 | JWT revocado/Redis caído o ID inválido | DELETE | Ninguna relación ajena alterada; error único | API-034, §6 | Seguridad pendiente | HTTP pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| API-034 (sin RF/AC Favorites publicados) | [Trazabilidad](../../../docs/traceability.md) | FAVR-01..04 | DEV-API034-01..04 |
| BR-006/009/011 | [Constitución](../../../docs/constitution.md) | FAVR-01/03/04 | DEV-API034-02/03/04; API-005/032/033 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| FAVR-01 | NO_IMPLEMENTADO | Sin DELETE protegido ni colección | DEV-API034-02/03 |
| FAVR-02 | BLOQUEADO | HTTP/ausencia/race sin aprobar | DEV-API034-01/03 |
| FAVR-03 | BLOQUEADO | Política producto retirado y Catalog | DEV-API034-01/02 |
| FAVR-04 | NO_IMPLEMENTADO | Sin seguridad/validación/tests | DEV-API034-02/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Trazabilidad](../../../docs/traceability.md), Favorites ausente | Endpoint ni pruebas. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad |
|---|---|---|---|---|
| API-034 fase 09 | [Migración](../../../docs/migracion-sdd.md) | 204 idempotente vs error en ausente, consulta Catalog o no, PUT/DELETE concurrentes | DEV-API034-01..04 | Producto/Favorites/Catalog/seguridad, acta/Excel. |
| BR-011/DEC-003 | [Constitución](../../../docs/constitution.md) | Borrado de cuenta retira favoritos propios | API-005 | Identity/Favorites/privacidad, no implementar aquí. |

## Criterios de preparación para desarrollo

Alcance [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) por fase 09 y semántica repetida; FAVR-01..04 exigen pruebas locales que conserven par ajeno y producto.
