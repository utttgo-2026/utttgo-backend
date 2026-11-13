# API-021 — Editar o reactivar categoría · spec

Estado de aprobación de la ficha: PENDIENTE
Estado operativo: BLOCKED

## Identificación y autoridad

API-021 `PATCH /api/v1/admin/categories/{categoryId}`, módulo `Catalog` con autorización Admin local, según [trazabilidad](../../../docs/traceability.md) y [migración](../../../docs/migracion-sdd.md). [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

RF-12 conserva edición/reactivación Admin y prohíbe borrar/modificar productos al ocultar categoría. Sprint 2 no aprobado; payload, slug, orden, transición de reactivación/HTTP pendientes de conciliación (constitución § 16). No atribuir a `DELETE` API-022 la reactivación.

## Responsabilidad única

Actualizar atributos autorizados de **una categoría** del catálogo, incluida reactivación si se aprueba en el contrato, sin tocar documentos `products`. API-020 crea, API-019 lista, API-022 desactiva; no permite manipulación de publicación ajena ni cambiar roles.

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | § 16 API-019..022, BR-006/009 | Reactivación, slug/orden/DTO pendientes; seguridad por recurso. |
| [Fase 03](../../phase-03-catalog-offers/spec.md), [plan](../../phase-03-catalog-offers/plan.md) | RF-12, Sprint 2 § 6.3 | `categories` no creada, campo `isActive` propuesto. |
| [Trazabilidad](../../../docs/traceability.md) | API-021; T-CAT-05/TASK-CATALOG-011 históricos | Método/ruta Admin, edita/reactiva; no contrato HTTP aprobado. |

## Contrato aprobado

| Regla | Fuente expresa | Límite |
|---|---|---|
| Categorías las gestiona Admin; desactivar/ocultar no borra ni modifica productos | RF-12 [fase 03](../../phase-03-catalog-offers/spec.md) | Payload/estados/HTTP pendientes. |
| Rol Admin se lee del perfil local, no del JWT ni body | [Constitución § 6](../../../docs/constitution.md) | No define autorización Admin por recurso ajeno a categoría. |

## Comportamiento implementado candidato

No existe `Modules/Catalog/`, PATCH Admin de categorías, repositorio `categories`, DTO ni pruebas específicas. La activación de vendedor API-009 no autoriza edición de categorías.

## Funcionalidad requerida faltante

PATCH parcial con control de concurrencia/normalización aprobados, reactivación segura de `categories`, invalidación de cache API-019 y efecto de visibilidad API-011/012; actualizar categoría no reescribe productos, y Orders revalida categoría activa al comprar.

## Contrato de entrada/salida

| Elemento | Aprobado | Candidato NO aprobado | Falta decidir |
|---|---|---|---|
| Actor/ruta | JWT y rol local Admin; categoryId en ruta | ObjectId hex sería consistente con Mongo, no ratificado | ID, perfil faltante, permisos/HTTP. |
| Body | Editar categoría/reactivar sin escribir productos | `name`, `order`, `isActive` son campos del modelo propuesto | Whitelist, omisión/null, slug generado/manual, rango, si `isActive` habilita reactivación y restricciones. |
| Respuesta | Categoría propia actualizada | `200` DTO y `409` CAS candidatos | HTTP/DTO, estado previo inactivo, repetición y versionamiento. |
| Error | Contrato único `correlationId`, Redis fail-closed | `400/401/403/404/409/429/503` candidatos | Conflicto slug, concurrencia, Redis/Mongo caídos y política cache. |

## Seguridad y validaciones

Aplicar [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md) y [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). JWT/blacklist, perfil Admin local, categoryId/whitelist estrictos y CAS si se aprueba; no aceptar ids, roles, sellerId ni cambio de productos en cuerpo. Rate limit distribuido fail-closed, invalidar cache de categorías/productos por contrato, no borrar/reescribir `products`; error `correlationId` sin tokens/PII en logs.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| EDT-01 | Admin y categoría existente | PATCH atributos permitidos | Categoría cambia, productos del vendedor intactos | RF-12 | Mongo integración por crear | Función confirmada, DTO pendiente |
| EDT-02 | Categoría inactiva | Admin solicita reactivación | Estado/visibilidad conforme transición aprobada; sin alterar productos | RF-12 | Unit/integración por crear | Reactivación por decidir |
| EDT-03 | Buyer, token inválido o categoría ausente | PATCH | Sin escritura, error acordado | BR-009 | Seguridad por crear | HTTP pendiente |
| EDT-04 | Dos cambios o Redis/Mongo caídos | PATCH | No se pierden actualizaciones ni se sirve estado obsoleto según protocolo | RF-12 | Integración fallo/carrera por crear | CAS/invalidez pendientes |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | DEV o dependencia |
|---|---|---|---|
| RF-12 | [Fase 03](../../phase-03-catalog-offers/spec.md) | EDT-01/02/03/04 | DEV-API021-01/02/03/04 |
| T-CAT-05, TASK-CATALOG-011 históricos | [Trazabilidad](../../../docs/traceability.md) | EDT-01/02 | DEV-API021-02/03 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| EDT-01 | NO_IMPLEMENTADO | Sin PATCH, modelo/guard | DEV-API021-02 |
| EDT-02 | BLOQUEADO | Reactivación/estado/HTTP sin ratificar | DEV-API021-01/03 |
| EDT-03 | NO_IMPLEMENTADO | Sin autorización y pruebas | DEV-API021-02/04 |
| EDT-04 | BLOQUEADO | Política CAS/invalidación no decidida | DEV-API021-01/03/04 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-13 | Inspección documental | [Fase 03 § 6.3](../../phase-03-catalog-offers/spec.md), `Modules/Catalog` ausente | PATCH/estado real ni decisión Sprint 2. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| API-019..022 Sprint 2 | [Constitución § 16](../../../docs/constitution.md), [trazabilidad](../../../docs/traceability.md) | Slug, orden, Reactivación, DTO, CAS/HTTP | DEV-API021-01/02/03/04 | Producto/API/Catalog/seguridad: acta Sprint 2 y cotejo Excel. |
| RF-12 | [Fase 03](../../phase-03-catalog-offers/spec.md) | Ocultar categoría no borra productos | DEV-API021-03 | Probar reactivación sin reescribir productos. |

## Criterios de preparación para desarrollo

Alcance: PATCH Admin propio de categoría, [BLOCKED](../../../docs/sdd-guia-api-atomica.md#estados-operativos) hasta contrato Sprint 2 y política CAS. Aceptación: categoría editada/reactivada según acta, productos intactos y lecturas/Orders coherentes con estado actual.
