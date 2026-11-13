# API-008 — Perfil público de vendedor · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Solo proyección pública; sin implementación de Catalog/Reviews/Orders.

## Arquitectura existente

Dueño Sellers: `SellersController`, `SellerProfileService`, `MongoSellerProfileRepository`, `RedisPublicSellerRateLimitGate`, `SellerProfileResponse`, `SellerIdRequestValidator`; tests `SellerProfile*Tests`. Código implementado con pruebas con dobles y lectura AWS limitada; no recrear controlador/repositorio.

## Componentes involucrados

| Capa | Componente actual | Responsabilidad |
|---|---|---|
| API/DTO | `src/UTTTGO.Api/Modules/Sellers/Controllers/SellersController.cs`, `Contracts/SellerProfileResponse.cs`, `Validators/SellerIdRequestValidator.cs` | Validar ID, proyectar sin PII. |
| Aplicación | `src/UTTTGO.Api/Modules/Sellers/Application/SellerProfileService.cs`, `ISellerProfileDependencies.cs` | Filtrar estados, usar Ratings por contrato. |
| Infraestructura | `src/UTTTGO.Api/Modules/Sellers/Infrastructure/MongoSellerProfileRepository.cs`, `RedisPublicSellerRateLimitGate.cs` | Leer `sellers`, limitar distribución. |

## Secuencia técnica

Tras aprobar acceso/DTO: validar ID → rate limit Redis → leer `sellers` → filtrar Pending/Suspended/Deleted; Inactive visible → solicitar ratings por contrato Reviews si existe, degradar a null sin inventar puntuación → devolver proyección. Error Redis/Mongo `503`; no acceder a `products` ni a `reviews` directamente.

## Componentes reutilizables del Bloque 1

[Base](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Brecha | Archivo | Cambio condicionado |
|---|---|---|
| PUB-01/03 | `src/UTTTGO.Api/Modules/Sellers/Controllers/SellersController.cs`, `Contracts/SellerProfileResponse.cs` | Solo si contrato ratificado difiere del candidato; no retrabajo por defecto. |
| PUB-04 | `src/UTTTGO.Api/Modules/Sellers/Application/ISellerProfileDependencies.cs`, adaptador de Reviews del dueño | Conectar lector autorizado cuando Reviews exista; no implementar Reviews aquí. |
| PUB-01..04 | `tests/UtttGo.IntegrationTests/SellerProfileEndpointTests.cs`, `tests/UtttGo.SecurityTests/SellerProfileSecurityTests.cs` | Añadir pruebas Mongo/Redis aislados, visibilidad, PII y caída. |

## Orden de implementación

DEV-API008-01 obtiene decisión; DEV-API008-02 consume interfaz Reviews solo cuando exista y sea aprobada; DEV-API008-03 prueba con Mongo/Redis no productivos y verifica drift del contrato. API-011/Orders quedan dependencias externas, no tareas de implementación aquí.

## Dependencias y bloqueos

| Gate | Tarea | Evidencia |
|---|---|---|
| TASK-AUTH-005/026, DF02-08 | DEV-API008-01/02/03 | Acta acceso/HTTP/DTO y gates 007→008→010. |
| Reviews, API-011, Orders | DEV-API008-02/03 | Contrato Ratings; catálogo y bloqueo de pedidos comprobados por sus dueños. |

## Estrategia de pruebas

Unitarias existentes `SellerProfileServiceTests.cs`; integración `SellerProfileEndpointTests.cs` y seguridad `SellerProfileSecurityTests.cs` con dobles ya reportados en QA 2026-11-04. Complementar con Mongo/Redis aislados para `Active`/`Inactive`, 400/404/429/503; Reviews mockeado sin cero inventado; no invocar producción. Al habilitar: build Release, format y tres proyectos `dotnet test` Release `--no-build`, registrar fecha/resultado.

## Seguridad, errores y observabilidad

Rate limit distribuido fail-closed, `correlationId`, sin email/teléfono/tokens ni PII en logs; HTTP público sigue candidato. No alterar middleware; prohibición de pedidos corresponde a Orders.

## Criterios de cierre técnico

PUB-01..04 verificados según acta y evidencia por entorno; tests/formato/build verdes sin secretos, TASK-AUTH-026 solo se cierra por sus propios criterios.
