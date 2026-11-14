# API-029 — Confirmar entrega y efectivo · plan

Estado de aprobación de la ficha: PENDIENTE

## Autoridad y alcance

[Constitución](../../../docs/constitution.md) → [spec](spec.md) → plan → [task](task.md). Orders dueño de entrega/auditoría; Catalog stock, Reviews reseñas, Notifications push; [plan fase 04](../../phase-04-orders-cash/plan.md) candidato.

## Arquitectura existente

No hay Orders/Reviews implementados. Reutilizar documento `orders` y comando idempotente Catalog previsto API-023, hash de API-028 y repositorio auditoría API-027; no duplicar colecciones ni escribir `reviews` desde Orders.

## Componentes involucrados

| Capa | Componente actual o previsto | Responsabilidad y fuente |
|---|---|---|
| API | `src/UTTTGO.Api/Modules/Orders/Controllers/OrdersController.cs`, `Contracts/DeliveryConfirmationRequest.cs`, `Contracts/DeliveryConfirmationResponse.cs`, `Validators/DeliveryConfirmationRequestValidator.cs` | POST, validación sin loguear token. |
| Aplicación | `src/UTTTGO.Api/Modules/Orders/Application/ConfirmDeliveryService.cs` | Propiedad, token, transición, efectos idempotentes. |
| Datos | `src/UTTTGO.Api/Modules/Orders/Infrastructure/MongoOrderRepository.cs`, `MongoOrderAuditRepository.cs` | CAS de token+estado y auditoría; recuperación pendiente. |
| Contratos | Catalog `ICatalogProductCommands`; Reviews contrato de hecho entregado; Notifications `INotificationPublisher` | Commit stock y publicación acíclica, no acceso a colecciones ajenas. |

## Secuencia técnica

JWT/Redis rate limit → seller propio/Ready → validar hash vigente y expiración → consumir token y mover Ready→Delivered por actualización condicional (momento de pago recibido) → confirmar reserva Catalog por clave idempotente → auditar, publicar hecho para Reviews y notificar por interfaces. Definir antes protocolo persistente/reconciliación si falla entre pasos, sin afirmar transacción cross-module ni marcar una reseña automáticamente; repetir no confirma ni publica dos veces.

## Componentes reutilizables del Bloque 1

[Base modular](../../bloque-1-arquitectura/base-modular/spec.md), [errores](../../bloque-1-arquitectura/contratos-transversales/spec.md), [Mongo](../../bloque-1-arquitectura/persistencia-mongodb/spec.md), [Redis](../../bloque-1-arquitectura/redis-rate-limit-revocacion/spec.md).

## Archivos que se crearán o modificarán cuando corresponda

| Escenario/brecha | Ruta del archivo o componente | Cambio condicionado; decisión previa |
|---|---|---|
| DEL-01/03/04 | `Modules/Orders/Controllers/OrdersController.cs`, `Contracts/DeliveryConfirmationRequest.cs`, `Contracts/DeliveryConfirmationResponse.cs`, `Validators/DeliveryConfirmationRequestValidator.cs`, `Application/ConfirmDeliveryService.cs`, `src/UTTTGO.Api/Program.cs` | Guard/validador/DI tras acta, middleware intacto. |
| DEL-01/02/05 | `Modules/Orders/Infrastructure/MongoOrderRepository.cs`, `MongoOrderAuditRepository.cs`, `Application/ConfirmDeliveryService.cs` | CAS token+estado y coordinación Catalog/Reviews/Notifications tras protocolo. |
| DEL-01..05 | `tests/UtttGo.UnitTests/ConfirmDeliveryServiceTests.cs`, `tests/UtttGo.IntegrationTests/ConfirmDeliveryEndpointTests.cs`, `tests/UtttGo.SecurityTests/ConfirmDeliverySecurityTests.cs` | Tests locales nuevos. |

## Orden de implementación

DEV-API029-01 ratifica token/protocolo; DEV-API029-02 guard/CAS; DEV-API029-03 commit Catalog y auditoría recuperable; DEV-API029-04 hecho Reviews/notificación con idempotencia; DEV-API029-05 pruebas de fallos y seguridad.

## Dependencias y bloqueos

| Gate transversal o decisión particular | Tarea DEV afectada | Evidencia para desbloquear |
|---|---|---|
| TASK-ORD-001..004, D-ORD-02/03/06/08 | DEV-API029-01..05 | Acta fase 04 y Sprint 2, protocolo de fallo parcial, token API-028. |
| API-023/027/028/030, Notifications | DEV-API029-02..05 | Reserva, Ready, token y contrato hecho Reviews acíclico aprobados; FCM no bloquea estado si política ratificada. |

## Estrategia de pruebas

Unit token falso/expirado/usado y máquina; WebApplicationFactory Mongo/Redis locales, dobles Catalog ledger/Reviews/Notifications: carrera token, caída después CAS/antes commit/auditoría/hecho y replay sin duplicar; seguridad propietario/revocado/Redis caído. Tras acta: build Release, format y tres suites `dotnet test -c Release --no-build` con resultados fechados, sin producción.

## Seguridad, errores y observabilidad

Seller propio por perfil, JWT/blacklist y Redis fail-closed, secreto ausente de logs/error/auditoría, `correlationId`; ningún push cliente ni modificación del middleware.

## Criterios de cierre técnico

DEL-01..05 probados con un commit de stock y hecho Reviews único/recuperable; build/formato/suites verdes, sin declarar aprobada política de reseñas.
