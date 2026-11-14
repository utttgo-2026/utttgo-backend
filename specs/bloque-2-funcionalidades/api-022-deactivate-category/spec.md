# API-022 — Desactivar categoría · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-022 `DELETE /api/v1/admin/categories/{categoryId}`, dueño `Catalog`, actor Admin, según [trazabilidad](../../../docs/traceability.md) y [migración](../../../docs/migracion-sdd.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

RF-12 y constitución § 16 confirman que ocultar/desactivar categoría no borra ni modifica productos del vendedor. Sprint 2 de [fase 03](../../phase-03-catalog-offers/spec.md) sigue sin aprobación: semántica exacta DELETE, repetición, DTO/HTTP e invalidación por ratificar. Reactivación corresponde a API-021.

## Responsabilidad única

Desactivar una categoría existente del catálogo bajo autorización Admin, impidiendo nueva compra/publicación según contrato Catalog/Orders, **sin** borrar categorías físicamente ni documentos `products`. No elimina producto, pedido ni cuenta; no crea ruta alternativa.

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-006/009, § 16 API-019..022 | Desactivación/orden pendientes; productos propios intactos. |
| [Fase 03 spec](../../phase-03-catalog-offers/spec.md), [tareas](../../phase-03-catalog-offers/task.md) | RF-09/12, Sprint 2 § 6.3 | `categories.isActive` propuesto; Sprint 2 no aprobado. |
| [Trazabilidad](../../../docs/traceability.md) | API-022; TASK-CATALOG-011/T-CAT-05 históricos | DELETE Admin desactiva y oculta productos; IDs históricos no son tareas v2.1 cerradas. |

## Contrato aprobado

| Regla | Autoridad | Límite |
|---|---|---|
| Ocultar categoría no borra/modifica registros de productos | RF-12, [fase 03](../../phase-03-catalog-offers/spec.md), [trazabilidad](../../../docs/traceability.md) | Cómo afecta listados, detalle y reactivación por definir. |
| Comprabilidad requiere categoría activa, publicación activa y stock; Orders revalida | [Fase 03 RF-09](../../phase-03-catalog-offers/spec.md), BR-009 | No cancelar pedidos ya creados por esta ruta. |

## Comportamiento implementado candidato

No existe `src/UTTTGO.Api/Modules/Catalog/` ni endpoint DELETE, colección `categories`, servicio ni pruebas de categorías. API-019/020/021 están documentadas, no implementadas.

## Funcionalidad requerida faltante

Escritor condicional de `categories` propiedad Catalog, invalidación de lecturas API-019 y productos API-011/012 y contrato de elegibilidad de API-013/Orders API-023; ninguna mutación de `products` o `orders`. Política de fallo parcial/cache y reactivación posterior en API-021.

## Contrato de entrada/salida

| Elemento | Aprobado | Propuesta NO aprobada | Falta decidir |
|---|---|---|---|
| Entrada | DELETE `/admin/categories/{categoryId}` con JWT Admin local | ID ObjectId hex y body vacío | Formato ID, estado cuenta, body y semántica repetida. |
| Efecto | Desactivar/ocultar; no borrar `products` | `categories.isActive=false`, CAS/version, sin borrado físico | Visibilidad de productos en lista/detalle, pedidos en curso, invalidación. |
| Salida | Contrato de error único `correlationId` | `204` o `200` en éxito; `400/401/403/404/409/429/503` candidatos | HTTP/DTO/estado ya desactivado y conflictos concurrentes. |

## Seguridad y validaciones

Aplicar [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Validar token/blacklist, rol Admin en perfil local y categoría existente; rate limit distribuido fail-closed. Filtrar ID/versión en escritura si se aprueba CAS; logs sin PII/token. La caché es fail-open para lectura, pero no mostrar productos como comprables tras ocultación: política de invalidación/revalidación por aprobar. `products` permanece inalterada.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| CATD-01 | Admin y categoría activa con productos | DELETE categoría | Categoría se desactiva; productos propios persisten sin cambiar | RF-12, T-CAT-05 | Mongo integración por crear | Función confirmada, HTTP pendiente |
| CATD-02 | Categoría ya desactivada o dos solicitudes simultáneas | DELETE otra vez | Resultado idempotente/conflicto conforme acta, sin duplicar efectos | RF-12 | Unit/integración por crear | Repetición/CAS pendiente |
| CATD-03 | Comprador o JWT inválido/revocado | DELETE | Sin cambio; error autorizado con `correlationId` | BR-009 | Seguridad por crear | Rol local vigente |
| CATD-04 | Producto en categoría oculta; Orders consulta elegibilidad | GET/POST compra posteriores | No se crea nueva compra; no se borran pedidos/propios por esta acción | RF-09/12 | Integración Catalog/Orders pendiente | Contrato entre módulos pendiente |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| RF-09/12 | [Fase 03](../../phase-03-catalog-offers/spec.md) | CATD-01/02/03/04 | DEV-API022-01/02/03/04 |
| T-CAT-05, TASK-CATALOG-011 históricos | [Trazabilidad](../../../docs/traceability.md) | CATD-01/04 | DEV-API022-02/03; API-023 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| CATD-01 | NO_IMPLEMENTADO | Sin DELETE ni colección | DEV-API022-02/03 |
| CATD-02 | BLOQUEADO | Repetición/CAS/HTTP por aprobar | DEV-API022-01/03 |
| CATD-03 | NO_IMPLEMENTADO | Sin guard Admin ni pruebas | DEV-API022-02/04 |
| CATD-04 | OTRA_FICHA | Lectura Catalog/compra Orders no implementadas | DEV-API022-03/04; API-011/012/023 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Documental | [Fase 03](../../phase-03-catalog-offers/spec.md), ausencia Catalog | Endpoint, seguridad ni aprobación Sprint 2. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| Sprint 2, API-019..022 | [Constitución § 16](../../../docs/constitution.md) | Borrado físico vs desactivación: solo desactivación funcional confirmada; HTTP/idempotencia abiertos | DEV-API022-01/02/03/04 | Producto/API/Catalog: acta y cotejo Excel. |
| RF-09/12 | [Fase 03](../../phase-03-catalog-offers/spec.md) | Visibilidad en búsqueda/detalle y carrera compra | DEV-API022-03/04 | Catalog/Orders ratifican interfaz elegibilidad e invalidación. |

## Criterios de preparación para desarrollo

Alcance: DELETE administrativo que desactiva, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta acta Sprint 2; escenarios CATD-01..04 solo aceptados con categorías/pedidos aislados, productos intactos y sin sobreventa.
