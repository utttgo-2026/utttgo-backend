# API-010 — Editar perfil propio de vendedor · plan

Estado de aprobación de la ficha: PARCIAL

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). PATCH Sprint 1, sin avatar, rol ni estado.

## Arquitectura existente

Dueño Sellers: `OwnSellerController`, `UpdateSellerProfileRequest`, `UpdateSellerProfileRequestValidator`, `UpdateOwnSellerProfileService`, `MongoSellerProfileRepository` y `RedisSellerRateLimitGate` ya existen; tests `UpdateOwnSellerProfile*Tests` con dobles. JWT/perfil en Identity, no duplicar su repositorio.

## Componentes involucrados

| Capa | Componente | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Sellers/Controllers/OwnSellerController.cs`, `Contracts/UpdateSellerProfileRequest.cs`, `Validators/UpdateSellerProfileRequestValidator.cs` | Whitelist, PATCH, error. |
| Aplicación | `src/UTTTGO.Api/Modules/Sellers/Application/UpdateOwnSellerProfileService.cs` | Resolver propio, omisiones y concurrencia. |
| Infraestructura | `src/UTTTGO.Api/Modules/Sellers/Infrastructure/MongoSellerProfileRepository.cs`, `RedisSellerRateLimitGate.cs` | Escritura CAS y límite distribuido. |

## Secuencia técnica

Validar Bearer/perfil local → validar objeto y campos conocidos → Redis gate → leer vendedor propio vía `ownerUserId` → CAS en `sellers` con version → devolver proyección o `409` si cambió; Redis/Mongo indisponibles ⇒ `503`. No usar sellerId del body ni tocar `users`.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivo | Cambio condicionado |
|---|---|---|
| EDIT-01..04 | `tests/UtttGo.IntegrationTests/UpdateOwnSellerProfileEndpointTests.cs`, `tests/UtttGo.SecurityTests/UpdateOwnSellerProfileSecurityTests.cs` | Añadir pruebas CAS/propiedad/fallo con Mongo/Redis locales autorizados; tests actuales no acreditan integraciones. |
| EDIT-02 | `src/UTTTGO.Api/Modules/Sellers/Infrastructure/MongoSellerProfileRepository.cs` | Solo corregir con defecto reproducible y autorización separada; no refactor preventivo. |

## Orden de implementación

DEV-API010-01 coteja contrato Sprint 1 y gate; DEV-API010-02 prueba CAS real/propiedad con contenedores; DEV-API010-03 obtiene evidencia de PATCH autorizada para TASK-SELLER-041. Media se documenta en su fase cuando se apruebe.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| TASK-SELLER-041 y autorización no productiva | DEV-API010-02/03 | Mongo/Redis aislados, respuesta PATCH específica. |
| TASK-AUTH-007/008/010 | DEV-API010-01/02 | Acta que aclare alcance de aprobación por ficha. |
| API-009 / Identity | DEV-API010-02 | Vendedor activo y guard JWT; no implementar autoactivación aquí. |

## Estrategia de pruebas

Unitarias `UpdateOwnSellerProfileServiceTests.cs` existentes; ampliar integración WebApplicationFactory + Mongo/Redis aislados para carrera dos PATCH, versión/relectura, omisiones, cuerpo extra; seguridad JWT revocado, recurso ajeno, 429/503. Registrar pruebas específicas fechadas con build Release, format y tres `dotnet test` Release `--no-build`; no llamar producción.

## Seguridad, errores y observabilidad

Autorizar por sujeto y `ownerUserId` más CAS; error único `correlationId`; logs sin token/jti/PII ni body; Redis fail-closed. No modificar middleware.

## Criterios de cierre técnico

EDIT-01..04 con evidencia de CAS y protección entre usuarios, suites/build/formato verdes sin secretos; TASK-SELLER-041 y ratificación se cierran por actos independientes.
