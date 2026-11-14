# API-009 — Autoactivación de vendedor · plan

Estado de aprobación de la ficha: PARCIAL

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Sprint 1 aprobado, cierre/verificación de API-009 no inferido.

## Arquitectura existente

Dueño Sellers. `OwnSellerController`, `SellerActivationService`, `ISellerActivationDependencies`, `MongoSellerProfileWriter`, `SellerActivationRequestValidator` y tests `SellerActivation*Tests` existentes; rol/`users` pertenece a Identity (`ISellerRolePromotion`, `MongoSellerRolePromotion`). No crear segundo escritor.

## Componentes involucrados

| Capa | Componente | Responsabilidad |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Sellers/Controllers/OwnSellerController.cs`, `Contracts/SellerActivationRequest.cs`, `Validators/SellerActivationRequestValidator.cs` | Bearer, whitelist/longitudes, HTTP. |
| Servicio | `src/UTTTGO.Api/Modules/Sellers/Application/SellerActivationService.cs` | Idempotencia y secuencia `PendingActivation` → promoción → `Active`. |
| Adaptadores | `src/UTTTGO.Api/Modules/Sellers/Infrastructure/MongoSellerProfileWriter.cs`, `RedisSellerRateLimitGate.cs`; `src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoSellerRolePromotion.cs` | Escrituras por dueño, gate compartido. |

## Secuencia técnica

JWT/blacklist y perfil local Active → rate limit → validar tres campos y prohibidos → upsert por `ownerUserId` único parcial `PendingActivation` → solicitar promoción de rol vía Identity → activar vendedor → devolver 201 o 200 idempotente. Suspended/Deleted ⇒ 409; fallo tras upsert ⇒ 503/no visible, retry seguro; no enviar push ni media.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivo | Cambio condicionado |
|---|---|---|
| ACT-01..05 | `tests/UtttGo.IntegrationTests/SellerActivationEndpointTests.cs`, `tests/UtttGo.SecurityTests/SellerActivationSecurityTests.cs` | Completar pruebas con Mongo/Redis aislados y Supabase controlado tras autorización; no reimplementar servicio. |
| ACT-03 | `src/UTTTGO.Api/Modules/Sellers/Application/SellerActivationService.cs` | Solo corregir si prueba real demuestra defecto y existe autorización de fix. |

## Orden de implementación

DEV-API009-01 ratifica ficha/alcance de gates; DEV-API009-02 verifica flujo real/reintentos en entorno aislado; DEV-API009-03 recopila evidencia operativa permitida para TASK-SELLER-041, sin tratar `/health` como éxito de endpoint.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| TASK-SELLER-041 y permiso de pruebas | DEV-API009-02/03 | Resultados específicos en entorno autorizado, sin credenciales. |
| TASK-AUTH-007/008/010 | DEV-API009-01/02 | Alcance de aprobación de ficha y gates firmado. |
| Identity/API-002/004 | DEV-API009-02 | Contrato de perfil Active y `ISellerRolePromotion`; no escribir users desde Sellers. |

## Estrategia de pruebas

Unitarias existentes `SellerActivationServiceTests.cs`; integración WebApplicationFactory + contenedores Mongo/Redis aislados y Supabase con doble controlado; seguridad JWT ausente/revocado, 403 cuenta, 409 suspendido, 400 extra/null, 429/503. Build Release, format y tres `dotnet test` proyectos Release `--no-build` cuando se autorice; no ejecutar servicios reales ahora.

## Seguridad, errores y observabilidad

No confiar en roles/sellerId del body; gate Redis fail-closed; errores con `correlationId`; no registrar token, `jti`, PII ni payloads sensibles. No tocar middleware.

## Criterios de cierre técnico

ACT-01..05 con evidencia fechada por tipo de prueba, mismo sellerId tras retry y sin perfil visible ante fallo; build/formato/suites verdes y aprobación/evidencia operativa separadas.
