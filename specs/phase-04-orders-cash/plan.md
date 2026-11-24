# plan.md — Pedidos y efectivo · Backend UTTT-GO
> Fase `phase-04-orders-cash` · Versión 0.4 · **Fase 1 (API-023..025) y Fase 2 (API-026..029) aprobadas e implementadas el 2026-11-16; ficha nueva `GET /api/v1/sellers/me/orders` aprobada el 2026-11-18 (D-ORD-05)** · 2026-11-18.
> Deriva de `spec.md` v0.5 y `docs/constitution.md` v2.6 (BR-001, BR-002, BR-003, BR-009, BR-010, §13, §14). Las decisiones D-ORD-01..20 quedan registradas en `spec.md` §0 y §9.2.

## 1. Alcance y fronteras

**Autoriza (tras aprobación):** el módulo `Orders` con las fichas API-023..API-029 y la **ficha nueva aprobada `GET /api/v1/sellers/me/orders`** (D-ORD-05), más las colecciones propias `orders` y `orderAuditLogs`. Estructura objetivo: `src/UTTTGO.Api/Modules/Orders/{Contracts,Application,Infrastructure,Validators,Controllers}` (§4.5).

**Orders consume (solo contratos publicados por el módulo dueño):**

| Contrato (dueño) | Propósito en Orders |
|---|---|
| `ICatalogProductReader` (Catalog) | validar producto `Published`, categoría activa, vendedor `Active`, precio vigente y stock disponible |
| `ICatalogProductCommands` (Catalog) | reservar / liberar / confirmar stock con `reservationId` (D-ORD-02) |
| `ISellerProfileReader` (Sellers) | estado del vendedor y `pickupPoint` para snapshot |
| `ILocalProfileReader` (Identity) | cuenta `Active` e identidad del actor |
| contrato de hecho «pedido entregado» (Reviews) | publicar el hecho al entregar (R-1: Reviews nunca consulta Orders) |
| `INotificationPublisher` (Notifications) | encolar notificación de cambio de estado (BR-008) |

**Orders publica:**

| Contrato | Consumidores |
|---|---|
| `IOrderReader` (participantes, estado, `completedAt`, métricas agregadas) | Chat (fase-07), Admin (fase-10) |

**Fronteras innegociables:** Orders escribe únicamente `orders` y `orderAuditLogs`; nunca lee ni escribe `products`, `categories`, `mediaAssets`, `sellers`, `users`, `reviews`, `notifications`, `chats`, `favorites` ni colecciones de Admin. No existe dependencia inversa desde Notifications, Reviews, Chat o Admin hacia Orders ni `IOrderEventPublisher` inverso (evita ciclos §4.4). No hay carrito, líneas de pedido, menús ni pasarela (BR-001..004). El precio, el vendedor, el total, el estado y el método de pago se derivan server-side (BR-009/BR-010); ninguna ruta acepta esos campos del cliente como autoridad.

**Fase 2 (implementada):** toda mutación del pedido pasa por **un único CAS** en MongoDB (`ApplyAsync`): el filtro exige `_id`, `status` esperado, `version` esperado y, en la entrega, el hash del token; el update incrementa `version` y fija `updatedAt`. Si el CAS no aplica, el servicio relee el pedido y distingue `409 ORDER_VERSION_CONFLICT` (cambió la versión) de `409 ORDER_STATE_CONFLICT` (cambió el estado). El token de entrega se genera con 32 bytes aleatorios (base64url), se guarda **solo** su hash SHA-256 con `expiresAt` (TTL configurable en `Orders:DeliveryTokenTtlSeconds`, 900 s por defecto) y se marca usado en el mismo documento (single-use). `Ready → Delivered` es la **única** vía para el estado terminal de pago (D-ORD-03) y el QR lo dibuja el móvil a partir del token (D-ORD-16). La notificación push y el hecho de Reviews quedan diferidos a sus fases (D-ORD-15).

**Gate previo:** fase-03 Sprint 2 (catálogo, `ICatalogProductReader/Commands`) aprobada y verificada. Fases 06 (notificaciones FCM) y 07 (chat) solo consumen hechos; no bloquean este plan.

## 2. Modelo de datos

| Colección / dueño | Campos e invariantes | Índices |
|---|---|---|
| `orders` / Orders | `_id`, `buyerUserId`, `sellerId`, `productId`, `productName` (snapshot), `quantity` (≥1), `unitPrice` y `total` (decimal MXN, snapshot al crear — D-ORD-01), `currency` `"MXN"`, `paymentMethod` `"Cash"`, `status` (enum §13), `reservationId`, `pickupPoint` (snapshot), `deliveryTokenHash`/`deliveryTokenExpiresAt`/`deliveryTokenUsedAt` (SHA-256, nunca el token en claro), `deliveredAt`, `completedAt` (terminales — D-ORD-08), `version` (`int64`, CAS — D-ORD-06), `idempotencyKey` (opcional — D-ORD-04), `createdAt`/`updatedAt` (UTC). | `{buyerUserId:1, createdAt:-1}`; `{sellerId:1, createdAt:-1}`; `{status:1, updatedAt:-1}`; **único parcial** `{buyerUserId:1, idempotencyKey:1}` where `idempotencyKey` existe. |
| `orderAuditLogs` / Orders | `_id`, `orderId`, `actorUserId`, `actorRole` (`buyer`/`seller`), `action` (`Created`/`StatusChanged`/`Cancelled`/`DeliveryTokenIssued`/`DeliveryConfirmed`), `fromStatus`/`toStatus`, `correlationId`, `occurredAt` (UTC). Inmutable. | `{orderId:1, occurredAt:-1}`; `{actorUserId:1, occurredAt:-1}`. |

**Estados (§13):** `Pending → Accepted → InPreparation → Ready → Delivered` (terminal); `Rejected` y `CancelledByBuyer` terminales; `Accepted → CancelledByBuyer` solo si D-ORD-07 está aprobado (por defecto `409`). `Ready → Delivered` **solo** por API-029 con token (D-ORD-03). Transición inválida → `409 ORDER_STATE_CONFLICT`.

**Secuencia de creación (sin transacciones multi-documento, Atlas Free Tier):** (1) validar producto/vendedor/precio vía `ICatalogProductReader`; (2) reservar stock vía `ICatalogProductCommands.ReserveStockAsync` con `reservationId` idempotente; (3) insertar `orders` en `Pending` con snapshot; si (3) falla, compensar liberando la reserva. Si (2) falla, no se inserta el pedido. Ningún paso deja reserva huérfana sin compensación (RF-ORD-32).

**Liberación/commit de stock:** cancelación/rechazo → liberar idempotente por `reservationId`; entrega (API-029) → confirmar (commit). Repetir la operación no libera ni confirma dos veces.

**Token de entrega:** generación solo en `Ready`; hash SHA-256 + `expiresAt` (TTL configurable, 900 s por defecto); token nuevo invalida al anterior; consumo marcado con update condicional (single-use). Rate limit estricto y sin log del token en claro.

**Idempotencia de creación:** `Idempotency-Key` opcional con índice único parcial; repetir devuelve el mismo pedido sin doble reserva (D-ORD-04).

## 3. Contratos HTTP

| Ficha | Método y ruta | Acceso | Códigos |
|---|---|---|---|
| API-023 | `POST /api/v1/orders` | Bearer + cuenta `Active` | `201` · `200` (replay de `Idempotency-Key`, D-ORD-13) · `400` `VALIDATION_ERROR` / `INSUFFICIENT_STOCK` (D-ORD-11) · `401` · `403` `ACCOUNT_NOT_ACTIVE` · `404` `PRODUCT_NOT_FOUND` · `409` `ORDER_VERSION_CONFLICT` · `422` `PRODUCT_NOT_AVAILABLE` · `429` `RATE_LIMITED` · `503` `ORDER_UNAVAILABLE` |
| API-024 | `GET /api/v1/orders/mine` | Bearer | `200` (paginado `{items,page,pageSize,totalCount}`) · `400` · `401` · `429` · `503` |
| API-025 | `GET /api/v1/orders/{orderId}` | Bearer + participante | `200` · `401` · `404` `ORDER_NOT_FOUND` · `429` · `503` |
| API-026 | `POST /api/v1/orders/{orderId}/cancel` | Bearer + comprador | `200` · `401` · `403` `ORDER_FORBIDDEN` · `404` · `409` `ORDER_STATE_CONFLICT` · `429` · `503` |
| API-027 | `PATCH /api/v1/orders/{orderId}/status` | Bearer + vendedor | `200` · `400` · `401` · `403` · `404` · `409` `ORDER_STATE_CONFLICT` / `ORDER_VERSION_CONFLICT` · `429` · `503` |
| API-028 | `POST /api/v1/orders/{orderId}/delivery-token` | Bearer + comprador + `Ready` | `201` · `401` · `403` · `404` · `409` `ORDER_STATE_CONFLICT` · `429` · `503` |
| API-029 | `POST /api/v1/orders/{orderId}/delivery-confirmations` | Bearer + vendedor + token | `200` · `400` · `401` · `403` · `404` · `409` `ORDER_STATE_CONFLICT` / `DELIVERY_TOKEN_ALREADY_USED` · `422` `DELIVERY_TOKEN_INVALID` / `DELIVERY_TOKEN_EXPIRED` · `429` · `503` |
| D-ORD-05 (ficha nueva) | `GET /api/v1/sellers/me/orders` | Bearer + rol `Seller` + cuenta `Active` (perfil local) | `200` (paginado `{items,page,pageSize,totalCount}` con `role: "seller"`) · `400` · `401` · `403` `SELLER_ROLE_REQUIRED` / `SELLER_ACCOUNT_NOT_ACTIVE` · `429` · `503` |

Detalle de cuerpos, validaciones y ejemplos JSON en `spec.md` §5 (§5.10 para la ficha nueva). Contrato de error único `{correlationId,status,code,message}` (§14; sin `details` — ver D-ORD-10).

**No participante → `404` (no `403`) para no revelar existencia; participante con rol equivocado → `403 ORDER_FORBIDDEN`.**

**Rutas fuera del inventario:** la única aprobada hasta ahora es `GET /api/v1/sellers/me/orders` (D-ORD-05, opción B, aprobada el 2026-11-18; precedente DEC-019.d). `/orders/mine` permanece solo del comprador. Cualquier otra ruta nueva requiere ficha aprobada.

## 4. Redis y rate limiting

| Política | Ventana | Límite | Partición |
|---|---|---|---|
| `order-create` | 60 s | 10 | `userId` SHA-256 |
| `order-read` | 60 s | 60 | `userId` |
| `order-write` | 60 s | 30 | `userId` |
| `order-cancel` | 60 s | 10 | `userId` |
| `order-delivery-token` | 300 s | 5 | `userId` |
| `order-delivery-confirm` | 60 s | 20 | `userId` |

- Clave `ratelimit:{policy}:{partición}` (§6.2); implementación con el `RedisRateLimiter` existente (`Shared/Infrastructure/RedisOptions.cs`) y `RateLimitingOptions` ampliado con las seis políticas.
- **Fail-closed:** Redis caído → la operación no se ejecuta y responde `503 ORDER_UNAVAILABLE` (o `429` según la capa), sin crear, transicionar ni generar token.
- **Sin caché de pedidos:** no se declara `cache:orders:*` (datos personales y estado mutable §5.3). `revoked:{jti}` sigue siendo responsabilidad de Identity.
- Las claves de prueba se limpian entre tests (§5.3).

## 5. Seguridad y observabilidad

- **Autorización por recurso:** participante = `buyerUserId` o `sellerId` del pedido; acciones restringidas por rol (cancelar solo comprador; transiciones y token solo vendedor). Verificar propiedad, no solo rol (§6).
- **Token de entrega:** hash SHA-256 almacenado, TTL corto, single-use, nuevo token invalida al anterior; nunca en logs ni en respuestas de error (§8).
- **Presupuesto de efectivo:** `paymentMethod` fijo `Cash`; no hay campos financieros en request ni response más allá de `total` MXN (BR-003).
- **Logs:** sin tokens, sin `jti`, sin montos con datos personales; solo `correlationId`, `orderId`, `action` y resultado.
- **`correlationId`** en toda respuesta de error y en `orderAuditLogs.correlationId`.
- **Cero recursos nuevos:** no toca Terraform, S3, Content Safety, FCM ni el pipeline de middleware.

## 6. Verificación

| Nivel | Cobertura mínima |
|---|---|
| Unitarias | cálculo de total server-side; snapshot de precio inalterable; máquina de estados (todas las transiciones válidas e inválidas); CAS por `version`; reserva/liberación/commit idempotentes por `reservationId`; generación/consumo de token (hash, TTL, single-use); validadores (cantidad, formato `ObjectId`, campos prohibidos); `/mine` solo comprador. |
| Integración | API-023..029 y `GET /sellers/me/orders` con `WebApplicationFactory` y dobles: `201`, `200`, `400`, `401`, `403`, `404`, `409`, `422`, `429`, `503`; forma exacta del JSON de éxito y del contrato de error; paginación `{items,page,pageSize,totalCount}`; el listado del vendedor solo devuelve pedidos propios con `role: "seller"` (T-ORD-21..23). |
| Seguridad | token ausente/inválido/expirado/revocado → `401`; no participante → `404`; rol equivocado → `403`; campos privilegios del cliente ignorados/rechazados; token de entrega no filtrado ni logueado; rate limit agotado y Redis caído → `429`/`503` sin operación; sin caché de pedidos. |
| Concurrencia | dos `PATCH /status` con la misma `version` → un `200` y un `409`; dos `POST /orders` con el mismo `Idempotency-Key` → un pedido y una reserva; rechazo de token reutilizado. |
| Operativa | `dotnet build -c Release --no-restore` 0 errores · `dotnet format --verify-no-changes` · las tres suites en verde · `docker compose config` · smoke local (`/health` + rutas de pedido) · CI-1 verde en la PR; CD-1 y evidencia en EC2 tras el merge. |

## 7. Dependencias y gates

1. **Gate documental:** `spec.md` v0.3, este `plan.md` v0.3 y `task.md` v0.3 alineados → **cumplido** (Fase 1 y Fase 2 aprobadas por el propietario el 2026-11-16).
2. **Gate técnico:** fase-03 Sprint 2 aprobada/implementada (contratos de catálogo y stock) → **cumplido**; la Fase 2 amplía `ICatalogOrderProducts` con `CommitStockAsync` (commit idempotente por `reservationId`).
3. **Fuera de este plan:** implementación en `src/` antes de la aprobación; rutas nuevas fuera de API-023..029 **y de la ficha D-ORD-05 ya aprobada**; `Accepted → CancelledByBuyer` sin D-ORD-07; handler de borrado de cuenta (D-ORD-09, bloqueado por DEC-003); publicación de notificaciones o hechos de Reviews antes de sus fases (D-ORD-15).
4. **Evidencia:** la disponibilidad productiva solo se afirma tras merge + CD-1; hasta entonces el estado es «Fase 2 implementada en rama, pendiente de PR».

## 8. Historial

| Versión | Fecha | Cambio |
|---|---|---|
| 0.1 | 2026-11-11 | Borrador inicial: fronteras del módulo Orders, modelo `orders`/`orderAuditLogs`, contratos API-023..029, rate limits `order-*`, verificación y gates. |
| 0.2 | 2026-11-16 | Fase 1 (API-023/024/025) implementada: reserva/liberación de stock vía Catalog, `Idempotency-Key`, auditoría `Created`. |
| 0.3 | 2026-11-16 | Fase 2 (API-026/027/028/029) implementada: CAS por `version` en cada transición, token de entrega con hash SHA-256 y TTL configurable, commit de stock en `Delivered`, rate limits `order-write`/`order-cancel`/`order-delivery-token`/`order-delivery-confirm`. |
| 0.4 | 2026-11-18 | Ficha nueva aprobada `GET /api/v1/sellers/me/orders` (D-ORD-05, opción B): fila de contrato en §3, verificación T-ORD-21..23 y alcance ampliado de §1/§7; `/orders/mine` permanece solo del comprador. |
