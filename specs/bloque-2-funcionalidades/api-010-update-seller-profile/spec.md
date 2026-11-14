# API-010 — Editar perfil propio de vendedor · spec

Estado de aprobación de la ficha: PARCIAL
Estado operativo: IMPLEMENTED_PENDING_VERIFICATION

## Identificación y autoridad

API-010 `PATCH /api/v1/sellers/me` según [trazabilidad](../../../docs/traceability.md) y [fase 03](../../phase-03-catalog-offers/spec.md) v2.1. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

DEC-019 y Sprint 1 fase 03 aprobaron edición propia sin media y con CAS por versión; falta ratificación individual de ficha y prueba con Mongo real para cerrar. No atribuir permiso de modificar otro vendedor ni de escribir imagen.

## Responsabilidad única

Actualizar campos permitidos del perfil de negocio del vendedor autenticado. No activa vendedores (API-009), no cambia rol/estado, no edita perfil público por ID (API-008), no sube media ni productos.

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | BR-009, DEC-019 | Propiedad estricta, sin imágenes Sprint 1. |
| [Fase 03](../../phase-03-catalog-offers/spec.md), [tareas](../../phase-03-catalog-offers/task.md) | RF-V07/08/09, T-CAT-V03/07/08/09, TASK-SELLER-022/041 | PATCH, whitelist y CAS; despliegue pendiente de evidencia específica. |
| [Fase 02](../../phase-02-auth-users/spec.md), [migración](../../../docs/migracion-sdd.md) | RF-13, T-AUTH-12, API-010 | Avatar histórico contradice Sprint 1 sin Media; prevalece DEC-019. |

## Contrato aprobado

| Regla | Autoridad | Límite |
|---|---|---|
| Editar solo propio `businessName`/`description`/`pickupPoint` | RF-V07/09, [fase 03 § 5.3](../../phase-03-catalog-offers/spec.md) | Sin media, rol/estado/ID ni null. |
| Actualización concurrente perdedora `409` por version | RF-V08 | Falta prueba Mongo real entre réplicas. |

## Comportamiento implementado candidato

[`OwnSellerController.cs`](../../../src/UTTTGO.Api/Modules/Sellers/Controllers/OwnSellerController.cs) expone PATCH; [`UpdateSellerProfileRequest.cs`](../../../src/UTTTGO.Api/Modules/Sellers/Contracts/UpdateSellerProfileRequest.cs) distingue omisiones, rechaza desconocidos/nulos; [`UpdateOwnSellerProfileService.cs`](../../../src/UTTTGO.Api/Modules/Sellers/Application/UpdateOwnSellerProfileService.cs) y [`MongoSellerProfileRepository.cs`](../../../src/UTTTGO.Api/Modules/Sellers/Infrastructure/MongoSellerProfileRepository.cs) implementan edición propia/CAS candidato. Tests `UpdateOwnSellerProfile*Tests` con dobles; sin curl específico documentado.

## Funcionalidad requerida faltante

No falta crear ruta; faltan prueba de concurrencia con Mongo aislado, fallo Redis y evidencia de PATCH no productivo. Edición de avatar depende de ficha Media fase 05 y decisión posterior; no implementarla aquí.

## Contrato de entrada/salida

| Elemento | Aprobado Sprint 1 | Implementado | Falta |
|---|---|---|---|
| Entrada | Bearer válido vendedor propio; JSON objeto con ≥1 de tres campos opcionales (`businessName` 1..100, `description` 1..500, `pickupPoint` 1..160); null/desconocidos `400` | `UpdateSellerProfileRequest.TryParse` y validador; sin sellerId | Cotejo Excel. |
| Éxito | `200` perfil actualizado, version incrementada internamente; respuesta sin `version` | `SellerOwnProfileResponse`: sellerId, campos, status, isActive, roles y fechas | Verificar Mongo y relectura. |
| Error | `400 VALIDATION_ERROR`, `401 UNAUTHORIZED`, `404 SELLER_PROFILE_NOT_FOUND`, `409 SELLER_CONFLICT`, `429 RATE_LIMITED` con Retry-After, `503 SELLER_UNAVAILABLE` | Controller mapea estados | Evidencia fallos reales y correlación. |

## Seguridad y validaciones

[Contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Sujeto JWT validado contra perfil local/blacklist; propietario `ownerUserId` por servidor, CAS `version` al escribir `sellers`; rate limit `seller-write` 30/60s por usuario hasheado, Redis fail-closed. No aceptar URL/`mediaAssetId`, roles, status, sellerId, ni log de token/PII; solo Sellers toca `sellers`.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| EDIT-01 | Vendedor autenticado | PATCH campo permitido | `200`, solo perfil propio actualizado; omisiones intactas | RF-V07 | `UpdateOwnSellerProfileEndpointTests` con dobles | Sprint 1 |
| EDIT-02 | Dos versiones concurrentes | Ambas hacen PATCH | Una gana, otra `409`; sin pérdida | RF-V08, T-CAT-V08 | `UpdateOwnSellerProfileServiceTests` con dobles; Mongo pendiente | Sprint 1 |
| EDIT-03 | Body inválido o usuario sin vendedor | PATCH | `400/404` sin escritura | RF-V09, T-CAT-V07 | Tests con dobles | Sprint 1 |
| EDIT-04 | JWT ajeno/revocado o Redis caído | PATCH | `401/503`, sin afectar otro perfil | BR-009, T-CAT-V03/09 | Seguridad dobles, entorno real pendiente | Sprint 1 |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | Tarea DEV |
|---|---|---|---|
| RF-V07/08/09, T-CAT-V03/07/08/09 | [Fase 03](../../phase-03-catalog-offers/spec.md) | EDIT-01/02/03/04 | DEV-API010-01/02 |
| TASK-SELLER-022/041 | [Tareas](../../phase-03-catalog-offers/task.md) | EDIT-01/02 | DEV-API010-02/03 |
| RF-13, T-AUTH-12 | [Fase 02](../../phase-02-auth-users/spec.md) | EDIT-03 | DEV-API010-01 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| EDIT-01/03 | IMPLEMENTADO_PROBADO | Solo pruebas con dobles; falta ratificación individual y Mongo | DEV-API010-01/02 |
| EDIT-02 | SIN_EVIDENCIA | CAS de Mongo real no acreditado | DEV-API010-02 |
| EDIT-04 | SIN_EVIDENCIA | Revocación y caída Redis con servicios aislados sin prueba específica | DEV-API010-02 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-08 | Código y dobles | [Migración](../../../docs/migracion-sdd.md), `UpdateOwnSellerProfile*Tests` | CAS Mongo real, respuesta AWS de PATCH o gate Redis real. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| T-AUTH-12 vs DEC-019.e | [Fase 02](../../phase-02-auth-users/spec.md), [constitución](../../../docs/constitution.md) | Avatar previsto históricamente vs Sprint 1 sin imágenes | DEV-API010-01; Media separado | Dueño Media/producto decide futura asociación por ficha, no API-010 actual. |
| TASK-SELLER-041 | [Tareas](../../phase-03-catalog-offers/task.md) | Salud del stack no acredita PATCH | DEV-API010-03 | Propietario/QA autoriza y registra evidencia específica. |

## Criterios de preparación para desarrollo

Alcance: implementación existente, [IMPLEMENTED_PENDING_VERIFICATION](../../../docs/sdd-guia-api-atomica.md#estados-operativos). No reimplementar PATCH. Cerrar con acta individual y evidencia de CAS real/propiedad y fallo Redis en entorno autorizado; no media hasta ficha aprobada.
