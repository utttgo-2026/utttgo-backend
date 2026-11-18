# spec.md — Pedidos y efectivo — Backend UTTT-GO

> **Fase:** 04 — Pedidos y efectivo
> **Versión:** 0.5
> **Estado:** **Fase 1 (API-023, API-024, API-025) y Fase 2 (API-026, API-027, API-028, API-029) aprobadas por el propietario e implementadas el 2026-11-16** (ramas `feature/orders-phase1-p1` y `feature/orders-phase2-status-qr`); **ficha nueva `GET /api/v1/sellers/me/orders` aprobada el 2026-11-18 (D-ORD-05, opción B)**
> **Fecha:** 2026-11-18
> **Derivación:** `docs/constitution.md` §3 (BR-001, BR-002, BR-003, BR-009, BR-010), §4.5, §6.2, §11, §12, §13, §14; `docs/traceability.md` API-023..API-029.
> **Fichas:** API-023, API-024, API-025, API-026, API-027, API-028, API-029; ficha nueva `GET /api/v1/sellers/me/orders` (D-ORD-05, precedente DEC-019.d; no altera el Excel de 42 fichas).
> **Gate previo:** fase-03 Sprint 2 (catálogo y contratos de stock) aprobada y verificada.

## 0. Alcance de la Fase 2 (aprobado el 2026-11-16)

El propietario aprobó por instrucción directa implementar la **Fase 2** (API-026..029) sobre la Fase 1 ya fusionada y desplegada. Quedan fuera de esta fase la cancelación desde `Accepted` (D-ORD-07), el filtro `role` del listado (D-ORD-05), el handler de borrado de cuenta (D-ORD-09) y las fases de Reviews, Notifications y Chat.

**Aprobación de la ficha nueva (2026-11-18, v0.5):** el propietario resolvió **D-ORD-05** eligiendo la **opción B**: una ruta propia `GET /api/v1/sellers/me/orders` («Pedidos Recibidos» del vendedor) en lugar de extender `/orders/mine` con el filtro `role`. Es una **ficha nueva aprobada** al estilo de `GET /api/v1/sellers/me` (DEC-019.d): no figura en el Excel de 42 fichas, que no se modifica, y se registra en `docs/traceability.md` con su propio identificador. En el mismo turno se confirmó que **API-025 no cambia**: el detalle sigue respondiendo `404 ORDER_NOT_FOUND` a los terceros (RF-ORD-15), pese a la solicitud móvil de `403`. El equipo móvil etiquetó esta ruta como «API-040», pero API-040 es `PATCH /api/v1/admin/users/{userId}/status`; el identificador correcto es **D-ORD-05**.

Desviaciones respecto de v0.2, fijadas por la implementación y la instrucción del propietario:

| ID | v0.2 | Fase 2 | Motivo |
|---|---|---|---|
| D-ORD-15 (nueva) | La cancelación y la entrega publicaban notificación vía `INotificationPublisher` y el hecho «pedido entregado» a Reviews | Esas dos publicaciones **se difieren** a sus fases (Notifications 08 y Reviews 06); la Fase 2 solo deja la traza en `orderAuditLogs` | Los módulos destino no existen todavía; no se crea una interfaz stub ni un publicador vacío (prohibido el código muerto, AGENTS §4). La traza de auditoría cubre la trazabilidad exigida en esta fase. |
| D-ORD-16 (nueva) | «token/QR seguro para entrega» sin fijar el formato | La API entrega el **token** (`{orderId, token, expiresAt}`) y el móvil genera el QR a partir de él; el backend nunca dibuja ni almacena imágenes | El QR es una representación del token en el cliente; el servidor solo garantiza entropía, TTL y un solo uso. |
| D-ORD-17 (nueva) | `Rejected` solo aparecía en la tabla de transiciones | `Rejected` se alcanza por API-027 (`Pending → Rejected`), libera la reserva de stock y fija `completedAt` | Es la única salida terminal disponible para el vendedor mientras `Accepted` no puede cancelarse (D-ORD-07). |
| D-ORD-18 (nueva) | La cancelación liberaba stock como efecto posterior | Si la liberación de stock falla, la cancelación **queda registrada** y la ruta responde `503 ORDER_UNAVAILABLE`; la repetición (idempotente) completa la liberación | El estado del pedido es la fuente de verdad y la liberación es idempotente por `reservationId`; un `503` invita a reintentar sin perder información. |
| D-ORD-19 (nueva) | El fallo del commit de stock en la entrega no estaba previsto | Si el commit de stock falla tras `Delivered`, la entrega **se confirma** y el fallo queda en el log sin datos sensibles | El pedido entregado es el hecho de negocio; la reserva en el producto es estado derivado e idempotente por `reservationId`. |


El propietario aprobó implementar **por partes** y autorizó la Fase 1: **API-023** (creación), **API-024** (listado propio) y **API-025** (detalle). API-026..029 (cancelación, transiciones, token de entrega y confirmación) quedan para la Fase 2.

Desviaciones respecto de v0.1, fijadas por instrucción del propietario:

| ID | v0.1 | Fase 1 | Motivo |
|---|---|---|---|
| D-ORD-11 (nueva) | Stock insuficiente → `409 INSUFFICIENT_STOCK` | **`400 INSUFFICIENT_STOCK`** | Instrucción explícita del propietario (2026-11-16) para el consumo desde el móvil; el código de error conserva el significado semántico. |
| D-ORD-12 (nueva) | `Published` como estado del producto | El producto publicable es el que está `Active` con categoría activa (contrato ya implementado por Catalog en el Sprint 2) | El vocabulario `Published` no existe en el catálogo vigente; no se introduce un estado nuevo. |
| D-ORD-13 (nueva) | Replay de `Idempotency-Key` sin código definido | El replay devuelve **`200`** con el pedido ya creado (mismo `orderId`), sin reservar stock otra vez | Reproducir `201` ocultaría que el recurso ya existía. |
| D-ORD-14 (nueva) | `CreateOrderRequest` sin nota del comprador | `POST /api/v1/orders` acepta **`notes` opcional** (string, máximo 250 caracteres tras recortar); se persiste en `orders.notes` y se devuelve en `OrderResponse` y `OrderSummaryResponse` (`null` si no se envió) | Solicitud explícita del propietario (2026-11-16) para el consumo desde el móvil: la nota viaja en el cuerpo de la orden y no en un canal aparte (BR-009: el cliente no decide precio, total ni estado). |
| D-ORD-20 (nueva) | Respuestas con `buyerUserId`, `total` (importe) y `total` (contador de página) | Contrato público con **`buyerId`**, **`totalAmount`** (importe) y **`totalCount`** (contador de página, `int`); la colección `orders` conserva `buyerUserId` y `total` | Alineación definitiva solicitada por el equipo móvil (2026-11-16): un mismo nombre `total` designaba el importe y el contador de página, y el nombre público del comprador no era `buyerId`. El nombre interno de la colección no cambia. |

Decisiones de v0.1 aplicadas en la Fase 1: **D-ORD-01** (snapshot de `unitPrice`/`total` al crear), **D-ORD-02** (reserva de stock por `reservationId` con liberación compensatoria, sin transacción cross-module), **D-ORD-04** (`Idempotency-Key` opcional con índice único parcial), **D-ORD-05** (sin filtro `role`: `/orders/mine` es solo del comprador), **D-ORD-06** (campo `version` presente; las transiciones CAS llegan en la Fase 2) y **D-ORD-08** (`completedAt` presente y `null` hasta un estado terminal).

Decisiones de v0.1 aplicadas en la Fase 2: **D-ORD-03** (`Delivered` solo por API-029 con token de entrega), **D-ORD-06** (CAS por `version` en cada transición, `409 ORDER_VERSION_CONFLICT`), **D-ORD-08** (`completedAt` en `Delivered`, `Rejected` y `CancelledByBuyer`) y **D-ORD-07** (se mantiene **prohibida** `Accepted → CancelledByBuyer`: responde `409 ORDER_STATE_CONFLICT` hasta que el propietario la apruebe).

Contratos cross-module publicados para esta fase: `ICatalogOrderProducts` (Catalog: leer el producto publicable, reservar, liberar y confirmar stock de forma atómica e idempotente) e `ISellerOrderProfiles` (Sellers: estado del vendedor y punto de recogida). Orders no lee las colecciones `products`, `categories` ni `sellers` (§4.4).

---

## 1. Decisiones ratificadas (de la constitución)

- **BR-001:** no existe carrito; la compra es directa e individual. `POST /api/v1/orders` recibe un producto y una cantidad, sin selección múltiple.
- **BR-002:** un pedido corresponde a un único producto de un único vendedor con su cantidad. No hay agrupación ni líneas de pedido.
- **BR-003:** el único método de pago es efectivo presencial en el campus UTTT. El contrato no acepta datos financieros; `paymentMethod` es fijo `Cash`.
- **BR-009:** el comprador no decide precio, total, estado ni identidad. El precio y el vendedor se derivan server-side del producto elegido; el vendedor propietario es quien puede cambiar precio/stock/publicación y transicionar estados.
- **BR-010:** total server-side = precio vigente del producto × cantidad, en MXN como `decimal`. La concurrencia/snapshot se define en este spec (D-ORD-01).
- **BR-011:** el borrado de cuenta elimina información propia incluyendo historial del vendedor. Orders participa como consumidor del evento de borrado (D-ORD-09, sección 9).
- **BR-012:** el chat se conserva mientras el pedido esté en curso y su historial se elimina dentro de tres días de completarse. Orders expone `completedAt` como ancla de purga (D-ORD-08).
- **§6.2 (Redis):** `ratelimit:{policy}:{partición}` con fail-closed; `cache:{recurso}:{clave}` con fail-open; `revoked:{jti}` es responsabilidad de Identity, no de Orders.
- **§11:** rutas kebab-case bajo `/api/v1`; JSON camelCase; `ObjectId` como string; fechas UTC ISO 8601; dinero `decimal` MXN sin `float`/`double`.
- **§13:** la máquina de estados de un pedido es `Pending → Accepted → InPreparation → Ready → Delivered`, con terminales `Rejected` y `CancelledByBuyer`. Toda transición inválida responde `409 Conflict`.
- **§14:** contrato de error único `{correlationId, status, code, message}` con `correlationId` presente en toda respuesta de error.

## 2. Alcance

### 2.1 Incluido (fase 04)

- Fichas API-023..API-029 de `docs/traceability.md`.
- Módulo `Orders` bajo `src/UTTTGO.Api/Modules/Orders/` (estructura objetivo §4.5; no se implementa en esta fase, solo se especifica).
- Colecciones `orders` y `orderAuditLogs` (propias de Orders, propiedad registrada en `memoria.md`).
- Reserva/confirmación de stock a través de contratos publicados por Catalog (D-ORD-02), nunca accediendo a sus colecciones.
- Contratos de salida: `IOrderReader` (participantes, estado, `completedAt`, métricas agregadas para Chat y Admin), hechos de notificación vía `INotificationPublisher` (Notifications), hecho «pedido entregado» vía contrato publicado por Reviews.
- Políticas de rate limit distribuido `order-*` en Redis, fail-closed.
- Pruebas: unitarias, integración, seguridad, concurrencia e idempotencia.

### 2.2 Diferido (fuera de esta fase)

- Implementación de código (`src/`) — se hará cuando la rama de specs esté aprobada.
- Notificaciones push FCM (fase-06) y chat en tiempo real (fase-07): Orders solo publica los hechos.
- Moderación de reseñas y lectura de resúmenes (fase-08) — solo el hecho de entrega.
- Reportes semanales de Admin (fase-10) — solo la lectura de métricas vía `IOrderReader`.
- Borrado de cuenta (BR-011/DEC-003): el handler de evento es D-ORD-09, bloqueado.

### 2.3 Fuera de alcance

- Carrito de compras, líneas de pedido, compra múltiple (BR-001/BR-002).
- Pasarela de pago, tarjetas, wallets, datos financieros (BR-003).
- Menús, horarios, disponibilidad por horario (BR-004) — la disponibilidad es stock/estado del producto.
- Cálculo o cambio de precio por parte del cliente.
- Envío de push por parte del cliente; solo backend (BR-008).
- Acceso directo de Orders a colecciones de Catalog, Sellers, Identity, Reviews, Notifications, Chat o Admin (§4.4).

## 3. Historias y reglas trazables

| ID | Historia / regla | Regla de negocio |
|---|---|---|
| RF-ORD-01 | Como comprador, quiero comprar un producto directamente para que el pedido quede registrado a mi nombre. | BR-001, BR-002 |
| RF-ORD-02 | Como comprador, quiero que el precio y el total los calcule el servidor para no poder alterarlos. | BR-009, BR-010 |
| RF-ORD-03 | Como vendedor, quiero reservar el stock al crearse un pedido para no vender más de lo disponible. | BR-010 |
| RF-ORD-04 | Como vendedor, quiero transicionar mi pedido por las etapas permitidas para informar el avance. | BR-009, §13 |
| RF-ORD-05 | Como comprador, quiero un token de entrega generado por el backend para reclamar mi pedido en el punto de recogida. | BR-003, BR-007 |
| RF-ORD-06 | Como vendedor, quiero confirmar la entrega y el cobro en efectivo con ese token para cerrar el pedido. | BR-003 |
| RF-ORD-07 | Como comprador, quiero cancelar un pedido pendiente y liberar el stock para no bloquear existencias. | BR-010 |
| RF-ORD-08 | Como comprador, quiero ver mis pedidos paginados para consultar mi historial. | §12 |
| RF-ORD-09 | Como comprador/vendedor, quiero consultar el detalle de un pedido del que soy participante. | §12 |
| RF-ORD-10 | Como sistema, quiero un registro auditable de cada cambio de estado para trazabilidad. | §4.5 |
| RF-ORD-11 | Como comprador/vendedor, quiero identificar errores claros y consistentes con el contrato único. | §14 |

## 4. Requisitos funcionales (EARS)

| ID | Forma | Requisito |
|---|---|---|
| RF-ORD-10 | **WHEN** un usuario autenticado con cuenta `Active` envía `POST /api/v1/orders` con `productId` de un producto `Published` de un vendedor activo y una cantidad válida, **THEN** el sistema creará el pedido en `Pending` con precio unitario y total derivados del producto vigente, y reservará el stock correspondiente. | BR-009, BR-010 |
| RF-ORD-11 | **WHILE** el precio o el stock del producto cambian después de creado el pedido, **THEN** el `unitPrice` y el `total` del pedido existente permanecen inalterados (snapshot en el momento de la compra). | BR-010 (D-ORD-01) |
| RF-ORD-12 | **WHEN** el producto no está `Published`, la categoría está inactiva o el vendedor no está `Active`, **THEN** el sistema rechazará la creación con `422` y no creará el pedido ni reservará stock. | BR-006 |
| RF-ORD-13 | **WHEN** el stock disponible del producto es menor que la cantidad solicitada, **THEN** el sistema rechazará con `400` e `INSUFFICIENT_STOCK`, sin crear el pedido ni reservar parcialmente. | BR-010, D-ORD-11 |
| RF-ORD-14 | **WHEN** el cliente envía campos `price`, `total`, `sellerId`, `status`, `paymentMethod` o `currency` en la creación, **THEN** el sistema los ignorará y usará los valores derivados del producto, o rechazará con `400` si son inválidos en forma. | BR-009 |
| RF-ORD-15 | **WHEN** un usuario que no es participante (ni comprador ni vendedor) intenta leer o actuar sobre un pedido, **THEN** el sistema responderá `404` con `ORDER_NOT_FOUND` sin revelar existencia. | §14, autorización por recurso |
| RF-ORD-16 | **WHEN** un participante con rol equivocado intenta una acción (ej. comprador envía `PATCH /status`), **THEN** el sistema responderá `403` con `ORDER_FORBIDDEN`. | §13 |
| RF-ORD-17 | **WHEN** se solicita una transición de estado que no está permitida por la máquina §13, **THEN** el sistema responderá `409` con `ORDER_STATE_CONFLICT` sin cambiar el estado. | §13 |
| RF-ORD-18 | **WHEN** dos escrituras concurrentes compiten por el mismo pedido, **THEN** solo una tendrá éxito; la otra recibirá `409` con `ORDER_VERSION_CONFLICT` (CAS por `version`). | §13, D-ORD-06 |
| RF-ORD-19 | **WHEN** el comprador envía `POST /orders/{orderId}/cancel` sobre un pedido en `Pending`, **THEN** el sistema transicionará a `CancelledByBuyer`, liberará la reserva de stock de forma idempotente y registrará la auditoría. | BR-010 |
| RF-ORD-20 | **WHEN** el comprador envía cancelación sobre un pedido que no está en `Pending` (o en `Accepted` sin aprobación de D-ORD-07), **THEN** el sistema responderá `409` sin cambiar el estado ni liberar stock. | §13 |
| RF-ORD-21 | **WHEN** el vendedor propietario envía `PATCH /status` con una transición válida (`Pending→Accepted`, `Pending→Rejected`, `Accepted→InPreparation`, `InPreparation→Ready`), **THEN** el sistema aplicará la transición con CAS y registrará auditoría. | §13 |
| RF-ORD-22 | **WHEN** se intenta transicionar a `Delivered` mediante `PATCH /status`, **THEN** el sistema responderá `409`; `Delivered` solo se alcanza por `POST /orders/{orderId}/delivery-confirmations`. | D-ORD-03 |
| RF-ORD-23 | **WHEN** el comprador solicita un token de entrega (`POST /orders/{orderId}/delivery-token`) sobre un pedido en `Ready`, **THEN** el sistema generará un token de un solo uso, con hash SHA-256 almacenado, TTL corto y rate limit estricto; si ya existe un token vigente, lo invalidará. | BR-003, BR-007, §6.2 |
| RF-ORD-24 | **WHEN** el token está ausente, no coincide, expiró o ya fue usado, **THEN** el sistema responderá `422` (`DELIVERY_TOKEN_INVALID` / `DELIVERY_TOKEN_EXPIRED`) o `409` (`DELIVERY_TOKEN_ALREADY_USED`) según el caso, sin entregar. | §14 |
| RF-ORD-25 | **WHEN** el vendedor confirma la entrega con un token válido sobre un pedido en `Ready`, **THEN** el sistema transicionará a `Delivered`, fijará `deliveredAt` y `completedAt`, marcará el token como usado, confirmará la reserva de stock, registrará auditoría y publicará el hecho de entrega a Reviews. | BR-003, BR-012, D-ORD-03 |
| RF-ORD-26 | **WHEN** el pedido llega a un estado terminal (`Delivered`, `Rejected`, `CancelledByBuyer`), **THEN** `completedAt` se fija y se expone vía `IOrderReader` como ancla de purga de chat (3 días, BR-012). | BR-012, D-ORD-08 |
| RF-ORD-27 | **WHEN** el servicio de Redis no responde ante una operación protegida por rate limit, **THEN** el sistema rechaza la operación (`429`/`503`) sin ejecutarla (fail-closed). | §6.2 |
| RF-ORD-28 | **WHEN** la creación incluye el header opcional `Idempotency-Key` repetido para el mismo comprador, **THEN** el sistema devolverá el mismo pedido sin crear duplicado ni reservar dos veces. | D-ORD-04 |
| RF-ORD-29 | **WHEN** ocurre un cambio de estado, **THEN** Orders publicará la notificación vía `INotificationPublisher` (Notifications) y nunca enviará push directamente. | BR-008 |
| RF-ORD-30 | **WHEN** se consulta `GET /orders/mine`, **THEN** el sistema devolverá paginación `page`+`pageSize` con máximo configurable, solo pedidos en los que el usuario autenticado es participante. | §12, D-ORD-05 |
| RF-ORD-31 | **WHILE** el pedido existe, **THEN** ninguna respuesta expone documentos completos de MongoDB: solo DTOs explícitos con `camelCase` y `ObjectId` como string. | §11, §4.5 |
| RF-ORD-32 | **WHEN** MongoDB o un contrato de otro módulo no responde, **THEN** la operación falla con `503`/`ORDER_UNAVAILABLE` sin dejar el pedido en un estado inconsistente (sin reserva huérfana). | §14 |
| RF-ORD-33 | **WHEN** un usuario con rol `Seller` y cuenta `Active` consulta `GET /api/v1/sellers/me/orders`, **THEN** el sistema devolverá una página `page`+`pageSize` con `totalCount` conteniendo **solo** los pedidos cuyo `sellerId` coincide con el del perfil autenticado, con `role: "seller"`; sin rol `Seller` → `403 SELLER_ROLE_REQUIRED` y cuenta no `Active` → `403 SELLER_ACCOUNT_NOT_ACTIVE`. | BR-009, D-ORD-05 |

## 5. Contratos HTTP

Prefijo `/api/v1`. Autenticación: JWT de Supabase validado (firma, issuer, audience, expiración y blacklist `revoked:{jti}` en Redis). JSON `camelCase`. `correlationId` en toda respuesta de error.

### 5.1 API-023 — `POST /api/v1/orders`

Crea un pedido directo (BR-001/BR-002).

- **Auth:** JWT, cuenta `Active`. Cualquier usuario activo puede comprar (también un vendedor comprando a otro).
- **Request body:**

```json
{
  "productId": "665f1c2e8b3f2a0012ab34cd",
  "quantity": 2,
  "notes": "Sin cebolla, por favor"
}
```

- **`notes` (opcional):** nota del comprador para el vendedor, máximo 250 caracteres tras recortar (D-ORD-14). Vacía o ausente se persiste como `null`. Más de 250 caracteres → `400 VALIDATION_ERROR`.

- **Header opcional:** `Idempotency-Key: <uuid>` (D-ORD-04).
- **Campos prohibidos (ignorados o `400`):** `price`, `unitPrice`, `total`, `totalAmount`, `sellerId`, `buyerId`, `buyerUserId`, `status`, `paymentMethod`, `currency`.
- **Derivación server-side:** `sellerId`, `productName`, `unitPrice` (vigente), `total = unitPrice × quantity` (decimal MXN), `paymentMethod = "Cash"`.
- **Reserva de stock:** vía `ICatalogProductCommands` (D-ORD-02). Si falla, no se crea el pedido.
- **Rate limit:** `order-create`, 10/60 s por comprador, fail-closed.
- **Respuesta `201`:**

```json
{
  "orderId": "665f1d408b3f2a0012ab34ce",
  "productId": "665f1c2e8b3f2a0012ab34cd",
  "sellerId": "665f1a108b3f2a0012ab34c1",
  "productName": "Quesadillas de huitlacoche (6 pza)",
  "quantity": 2,
  "unitPrice": 85.50,
  "totalAmount": 171.00,
  "currency": "MXN",
  "paymentMethod": "Cash",
  "status": "Pending",
  "createdAt": "2026-11-11T18:00:00Z",
  "notes": "Sin cebolla, por favor"
}
```

- **Errores:** `400 VALIDATION_ERROR` (forma/cantidad) y `400 INSUFFICIENT_STOCK` (D-ORD-11), `401 UNAUTHORIZED`, `403 ACCOUNT_NOT_ACTIVE`, `404 PRODUCT_NOT_FOUND`, `409 ORDER_VERSION_CONFLICT`, `422 PRODUCT_NOT_AVAILABLE` (no publicado / categoría inactiva / vendedor no activo), `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE`. Un replay del mismo `Idempotency-Key` devuelve `200` con el pedido ya creado (D-ORD-13).

### 5.2 API-024 — `GET /api/v1/orders/mine`

Lista pedidos del usuario autenticado, paginado.

- **Query:** `page` (≥1, default 1), `pageSize` (default 20, máx configurable), `status` (opcional, enum), `role` (`buyer`|`seller`, opcional — D-ORD-05).
- **Alcance:** solo pedidos donde el usuario es participante (comprador o vendedor según `role`).
- **Rate limit:** `order-read`, 60/60 s.
- **Respuesta `200`:**

```json
{
  "items": [ { "orderId": "...", "productId": "...", "productName": "...", "sellerId": "...", "buyerId": "...", "quantity": 1, "unitPrice": 85.50, "totalAmount": 85.50, "currency": "MXN", "status": "Pending", "role": "buyer", "createdAt": "...", "notes": null } ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 3
}
```

- **Errores:** `400 VALIDATION_ERROR`, `401 UNAUTHORIZED`, `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE`.

### 5.3 API-025 — `GET /api/v1/orders/{orderId}`

Detalle de un pedido.

- **Path:** `orderId` (`ObjectId` como string).
- **Auth:** solo participantes (comprador o vendedor del pedido); no participante → `404 ORDER_NOT_FOUND`.
- **Rate limit:** `order-read`.
- **Respuesta `200`:**

```json
{
  "orderId": "...",
  "productId": "...",
  "productName": "...",
  "sellerId": "...",
  "buyerId": "...",
  "quantity": 2,
  "unitPrice": 85.50,
  "totalAmount": 171.00,
  "currency": "MXN",
  "paymentMethod": "Cash",
  "status": "Ready",
  "pickupPoint": "Biblioteca central, entrada norte",
  "deliveredAt": null,
  "completedAt": null,
  "createdAt": "...",
  "updatedAt": "...",
  "version": 4
}
```

- **Errores:** `401 UNAUTHORIZED`, `404 ORDER_NOT_FOUND`, `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE`.

### 5.4 API-026 — `POST /api/v1/orders/{orderId}/cancel`

Cancelación del comprador.

- **Auth:** solo el comprador del pedido; el vendedor responde `403 ORDER_FORBIDDEN` y un tercero `404 ORDER_NOT_FOUND` (no se divulga la existencia).
- **Transición:** `Pending → CancelledByBuyer` (§13). `Accepted → CancelledByBuyer` **solo si D-ORD-07 está aprobado**; por defecto `409 ORDER_STATE_CONFLICT`.
- **Efectos:** liberar reserva de stock de forma idempotente (D-ORD-02), fijar `completedAt`, auditoría. La notificación vía `INotificationPublisher` queda diferida a la fase 08 (D-ORD-15).
- **Idempotencia:** cancelar un pedido ya cancelado responde `200` con el estado vigente (sin doble liberación: la reserva se libera una sola vez por `reservationId`).
- **Fallo de la liberación:** la cancelación queda registrada y la ruta responde `503 ORDER_UNAVAILABLE`; al repetir la llamada se completa la liberación (D-ORD-18).
- **Rate limit:** `order-cancel`, 10/60 s.
- **Respuesta `200`:** `orderId`, `status`, `completedAt`, `version`.
- **Errores:** `401`, `403 ACCOUNT_NOT_ACTIVE`, `403 ORDER_FORBIDDEN`, `404 ORDER_NOT_FOUND`, `409 ORDER_STATE_CONFLICT`, `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE`.

### 5.5 API-027 — `PATCH /api/v1/orders/{orderId}/status`

Transiciones del vendedor propietario.

- **Auth:** solo el vendedor del pedido (verificado por `sellerId` del perfil local contra el pedido); el comprador responde `403 ORDER_FORBIDDEN` y un tercero `404 ORDER_NOT_FOUND`.
- **Request body:**

```json
{ "status": "Accepted", "version": 3 }
```

- **Transiciones permitidas:**

| Desde | Hasta |
|---|---|
| `Pending` | `Accepted`, `Rejected` |
| `Accepted` | `InPreparation` |
| `InPreparation` | `Ready` |

- **Prohibido aquí:** `Delivered` (solo API-029, D-ORD-03), `CancelledByBuyer` (solo API-026). Ambos responden `409 ORDER_STATE_CONFLICT`.
- **Concurrencia:** CAS por `version` (D-ORD-06); conflicto → `409 ORDER_VERSION_CONFLICT`.
- **Efectos:** auditoría; `Rejected` libera la reserva y fija `completedAt` (D-ORD-17). La notificación vía `INotificationPublisher` queda diferida a la fase 08 (D-ORD-15).
- **Rate limit:** `order-write`, 30/60 s.
- **Respuesta `200`:** `orderId`, `status`, `version`, `updatedAt`.
- **Errores:** `400 VALIDATION_ERROR`, `401`, `403 ACCOUNT_NOT_ACTIVE`, `403 ORDER_FORBIDDEN`, `404 ORDER_NOT_FOUND`, `409 ORDER_STATE_CONFLICT`, `409 ORDER_VERSION_CONFLICT`, `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE`.

### 5.6 API-028 — `POST /api/v1/orders/{orderId}/delivery-token`

Generación de token de entrega (BR-003, BR-007).

- **Auth:** solo el comprador del pedido; el vendedor responde `403 ORDER_FORBIDDEN` y un tercero `404 ORDER_NOT_FOUND`.
- **Estado requerido:** `Ready`; otro estado → `409 ORDER_STATE_CONFLICT`.
- **Diseño:** token aleatorio de alta entropía (32 bytes, base64url); se almacena **solo el hash SHA-256** con `expiresAt` (TTL corto, 900 s por defecto, configurable con `Orders:DeliveryTokenTtlSeconds`). Un token nuevo invalida al anterior. Nunca se loguea el token en claro (§8). El QR lo genera el móvil a partir del token (D-ORD-16).
- **Rate limit:** `order-delivery-token`, 5/300 s.
- **Respuesta `201`:**

```json
{
  "orderId": "...",
  "token": "9f2c...",
  "expiresAt": "2026-11-11T18:15:00Z"
}
```

- **Errores:** `401`, `403 ACCOUNT_NOT_ACTIVE`, `403 ORDER_FORBIDDEN`, `404 ORDER_NOT_FOUND`, `409 ORDER_STATE_CONFLICT`, `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE`.

### 5.7 API-029 — `POST /api/v1/orders/{orderId}/delivery-confirmations`

Confirmación de entrega y cobro en efectivo por el vendedor (BR-003, §13).

- **Auth:** solo el vendedor del pedido; el comprador responde `403 ORDER_FORBIDDEN` y un tercero `404 ORDER_NOT_FOUND`.
- **Request body:**

```json
{ "token": "9f2c..." }
```

- **Efectos al validar:** `Ready → Delivered`; fijar `deliveredAt` y `completedAt`; marcar el token usado (single-use por CAS); confirmar la reserva de stock (commit, idempotente por `reservationId`); auditoría. La notificación vía `INotificationPublisher` y el hecho «pedido entregado» a Reviews quedan diferidos a sus fases (D-ORD-15). Si el commit de stock falla, la entrega se confirma y el fallo queda registrado en el log (D-ORD-19).
- **Token:** ausente/no coincide → `422 DELIVERY_TOKEN_INVALID`; expirado → `422 DELIVERY_TOKEN_EXPIRED`; ya usado → `409 DELIVERY_TOKEN_ALREADY_USED`.
- **Idempotencia:** reintentar con el mismo token tras éxito → `409 DELIVERY_TOKEN_ALREADY_USED` (sin doble commit).
- **Rate limit:** `order-delivery-confirm`, 20/60 s.
- **Respuesta `200`:** `orderId`, `status`, `deliveredAt`, `version`.
- **Errores:** `400 VALIDATION_ERROR`, `401`, `403 ACCOUNT_NOT_ACTIVE`, `403 ORDER_FORBIDDEN`, `404 ORDER_NOT_FOUND`, `409 ORDER_STATE_CONFLICT`, `409 DELIVERY_TOKEN_ALREADY_USED`, `422 DELIVERY_TOKEN_INVALID`, `422 DELIVERY_TOKEN_EXPIRED`, `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE`.

### 5.8 Resumen de rate limits (Redis, fail-closed)

| Política | Ventana | Límite | Partición |
|---|---|---|---|
| `order-create` | 60 s | 10 | `userId` (SHA-256) |
| `order-read` | 60 s | 60 | `userId` |
| `order-write` | 60 s | 30 | `userId` |
| `order-cancel` | 60 s | 10 | `userId` |
| `order-delivery-token` | 300 s | 5 | `userId` |
| `order-delivery-confirm` | 60 s | 20 | `userId` |

- Clave: `ratelimit:{policy}:{partición}` (§6.2).
- Redis caído → fail-closed (`503 ORDER_UNAVAILABLE` o `429 RATE_LIMITED`), sin ejecutar la operación.
- **Sin caché de pedidos:** no se declara `cache:orders:*`; los pedidos son datos personales/estado mutable (§5.3 prohibición) y no se cachean.

### 5.9 Idempotencia y concurrencia (resumen)

- **Creación:** `Idempotency-Key` opcional (D-ORD-04); índice único parcial `(buyerUserId, idempotencyKey)`. Sin header, cada POST crea un pedido nuevo.
- **Escrituras de estado:** CAS por `version` (int64) → `409 ORDER_VERSION_CONFLICT`.
- **Cancelación y rechazo:** liberación de stock idempotente por `reservationId` (D-ORD-02); repetir no libera dos veces.
- **Token de entrega:** single-use marcado en el mismo documento del token; carrera resuelta por update condicional.

### 5.10 Ficha nueva — `GET /api/v1/sellers/me/orders` (D-ORD-05, opción B)

Bandeja «Pedidos Recibidos» del vendedor. **Ficha nueva aprobada** por el propietario el 2026-11-18 (precedente DEC-019.d); no altera el Excel de 42 fichas.

- **Query:** `page` (≥1, default 1), `pageSize` (default 20, máx 50), `status` (opcional, enum §13).
- **Auth:** JWT validado + cuenta `Active` + rol `Seller`, leídos del **perfil local** (nunca del JWT, §6). El `sellerId` con el que se filtra sale del perfil autenticado; el cliente no lo envía ni puede elegirlo (BR-009).
- **Alcance:** solo pedidos cuyo `sellerId` coincide con el del perfil, ordenados por `createdAt` descendente; consulta apoyada en el índice existente `sellerId_createdAt_desc`.
- **Módulo:** el endpoint vive en **Orders** (dueño de la colección `orders`) bajo el prefijo de ruta de Sellers; así la ruta pública queda en la familia `sellers/me` y no se crea la dependencia inversa Sellers→Orders (evita el ciclo §4.4).
- **Rate limit:** `order-read`, 60/60 s, partición por usuario, fail-closed (Redis caído → `503 ORDER_UNAVAILABLE` sin ejecutar la consulta).
- **Respuesta `200`:** mismo `OrderListResponse` que §5.2 con `role: "seller"`:

```json
{
  "items": [ { "orderId": "...", "productId": "...", "productName": "...", "sellerId": "...", "buyerId": "...", "quantity": 1, "unitPrice": 85.50, "totalAmount": 85.50, "currency": "MXN", "status": "Pending", "role": "seller", "createdAt": "...", "notes": null } ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 3
}
```

- **Errores:** `400 VALIDATION_ERROR`, `401 UNAUTHORIZED`, `403 SELLER_ROLE_REQUIRED` (sin rol `Seller`), `403 SELLER_ACCOUNT_NOT_ACTIVE` (cuenta no `Active`), `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE`. Códigos ya existentes en el inventario de `docs/guia-integracion-movil.md`; no se agrega ninguno nuevo.
- **Detalle API-025 (sin cambios):** comprador o vendedor → `200`; tercero → `404 ORDER_NOT_FOUND` (RF-ORD-15). El equipo móvil pidió `403` para terceros y el propietario resolvió **conservar `404`** para no divulgar existencia de pedidos.

## 6. Modelo de datos

Propiedad de colecciones (matriz en `memoria.md`): `orders`, `orderAuditLogs` pertenecen a **Orders**. Orders no lee ni escribe colecciones ajenas.

### 6.1 `orders`

| Campo | Tipo | Notas |
|---|---|---|
| `_id` | `ObjectId` | serializado como string en JSON |
| `buyerUserId` | `string` | participante comprador |
| `sellerId` | `string` | participante vendedor (derivado del producto) |
| `productId` | `string` | producto comprado |
| `productName` | `string` | snapshot al crear |
| `quantity` | `int` | ≥1 |
| `unitPrice` | `decimal` (Decimal128) | snapshot MXN (D-ORD-01) |
| `total` | `decimal` (Decimal128) | `unitPrice × quantity`, server-side |
| `currency` | `string` | `"MXN"` fijo |
| `paymentMethod` | `string` | `"Cash"` fijo (BR-003) |
| `status` | `string` | enum §13 |
| `reservationId` | `string` | correlación con reserva en Catalog (D-ORD-02) |
| `pickupPoint` | `string` | snapshot del punto de recogida del vendedor |
| `deliveryTokenHash` | `string?` | SHA-256; nunca el token en claro |
| `deliveryTokenExpiresAt` | `DateTime?` UTC | |
| `deliveryTokenUsedAt` | `DateTime?` UTC | |
| `deliveredAt` | `DateTime?` UTC | en `Delivered` |
| `completedAt` | `DateTime?` UTC | en estados terminales (D-ORD-08) |
| `version` | `long` | CAS (D-ORD-06) |
| `notes` | `string?` | nota del comprador para el vendedor, ≤250 caracteres (D-ORD-14) |
| `createdAt` / `updatedAt` | `DateTime` UTC | |

**Índices:**

1. `{ buyerUserId: 1, createdAt: -1 }` — API-024.
2. `{ sellerId: 1, createdAt: -1 }` — API-024 con `role=seller`.
3. `{ status: 1, updatedAt: -1 }` — consultas operativas.
4. **Único parcial:** `{ buyerUserId: 1, idempotencyKey: 1 }` where `idempotencyKey` existe — D-ORD-04.

### 6.2 `orderAuditLogs`

| Campo | Tipo | Notas |
|---|---|---|
| `_id` | `ObjectId` | |
| `orderId` | `string` | |
| `actorUserId` | `string` | |
| `actorRole` | `string` | `buyer`/`seller` |
| `action` | `string` | `Created`, `StatusChanged`, `Cancelled`, `DeliveryTokenIssued`, `DeliveryConfirmed` |
| `fromStatus` / `toStatus` | `string?` | |
| `correlationId` | `string` | trazabilidad §14 |
| `occurredAt` | `DateTime` UTC | |

**Índices:** `{ orderId: 1, occurredAt: -1 }`, `{ actorUserId: 1, occurredAt: -1 }`.

### 6.3 Estados y transiciones (§13)

```
Pending ──► Accepted ──► InPreparation ──► Ready ──► Delivered (terminal)
   │            │                                ▲
   │            └── (Accepted→CancelledByBuyer: requiere D-ORD-07)
   ├──► Rejected (terminal)
   └──► CancelledByBuyer (terminal)
```

| Transición | Quién | Vía |
|---|---|---|
| `Pending → Accepted` | vendedor | API-027 |
| `Pending → Rejected` | vendedor | API-027 |
| `Pending → CancelledByBuyer` | comprador | API-026 |
| `Accepted → InPreparation` | vendedor | API-027 |
| `Accepted → CancelledByBuyer` | comprador | API-026, **solo si D-ORD-07 aprobado** |
| `InPreparation → Ready` | vendedor | API-027 |
| `Ready → Delivered` | vendedor + token | API-029 |
| Cualquier otra | — | `409 ORDER_STATE_CONFLICT` |

`completedAt` se fija en `Delivered`, `Rejected` y `CancelledByBuyer`.

### 6.4 Contratos de módulos

**Orders consume (interfaces publicadas por el módulo dueño):**

| Contrato | Módulo dueño | Uso |
|---|---|---|
| `ICatalogProductReader` | Catalog | validar producto/publicación/vendedor y precio vigente |
| `ICatalogProductCommands` | Catalog | reservar / liberar / confirmar stock (D-ORD-02) |
| `ISellerProfileReader` | Sellers | estado del vendedor y punto de recogida |
| `ILocalProfileReader` | Identity | cuenta `Active`, identidad del actor |
| contrato de hecho de Reviews | Reviews | publicar «pedido entregado» (R-1: Reviews no consulta Orders) |
| `INotificationPublisher` | Notifications | encolar notificación de cambio de estado (BR-008) |

**Orders publica:**

| Contrato | Consumidores | Uso |
|---|---|---|
| `IOrderReader` | Chat, Admin | participantes, estado, `completedAt`, métricas agregadas |

No existe `IOrderEventPublisher` inverso desde Orders a Notifications ni dependencia de Chat/Admin desde Orders (evita ciclos §4.4).

### 6.5 Variables de configuración (tabla §2, no valores)

| Clave | Uso | Defecto propuesto |
|---|---|---|
| `Orders:MaxQuantityPerOrder` | límite de cantidad por pedido | 50 |
| `Orders:DeliveryTokenTtlSeconds` | TTL del token de entrega | 900 |
| `Orders:PageSizeMax` | máximo de `pageSize` | 50 |
| `RateLimiting:order-*` | ventanas/límites §5.8 | ver §5.8 |

## 7. Casos de aceptación

| ID | Given / When / Then |
|---|---|
| T-ORD-01 | **Given** un producto `Published` con precio 85.50 y stock 5, **when** el comprador crea un pedido de cantidad 2, **then** el pedido queda `Pending` con `unitPrice` 85.50, `total` 171.00, `paymentMethod` `Cash` y la reserva de stock consume 2 unidades. |
| T-ORD-02 | **Given** un producto con precio 85.50, **when** el vendedor cambia el precio a 99.00 después de creado el pedido, **then** `unitPrice` y `total` del pedido permanecen 85.50 y 171.00. |
| T-ORD-03 | **Given** un producto con stock 1, **when** se crea un pedido de cantidad 2, **then** responde `409 INSUFFICIENT_STOCK` y no existe pedido ni reserva. |
| T-ORD-04 | **Given** un producto `Paused`, **when** se crea un pedido, **then** responde `422 PRODUCT_NOT_AVAILABLE` y no hay reserva. |
| T-ORD-05 | **Given** un pedido `Pending`, **when** el comprador lo cancela, **then** queda `CancelledByBuyer`, `completedAt` está fijado, el stock se libera una sola vez y hay registro en `orderAuditLogs`. |
| T-ORD-06 | **Given** un pedido ya `CancelledByBuyer`, **when** se repite la cancelación, **then** responde `200` con el estado vigente y no se libera stock dos veces. |
| T-ORD-07 | **Given** un pedido `Pending`, **when** el comprador intenta `PATCH /status`, **then** responde `403 ORDER_FORBIDDEN`. |
| T-ORD-08 | **Given** un usuario no participante, **when** consulta `GET /orders/{orderId}`, **then** responde `404 ORDER_NOT_FOUND`. |
| T-ORD-09 | **Given** un pedido `Pending`, **when** el vendedor envía `status=InPreparation`, **then** responde `409 ORDER_STATE_CONFLICT`. |
| T-ORD-10 | **Given** un pedido `Ready`, **when** el vendedor intenta `PATCH /status` con `Delivered`, **then** responde `409 ORDER_STATE_CONFLICT` y el estado no cambia. |
| T-ORD-11 | **Given** dos `PATCH /status` concurrentes con la misma `version`, **when** se ejecutan, **then** uno aplica (`200`) y el otro recibe `409 ORDER_VERSION_CONFLICT`. |
| T-ORD-12 | **Given** un pedido `Ready`, **when** el comprador solicita token, **then** recibe `201` con `token` y `expiresAt`, y en `orders` solo hay `deliveryTokenHash` (no el token en claro). |
| T-ORD-13 | **Given** un token vigente, **when** el comprador pide otro token, **then** el anterior queda invalidado y solo el nuevo es aceptado. |
| T-ORD-14 | **Given** un pedido `Ready` con token, **when** el vendedor confirma con el token correcto, **then** queda `Delivered`, `deliveredAt` y `completedAt` fijados, el token se marca usado y el stock se confirma (commit). |
| T-ORD-15 | **Given** un token ya usado, **when** se reintenta la confirmación, **then** responde `409 DELIVERY_TOKEN_ALREADY_USED` sin doble commit. |
| T-ORD-16 | **Given** un token expirado, **when** se envía en la confirmación, **then** responde `422 DELIVERY_TOKEN_EXPIRED` y el pedido sigue `Ready`. |
| T-ORD-17 | **Given** Redis caído, **when** se invoca `POST /orders`, **then** la operación se rechaza fail-closed (`503 ORDER_UNAVAILABLE`) sin crear el pedido. |
| T-ORD-18 | **Given** el mismo `Idempotency-Key`, **when** se envían dos `POST /orders`, **then** solo hay un pedido y una reserva. |
| T-ORD-19 | **Given** `GET /orders/mine?page=2&pageSize=20`, **when** el comprador tiene 25 pedidos, **then** la página 2 tiene 5 items y `total` 25; no se exponen pedidos ajenos. |
| T-ORD-20 | **Given** un pedido `InPreparation`, **when** el vendedor lo mueve a `Ready`, **then** la respuesta incluye `version` incrementada y hay notificación encolada vía `INotificationPublisher` (mock en pruebas). |
| T-ORD-21 | **Given** un vendedor con dos pedidos propios y un pedido de otro vendedor, **when** consulta `GET /api/v1/sellers/me/orders`, **then** recibe solo sus dos pedidos con `role: "seller"` y `totalCount: 2`, sin exponer pedidos ajenos. |
| T-ORD-22 | **Given** un usuario autenticado sin rol `Seller` (o con cuenta no `Active`), **when** consulta `GET /api/v1/sellers/me/orders`, **then** recibe `403 SELLER_ROLE_REQUIRED` (o `403 SELLER_ACCOUNT_NOT_ACTIVE`) y no se ejecuta la consulta. |
| T-ORD-23 | **Given** sin token, **when** se consulta `GET /api/v1/sellers/me/orders`, **then** responde `401 UNAUTHORIZED`. |

## 8. Criterios de cierre y aprobación

- [ ] `spec.md`, `plan.md` y `task.md` de `specs/phase-04-orders-cash/` redactados y consistentes entre sí.
- [ ] Fichas API-023..API-029 cubiertas con ruta, método, auth, contrato, rate limit y errores.
- [ ] Decisiones pendientes (sección 9) marcadas como pendientes, ninguna auto-aprobada.
- [ ] Trazabilidad en `docs/traceability.md` actualizada a «spec v0.1 redactado, pendiente de aprobación».
- [ ] Aprobación explícita del propietario antes de cualquier implementación en `src/`.

### Historial

| Versión | Fecha | Cambio |
|---|---|---|
| 0.1 | 2026-11-11 | Borrador inicial: API-023..029, máquina §13, reserva vía Catalog, token de entrega, CAS por versión, rate limits `order-*`. |
| 0.2 | 2026-11-16 | Fase 1 aprobada e implementada (API-023/024/025): D-ORD-11..14 y `notes` del comprador. |
| 0.3 | 2026-11-16 | Fase 2 aprobada e implementada (API-026/027/028/029): D-ORD-15..19, CAS por `version` en cada transición, token de entrega con hash SHA-256 y TTL configurable, commit de stock en `Delivered` y el tratamiento D-ORD-18/19 de los fallos de stock. |
| 0.4 | 2026-11-16 | Alineación del contrato público con el equipo móvil (D-ORD-20): `buyerId`, `totalAmount` y `totalCount` en las respuestas de pedidos y listados paginados; los nombres internos de la colección no cambian. |
| 0.5 | 2026-11-18 | D-ORD-05 resuelta con la **opción B** aprobada por el propietario: ficha nueva `GET /api/v1/sellers/me/orders` (§5.10, RF-ORD-33, T-ORD-21..23) con `403 SELLER_ROLE_REQUIRED`/`SELLER_ACCOUNT_NOT_ACTIVE` y `role: "seller"`; API-025 conserva `404` para terceros (la solicitud móvil de `403` queda rechazada y registrada). |

## 9. Rutas necesarias fuera del inventario y decisiones pendientes

### 9.1 Rutas fuera del inventario de las 42 fichas

- **Listado de pedidos del vendedor:** API-024 es `GET /orders/mine` orientado al comprador (`docs/traceability.md`: «paginación y comprador autenticado»). **Resuelto el 2026-11-18 (D-ORD-05, opción B, aprobada por el propietario):** se implementa la ruta separada **`GET /api/v1/sellers/me/orders`** como ficha nueva (precedente: `GET /api/v1/sellers/me`, DEC-019.d), en lugar de extender `/orders/mine` con el filtro `role`. `/orders/mine` sigue siendo solo del comprador y su contrato no cambia. Contrato completo en §5.10.

### 9.2 Decisiones del propietario

| ID | Decisión | Resolución | Estado |
|---|---|---|---|
| D-ORD-01 | Snapshot de precio (`unitPrice`/`total`) al crear el pedido vs. precio al entregar. | Snapshot al crear. | **Aplicada (Fase 1)** |
| D-ORD-02 | Mecanismo de reserva de stock: contrato `ICatalogProductCommands` con `reservationId` y saga de compensación sin transacciones cross-module (Atlas Free Tier). | Reserva + liberación idempotente por `reservationId`; commit en `Delivered`. | **Aplicada (Fases 1 y 2)** |
| D-ORD-03 | `Delivered` solo por API-029 con token (no por API-027). | Sí: obliga a evidenciar cobro presencial (BR-003). | **Aplicada (Fase 2)** |
| D-ORD-04 | Header opcional `Idempotency-Key` en API-023. | Sí, opcional, con índice único parcial. | **Aplicada (Fase 1)** |
| D-ORD-05 | Alcance de `/orders/mine` (solo comprador vs. participante con `role`). | **Opción B aprobada (2026-11-18):** ficha nueva `GET /api/v1/sellers/me/orders` (§5.10); `/orders/mine` permanece solo del comprador. | **Aplicada (ficha nueva D-ORD-05)** |
| D-ORD-06 | Concurrencia por CAS `version` → `409 ORDER_VERSION_CONFLICT`. | Sí. | **Aplicada (Fase 2)** |
| D-ORD-07 | `Accepted → CancelledByBuyer` (constitución §13: «solo si el equipo lo aprueba»). | **No habilitar en el MVP** (fail-closed a `409 ORDER_STATE_CONFLICT`). | **Aplicada (Fase 2): permanece prohibida** |
| D-ORD-08 | `completedAt` en cualquier terminal como ancla de purga de chat (BR-012), coordinado con fase-07. | Fijar en `Delivered`, `Rejected`, `CancelledByBuyer`; fase-07 define la purga. | **Aplicada (Fase 2)** |
| D-ORD-09 | Borrado de cuenta (BR-011/DEC-003): handler de evento desde Identity para `orders`/`orderAuditLogs` compartidos. | Interfaz prevista, **bloqueada** hasta que DEC-003 defina el diseño. | **Bloqueada** |
| D-ORD-10 | Contrato de error: `ApiError` real tiene 4 campos sin `details` (§14). | Alinear `spec.md` §14 con el código en una revisión transversal; este spec usa solo los 4 campos. | **Pendiente (transversal)** |

> **Estado:** Fase 1 y Fase 2 aprobadas por el propietario e implementadas (2026-11-16). **D-ORD-05 resuelta el 2026-11-18** (ficha nueva `GET /api/v1/sellers/me/orders`, §5.10, en implementación). Siguen pendientes D-ORD-09 (borrado de cuenta, bloqueada por DEC-003) y D-ORD-10 (alineación transversal de `ApiError`).
