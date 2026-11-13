# API-009 — Autoactivación de vendedor · spec

Estado de aprobación de la ficha: PARCIAL
Estado operativo: IMPLEMENTED_PENDING_VERIFICATION

## Identificación y autoridad

API-009 `POST /api/v1/sellers/activation-requests` según [trazabilidad](../../../docs/traceability.md) y [fase 03](../../phase-03-catalog-offers/spec.md) v2.1. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). Cotejo directo con Excel pendiente de fuente.

## Estado de aprobación

DEC-002/019 y Sprint 1 fase 03 aprueban autoactivación y contrato v2.1; esta ficha atómica aún requiere revisión/ratificación identificable y evidencia específica no productiva para cerrar implementación. No trasladar aprobación de Sprint 1 a otro módulo.

## Responsabilidad única

Crear/activar el perfil propio de negocio y promover rol local `Seller` de modo idempotente. No requiere aprobación Admin, no crea productos, no sube imágenes (fase 05), no implementa GET propio DEC-019.d ni PATCH API-010.

## Fuentes consultadas

| Fuente | ID original | Alcance y límite |
|---|---|---|
| [Constitución](../../../docs/constitution.md) | DEC-002/019, BR-009 | Autoactivación, identidad Sellers separada, roles solo backend. |
| [Fase 03](../../phase-03-catalog-offers/spec.md), [tareas](../../phase-03-catalog-offers/task.md) | RF-V01..05/09, T-CAT-V01/02/03/05/06/07/09, TASK-SELLER-020/041 | Contrato Sprint 1, TASK-SELLER-041 abierto. |
| [Migración](../../../docs/migracion-sdd.md), [trazabilidad](../../../docs/traceability.md) | API-009 | Con dobles; sin verificación externa específica documentada. |

## Contrato aprobado

| Regla | Autoridad | Límite |
|---|---|---|
| Autopromoción de usuario Active al completar tres campos; `sellerId` backend | DEC-002/019, RF-V01 | No rol desde cliente. |
| Repetición devuelve mismo vendedor sin duplicados | [Fase 03 § 5.1](../../phase-03-catalog-offers/spec.md) RF-V02 | Confirmar con Mongo real antes de cerrar. |
| Fallo promoción deja perfil no visible y retry seguro | RF-V03 | Evidencia con dobles solamente. |

## Comportamiento implementado candidato

[`OwnSellerController.cs`](../../../src/UTTTGO.Api/Modules/Sellers/Controllers/OwnSellerController.cs) POST 201/200; [`SellerActivationService.cs`](../../../src/UTTTGO.Api/Modules/Sellers/Application/SellerActivationService.cs) y [`MongoSellerProfileWriter.cs`](../../../src/UTTTGO.Api/Modules/Sellers/Infrastructure/MongoSellerProfileWriter.cs) coordinan vendedor con `ISellerRolePromotion` de Identity; [`SellerActivationRequestValidator.cs`](../../../src/UTTTGO.Api/Modules/Sellers/Validators/SellerActivationRequestValidator.cs) valida cuerpo. Tests con dobles existen; no hay prueba específica documentada de respuesta AWS.

## Funcionalidad requerida faltante

No falta crear endpoint: faltan evidencia de idempotencia/fallo parcial real (Mongo/Redis/Supabase no productivos), seguridad inter-réplicas y TASK-SELLER-041. Media/Catalog/Orders no pertenecen al Sprint 1.

## Contrato de entrada/salida

| Elemento | Aprobado Sprint 1 | Implementado | Falta |
|---|---|---|---|
| Entrada | Bearer JWT válido, perfil local Active; `businessName` 1..100, `description` 1..500, `pickupPoint` 1..160, tras trim; tres obligatorios | `SellerActivationRequest`, validador whitelist | Evidencia real, cotejo Excel. |
| Éxito | `201` creado; `200` repetido; `sellerId,businessName,description,pickupPoint,status,isActive,roles,createdAt,updatedAt` | `SellerOwnProfileResponse` | Verificar persistencia real. |
| Errores | `400 VALIDATION_ERROR`, `401 UNAUTHORIZED`, `403 SELLER_ACCOUNT_NOT_ACTIVE`, `404 PROFILE_NOT_FOUND`, `409 SELLER_STATE_CONFLICT`, `429 RATE_LIMITED` con Retry-After, `503 SELLER_UNAVAILABLE` | Controller traduce resultados | Verificar headers/caídas; `correlationId`. |

## Seguridad y validaciones

Usar [contratos](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md). Rechazar campos extra (`sellerId`, `roles`, `status`, `version`), nulos/vacíos; JWT/blacklist y rate limit `seller-activation` 5/900s por usuario hasheado, fail-closed. `sellers.ownerUserId` único parcial; `users.sellerId` y rol promovidos exclusivamente mediante contrato Identity. Sin credenciales o PII sensible en logs.

## Escenarios Given/When/Then

| ID local | Dado | Cuando | Entonces | RF/AC | Prueba/pendiente | Autorización |
|---|---|---|---|---|---|---|
| ACT-01 | Cuenta Active válida | POST tres campos | `201`, sellerId backend y rol local Seller | RF-V01, T-CAT-V01 | `SellerActivationEndpointTests` con dobles | Sprint 1 |
| ACT-02 | Ya activo | Repite POST | `200`, mismo ID sin duplicados | RF-V02, T-CAT-V02 | `SellerActivationServiceTests` con dobles | Sprint 1 |
| ACT-03 | Promoción falla después de crear | POST/reintenta | No visible hasta activación; sin duplicado | RF-V03, T-CAT-V06 | Dobles; Mongo real pendiente | Sprint 1 |
| ACT-04 | JWT inválido, cuenta no Active o body inválido | POST | `401/403/400`, no escritura | RF-V04/05/09 | `SellerActivationSecurityTests` con dobles | Sprint 1 |
| ACT-05 | Redis sin servicio o vendedor suspendido | POST | `503` fail-closed o `409`; no auto-reactivar | T-CAT-V09, TASK-SELLER-020 | Prueba aislada pendiente | Sprint 1 |

## Trazabilidad RF/RNF/AC/T-AUTH/TASK

| IDs originales | Fuente | Escenario | Tarea DEV |
|---|---|---|---|
| RF-V01/02/03/04/05/09, T-CAT-V01/02/03/05/06/07/09 | [Fase 03](../../phase-03-catalog-offers/spec.md) | ACT-01/02/03/04/05 | DEV-API009-01/02 |
| TASK-SELLER-020/041 | [Tareas Sprint 1](../../phase-03-catalog-offers/task.md) | ACT-01/03 | DEV-API009-02/03 |

## Matriz de brechas

| Escenario | Clasificación | Brecha | Tarea/decisión |
|---|---|---|---|
| ACT-01/02 | IMPLEMENTADO_PROBADO | Dobles; sin verificación con Mongo/Identity reales específica | DEV-API009-02 |
| ACT-03/05 | SIN_EVIDENCIA | Fallo parcial/retry entre instancias y Redis caído no acreditados externamente | DEV-API009-02 |
| ACT-04 | IMPLEMENTADO_PROBADO | Solo seguridad con dobles | DEV-API009-02 |

## Evidencia con nivel y fecha

| Fecha | Nivel | Artefacto | No acredita |
|---|---|---|---|
| 2026-11-08 | Código y tests con dobles | [Migración](../../../docs/migracion-sdd.md) Sprint 1; `SellerActivation*Tests` | Respuesta real endpoint ni idempotencia Mongo/Redis/Supabase. |

## DRIFT y decisiones pendientes

| ID | Fuente | Alternativas | Impacto/tareas | Autoridad necesaria |
|---|---|---|---|---|
| TASK-SELLER-041 | [Tareas](../../phase-03-catalog-offers/task.md) | No extrapolar `/health` a API-009 | DEV-API009-02/03 | Propietario y QA: evidencia específica autorizada. |
| TASK-AUTH-007/008/010 | [Fase 02](../../phase-02-auth-users/task.md) | Gates fase 02 no se cierran por Sprint 1 | DEV-API009-01/02 | Equipo/delegación confirma alcance aplicable. |

## Criterios de preparación para desarrollo

Alcance: código existente, [IMPLEMENTED_PENDING_VERIFICATION](../../../docs/sdd-guia-api-atomica.md#estados-operativos); ninguna tarea de reimplementación sin defecto demostrado. Para cierre: ratificación atómica, pruebas específicas con dependencias autorizadas y evidencia de activación/reintento sin acceso productivo en CI.
