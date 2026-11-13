# API-027 — Cambiar estado de pedido · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Orders dueño de estados/auditoría; Catalog del stock, Notifications del push. [Plan histórico](../../phase-04-orders-cash/plan.md) v0.1 no aprobado.

## Arquitectura existente

Sin módulo Orders ni tests; reutilizar documento/repositorio de API-023 y controlador de API-026 cuando existan, sin duplicar colección. `ICatalogProductCommands` y `INotificationPublisher` son interfaces entre dueños, no acceso directo a repositorios ajenos.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Orders/Controllers/OrdersController.cs`, `Contracts/UpdateOrderStatusRequest.cs`, `Validators/UpdateOrderStatusRequestValidator.cs` | PATCH, whitelist y contrato HTTP ratificado. |
| Aplicación | `src/UTTTGO.Api/Modules/Orders/Application/UpdateOrderStatusService.cs` | Propiedad Seller, aristas §13, coordinación de efectos. |
| Persistencia | `src/UTTTGO.Api/Modules/Orders/Infrastructure/MongoOrderRepository.cs`, `MongoOrderAuditRepository.cs` | Escritura condicional y auditoría propias (D-ORD-06). |
| Interfaces | Catalog `ICatalogProductCommands`, Notifications `INotificationPublisher` | Liberar al rechazar, publicar cambio sin FCM directo. |

## Secuencia técnica

JWT/blacklist y rate limit Redis → validar ID, destino y versión si aprobada → verificar seller propietario a través de Sellers → cargar pedido → transición permitida §13 → CAS estado/version y auditar → coordinar liberación idempotente en Rejected y notificar por interfaces; ordenar efectos, recuperación y respuesta solo tras decisión D-ORD-02/06. Si Redis falla, no ejecutar; no marcar entrega por PATCH.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| ST-01..04 | `Modules/Orders/Controllers/OrdersController.cs`, `Contracts/UpdateOrderStatusRequest.cs`, `Validators/UpdateOrderStatusRequestValidator.cs`, `Application/UpdateOrderStatusService.cs`, `src/UTTTGO.Api/Program.cs` | Transiciones y DI sin reordenar middleware, tras acta. |
| ST-01/05 | `Modules/Orders/Infrastructure/MongoOrderRepository.cs`, `MongoOrderAuditRepository.cs` | Reusar API-023; CAS y protocolo Catalog/auditoría tras D-ORD-02/06. |
| ST-01..05 | `tests/UtttGo.UnitTests/UpdateOrderStatusServiceTests.cs`, `tests/UtttGo.IntegrationTests/UpdateOrderStatusEndpointTests.cs`, `tests/UtttGo.SecurityTests/UpdateOrderStatusSecurityTests.cs` | Tests nuevos. |

## Orden de implementación

DEV-API027-01 decide gates; DEV-API027-02 controlador/validador/servicio; DEV-API027-03 CAS, auditoría y efectos; DEV-API027-04 pruebas negativas/concurrencia/recuperación. Comprobar previamente archivos de API-023/026 para ampliarlos sin duplicarlos.

## Dependencias y bloqueos

| Gate transversal o decisión particular | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| TASK-ORD-001..004, D-ORD-02/06/08/10 | DEV-API027-01..04 | Acta de fase 04, protocolo stock y Sprint 2 Catalog. |
| API-023/026/028/029, Notifications | DEV-API027-02..04 | Pedido/reserva existentes; exclusividad de entrega y publicador publicados por dueños. |

## Estrategia de pruebas

Unit: tabla §13, propiedad, versión. Integración WebApplicationFactory Mongo/Redis aislados, Catalog ledger y Notifications mock: cambio único, fallo parcial/retry, Rejected una liberación. Seguridad comprador/ajeno/revocado, Redis caído. Tras autorización: build Release, format y tres suites `dotnet test -c Release --no-build`, evidencia fechada; nada productivo.

## Seguridad, errores y observabilidad

JWT y sellerId propio del perfil, Redis fail-closed, error `correlationId`, logs sin PII/tokens; ningún push directo ni cambio de middleware.

## Criterios de cierre técnico

ST-01..05 probados con estado y stock coherentes, auditoría y publicación conforme acta; build/formato/suites verdes. No cerrar TASK-ORD históricos solo por esta documentación.
