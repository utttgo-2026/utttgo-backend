# task.md — Pedidos y efectivo · Backend UTTT-GO
> Fase `phase-04-orders-cash` · Versión 0.1 · **Borrador para aprobación del propietario** · 2026-11-11.
> Deriva de `spec.md` v0.1 → `plan.md` v0.1. Ninguna tarea autoriza escribir código en `src/` hasta la aprobación del propietario (D-ORD-01..10) y el gate de fase-03 Sprint 2. Las tareas se marcan solo con su "Hecho cuando" cumplido y pruebas verdes.

## Gates previos (documentales)

- [ ] **TASK-ORD-001 — Aprobar spec de la fase 04.** RF-ORD-01..32, API-023..029, máquina §13 y token de entrega. Hecho cuando: `spec.md` v0.1 aprobado por el propietario con contratos, códigos HTTP, modelo de datos y casos T-ORD-01..20 revisados, sin rutas inventadas. **Pendiente de aprobación.**
- [ ] **TASK-ORD-002 — Resolver D-ORD-01..10.** Hecho cuando: cada decisión de `spec.md` §9.2 tiene veredicto explícito (aprobar / rechazar / modificar) y el spec queda actualizado; D-ORD-09 permanece bloqueada hasta DEC-003. **Pendiente de aprobación.**
- [ ] **TASK-ORD-003 — Aprobar plan y dependencias.** Hecho cuando: `plan.md` v0.1 y `task.md` v0.1 alineados con el spec, con fronteras de módulo, modelo de datos, rate limits, verificación y gates. **Pendiente de aprobación.**
- [ ] **TASK-ORD-004 — Confirmar gate de fase-03 Sprint 2.** Hecho cuando: `ICatalogProductReader`/`ICatalogProductCommands` están aprobados/implementados y disponibles para consumir. **Bloqueado:** requiere el Sprint 2 del catálogo.

## Implementación (solo tras TASK-ORD-001..004)

### Datos y contratos
- [ ] **TASK-ORD-010 — Documento y escritura de `orders`.** RF-ORD-10/11/18/31. Depende de: 001/004. Hecho cuando: `OrderDocument` con snapshot `unitPrice`/`total`/`productName`/`pickupPoint`, `status`, `reservationId`, token hash, `completedAt`, `version`, `createdAt`/`updatedAt` UTC; escritor idempotente con CAS por `version` y los cuatro índices de `plan.md` §2 (incluido el único parcial de `idempotencyKey`).
- [ ] **TASK-ORD-011 — Auditoría `orderAuditLogs`.** RF-ORD-10. Depende de: 010. Hecho cuando: cada creación, transición, cancelación, emisión de token y confirmación de entrega inserta un registro inmutable con `actorUserId`, `actorRole`, `action`, `fromStatus`/`toStatus`, `correlationId` e `occurredAt`, más sus dos índices.
- [ ] **TASK-ORD-012 — Contratos de salida e interior.** RF-ORD-26/29. Depende de: 001. Hecho cuando: `IOrderReader` (participantes, estado, `completedAt`, métricas) publicado para Chat/Admin; Orders consume `ICatalogProductReader/Commands`, `ISellerProfileReader`, `ILocalProfileReader`, el hecho de Reviews y `INotificationPublisher`; sin dependencia inversa ni `IOrderEventPublisher` inverso.
- [ ] **TASK-ORD-013 — DTOs y validadores.** RF-ORD-14/30/31. Depende de: 001. Hecho cuando: `CreateOrderRequest` (solo `productId`, `quantity`), `UpdateOrderStatusRequest`, `DeliveryConfirmationRequest`, `OrderResponse`, `OrdersPageResponse` con whitelist estricta (campos prohibidos `price`/`total`/`sellerId`/`status`/`paymentMethod`/`currency` → `400` o ignorados según spec), FluentValidation (cantidad 1..`Orders:MaxQuantityPerOrder`, `ObjectId` válido, `page`/`pageSize` acotados) y el contrato de error único.

### Servicios y rutas
- [ ] **TASK-ORD-020 — Creación de pedido (API-023).** RF-ORD-10..14/28/32. Depende de: 010/012/013. Hecho cuando: valida producto/vendedor/precio vía `ICatalogProductReader`, reserva stock idempotente vía `ICatalogProductCommands`, inserta `Pending` con snapshot y total server-side, es idempotente con `Idempotency-Key` (D-ORD-04), responde `404`/`409 INSUFFICIENT_STOCK`/`422 PRODUCT_NOT_AVAILABLE`/`503` sin reserva huérfana.
- [ ] **TASK-ORD-021 — Listado y detalle (API-024, API-025).** RF-ORD-30/09/15. Depende de: 010/013. Hecho cuando: `/mine` pagina `page`+`pageSize` con máximo configurable, filtra por participante (y `role` si D-ORD-05 se aprueba), el detalle responde solo a participantes (`404` a terceros) y el payload expone los campos de `spec.md` §5.3 sin datos ajenos.
- [ ] **TASK-ORD-022 — Cancelación (API-026).** RF-ORD-19/20. Depende de: 020. Hecho cuando: solo el comprador cancela desde `Pending` (o `Accepted` si D-ORD-07), libera la reserva de forma idempotente, fija `completedAt`, audita, publica notificación y responde `200` idempotente en repetición.
- [ ] **TASK-ORD-023 — Transiciones del vendedor (API-027).** RF-ORD-21/22/16/17/18. Depende de: 010/013. Hecho cuando: solo el vendedor propietario aplica `Pending→Accepted|Rejected`, `Accepted→InPreparation`, `InPreparation→Ready` con CAS por `version` (`409 ORDER_VERSION_CONFLICT`), rechaza `Delivered`/`CancelledByBuyer` con `409`, audita y publica notificación; `Rejected` libera reserva y fija `completedAt`.
- [ ] **TASK-ORD-024 — Token de entrega (API-028).** RF-ORD-23/24. Depende de: 020. Hecho cuando: solo el comprador genera token en `Ready`, se almacena solo el hash SHA-256 con TTL (`Orders:DeliveryTokenTtlSeconds`), un token nuevo invalida al anterior, rate limit `order-delivery-token` aplicado y el token nunca aparece en logs ni en errores.
- [ ] **TASK-ORD-025 — Confirmación de entrega (API-029).** RF-ORD-24/25/26. Depende de: 024. Hecho cuando: solo el vendedor valida el token (ausente/no coincide → `422`, expirado → `422`, usado → `409`), aplica `Ready→Delivered` con CAS, fija `deliveredAt`/`completedAt`, marca el token usado de forma atómica, confirma la reserva de stock, audita, publica notificación y el hecho de Reviews.
- [ ] **TASK-ORD-026 — Rate limiting y DI.** RF-ORD-27. Depende de: 004. Hecho cuando: las seis políticas `order-*` de `plan.md` §4 están en `RateLimitingOptions`/`appsettings.json`, particionadas y hasheadas; Redis caído produce fail-closed sin ejecutar la operación; `Program.cs` registra servicios, validadores, gates e índices sin alterar el orden del middleware.

### Pruebas
- [ ] **TASK-ORD-030 — Unitarias.** Depende de: 020..025. Hecho cuando: `OrderCreationServiceTests`, `OrderTransitionServiceTests`, `OrderDeliveryTokenTests`, `OrderCancellationServiceTests` y validadores cubren T-ORD-01..11, 13, 18 y la máquina de estados completa.
- [ ] **TASK-ORD-031 — Integración.** Depende de: 020..026. Hecho cuando: `OrderEndpointTests` (API-023..029) con `WebApplicationFactory` y dobles cubre T-ORD-12, 14..17, 19, 20, todos los códigos de `plan.md` §3 y la forma exacta del JSON de éxito y del error.
- [ ] **TASK-ORD-032 — Seguridad y concurrencia.** Depende de: 030/031. Hecho cuando: `OrderSecurityTests` verifica token ausente/inválido/expirado/revocado, no participante `404`, rol equivocado `403`, campos privilegiados del cliente rechazados, token de entrega no filtrado, rate limit agotado y Redis caído fail-closed; `OrderConcurrencyTests` cubre carrera de `version`, `Idempotency-Key` duplicado y token reutilizado.

### Cierre
- [ ] **TASK-ORD-040 — Verificación local y CI.** Depende de: 030/031/032. Hecho cuando: `dotnet restore UTTTGO.Backend.slnx`, `dotnet build UTTTGO.Backend.slnx -c Release --no-restore` sin errores, `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore` sin diferencias, las tres suites (`UnitTests`/`IntegrationTests`/`SecurityTests`) en verde, `docker compose config` válido y smoke local de `/health` y de API-023..029; CI-1 en verde en la PR.
- [ ] **TASK-ORD-041 — Evidencia de despliegue en la EC2.** Depende de: 040 y merge a `main`. Hecho cuando: CD-1 despliega el SHA de la PR y la evidencia registra `/health` en `Healthy` y los pedidos funcionando en `3.129.142.54` con una cuenta real `@uttt.edu.mx` confirmada.
- [ ] **TASK-ORD-042 — Trazabilidad y memoria.** Depende de: 030/031/032. Hecho cuando: `memoria.md` registra el mapa de colecciones (`orders`, `orderAuditLogs`) y el estado de la fase 04; `docs/traceability.md` actualiza API-023..API-029; `PROJECT_STATUS.md` refleja el estado real; se propone D-ORD-10 (alineación `ApiError` con §14) como tarea transversal.

## Bloqueos conocidos

- **TASK-ORD-001/002/003:** pendientes de aprobación del propietario; sin ellas no se escribe código.
- **TASK-ORD-004:** bloqueado por la fase-03 Sprint 2.
- **D-ORD-07:** `Accepted → CancelledByBuyer` permanece prohibida (`409`) hasta aprobación explícita.
- **D-ORD-09:** borrado de cuenta bloqueado hasta que DEC-003 defina el diseño; no se implementa handler parcial.
- **D-ORD-05:** sin aprobación, `/orders/mine` sigue siendo solo del comprador y el listado del vendedor queda sin ruta (no se crea ninguna fuera del inventario).
