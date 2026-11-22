# Guía de integración móvil · UTTT-GO Backend

> Versión 1.5 · 2026-11-21 · Contrato vigente de la API pública desplegada (incluye logout API-003 con revocación JWT en Redis).
> Fuente canónica: `docs/constitution.md` y `docs/traceability.md`; el documento OpenAPI vivo es `GET /openapi/v1.json` y la consola de lectura es `GET /openapi`.

## 1. Entorno y transporte

| Elemento | Valor |
|---|---|
| Base HTTPS (producción) | `https://3.129.142.54.nip.io` |
| Prefijo de la API | `/api/v1` |
| OpenAPI (JSON) | `https://3.129.142.54.nip.io/openapi/v1.json` |
| Swagger UI (solo lectura) | `https://3.129.142.54.nip.io/openapi` |
| Sonda operativa | `GET /health` → `200 {"status":"Healthy","checks":{...}}` |
| JSON | `camelCase`, UTF-8, `Content-Type: application/json` |
| Fechas | ISO-8601 UTC (`2026-11-16T18:00:00Z`) |
| Dinero | número decimal con punto decimal (`55.50`), moneda `MXN` |
| Identificadores | `ObjectId` de MongoDB serializado como **string** de 24 hex (`6afb4bf7b74aeb07483052d1`) |

Reglas de transporte:

- **Siempre HTTPS.** El campo `servers` del OpenAPI declara `https://3.129.142.54.nip.io`; la app móvil no debe construir URLs a partir del esquema con el que llegó la petición.
- **Autenticación:** `Authorization: Bearer <accessToken>` en toda ruta autenticada. El token se obtiene en `POST /api/v1/auth/login`.
- **Errores:** todas las respuestas de error usan el contrato único `{correlationId, status, code, message}`. `correlationId` es el `TraceIdentifier` de la petición: debe incluirse en los reportes.
- **Rate limits:** distribuidos en Redis y **fail-closed** (si Redis no responde, la operación se rechaza). Al recibir `429` hay que esperar la ventana indicada en la cabecera del límite.

| Política | Límite | Ventana | Partición |
|---|---|---|---|
| `auth-register` | 5 | 900 s | IP |
| `auth-login` | 10 (IP) / 5 (correo) | 900 s | IP y correo |
| `catalog-public` | 120 | 60 s | IP |
| `catalog-write` | 30 | 60 s | usuario |
| `order-create` | 10 | 60 s | usuario |
| `order-read` | 60 | 60 s | usuario |
| `order-write` | 30 | 60 s | usuario |
| `order-cancel` | 10 | 60 s | usuario |
| `order-delivery-token` | 5 | 300 s | usuario |
| `order-delivery-confirm` | 20 | 60 s | usuario |
| `seller-me` / `seller-write` | 60 / 30 | 60 s | usuario |
| `admin-users` | 30 | 60 s | usuario |

## 2. Autenticación (API-001, API-002, API-004, API-006)

### 2.1 `POST /api/v1/auth/register` → `202`

```json
{ "email": "alumna@uttt.edu.mx", "password": "Clave-de-prueba-1", "displayName": "Alumna" }
```

```json
{ "message": "Si el registro puede procesarse, recibirás instrucciones para verificar tu correo." }
```

`202` es genérico a propósito: no revela si el correo ya existe. La verificación la hace **Supabase** (el móvil usa `verifyOtp`/`resend`); el backend no envía correos.

Errores: `400 VALIDATION_ERROR`, `413` (cuerpo mayor a 8 KB), `422 INSTITUTIONAL_EMAIL_REQUIRED` (el correo no es institucional), `429 RATE_LIMITED`, `503 AUTH_UNAVAILABLE`.

### 2.2 `POST /api/v1/auth/login` → `200`

```json
{ "email": "alumna@uttt.edu.mx", "password": "Clave-de-prueba-1" }
```

```json
{
  "accessToken": "eyJhbGciOi...",
  "expiresIn": 3600,
  "tokenType": "Bearer",
  "user": { "id": "665f1f77bcf86cd799439001", "roles": ["Buyer"], "accountStatus": "Active" }
}
```

Errores: `400 VALIDATION_ERROR`, `401 INVALID_CREDENTIALS`, `429 RATE_LIMITED`, `503 AUTH_UNAVAILABLE`.

### 2.3 `GET /api/v1/auth/me` → `200` (Bearer)

```json
{
  "id": "665f1f77bcf86cd799439001",
  "email": "alumna@uttt.edu.mx",
  "displayName": "Alumna",
  "roles": ["Buyer"],
  "accountStatus": "Active"
}
```

### 2.4 `POST /api/v1/auth/logout` → `204` (Bearer)

El móvil debe enviar el access token vigente en `Authorization: Bearer <accessToken>`. El backend valida firma, issuer, audience, expiración y `jti`, y registra `revoked:{jti}` en Redis hasta la expiración del token. La respuesta `204` no tiene cuerpo. No se envía refresh token al backend ni se revoca la sesión global de Supabase.

Errores: `401 UNAUTHORIZED` si falta el header, el JWT es inválido, expiró o ya fue revocado; `503 AUTH_UNAVAILABLE` si Redis no está disponible.

### 2.5 `GET /api/v1/users/me` → `200` (Bearer) · `PATCH /api/v1/users/me` → `200`

```json
{
  "id": "665f1f77bcf86cd799439001",
  "displayName": "Alumna",
  "email": "alumna@uttt.edu.mx",
  "phone": "1234567890",
  "avatarUrl": null,
  "roles": ["Buyer"]
}
```

`PATCH` acepta **solo** `displayName` y `phone` (whitelist estricta; cualquier otro campo → `400 VALIDATION_ERROR`). `avatarUrl` siempre es `null`: no hay Media/imágenes todavía.

## 3. Vendedores (API-008, API-009, API-010, API-039)

### 3.1 `POST /api/v1/sellers/activation-requests` → `201` (Bearer, cuenta `Active`)

```json
{ "storeName": "Puesto de la Alumna", "description": "Tacos y bebidas", "phone": "55 1234 5678" }
```

```json
{
  "sellerId": "665f1a108b3f2a0012ab34c1",
  "storeName": "Puesto de la Alumna",
  "description": "Tacos y bebidas",
  "phone": "55 1234 5678",
  "status": "Active",
  "isActive": true,
  "roles": ["Buyer", "Seller"],
  "createdAt": "2026-11-16T18:00:00Z",
  "updatedAt": "2026-11-16T18:00:00Z"
}
```

**Autoactivación**: no existe aprobación administrativa. Al completar `storeName`, `description` y `phone`, el vendedor queda `Active` y recibe el rol `Seller` en el perfil local. Repetir la llamada devuelve `200` con el mismo perfil.

El cuerpo acepta **solo** `storeName` (1..100), `description` (1..500) y `phone` (3..32). Los nombres antiguos `businessName` y `pickupPoint` ya **no** se aceptan (`400 VALIDATION_ERROR`). El `phone` es el contacto del negocio: el backend lo guarda internamente como punto de recogida del pedido, así que es el dato que el comprador ve en el pedido.

Errores: `400 VALIDATION_ERROR`, `401`, `403 ACCOUNT_NOT_ACTIVE`, `404`, `429`, `503`.

### 3.2 `GET /api/v1/sellers/me` · `PATCH /api/v1/sellers/me` → `200` (Bearer)

Mismo cuerpo que la activación (`storeName`, `description`, `phone`, `status`, `isActive`, `roles`, `createdAt`, `updatedAt`). Tras la activación, `GET /sellers/me` responde `200` con el perfil del negocio activo (`status: "Active"`, `isActive: true`), de modo que el frontend puede validar permisos de inmediato; si el usuario no es vendedor responde `404 SELLER_PROFILE_NOT_FOUND`.

`PATCH` acepta solo `storeName`, `description` y `phone` (los campos omitidos se conservan); nunca `roles`, `sellerId`, `status` ni `version`. `409 SELLER_CONFLICT` indica que el perfil cambió y hay que releerlo.

### 3.3 `GET /api/v1/sellers/{sellerId}` → `200` (público)

```json
{
  "sellerId": "665f1a108b3f2a0012ab34c1",
  "displayName": "Puesto de la Alumna",
  "ratingAverage": null,
  "ratingCount": null,
  "isActive": true
}
```

Los ratings son `null` porque el módulo de Reseñas no existe (fail-open, sin inventar puntuaciones). No se exponen datos de contacto ni la lista de productos.

## 4. Catálogo (API-011 a API-015, API-019)

### 4.1 `GET /api/v1/categories` → `200` (público)

Respuesta real de la instancia tras el seeding:

```json
{
  "items": [
    { "id": "6afb4bf7b74aeb07483052d1", "name": "Comida Preparada", "sortOrder": 10 },
    { "id": "6afb4bf7b74aeb07483052d2", "name": "Bebidas", "sortOrder": 20 },
    { "id": "6afb4bf7b74aeb07483052d3", "name": "Snacks", "sortOrder": 30 }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 3
}
```

Las categorías base se siembran de forma idempotente en la instancia (`CATALOG_SEED_ENABLED=true`): el `upsert` es por `normalizedName`, así que repetirlo nunca duplica ni altera el `id`.

### 4.2 `GET /api/v1/products` → `200` (público)

Parámetros: `search`, `categoryId`, `minPrice`, `maxPrice`, `page` (≥1), `pageSize` (1..50), `sortBy` (`createdAtDesc`, `priceAsc`, `priceDesc`, `nameAsc`).

```json
{
  "items": [
    {
      "id": "6afb4c1a8b3f2a0012ab34d0",
      "sellerId": "665f1a108b3f2a0012ab34c1",
      "sellerDisplayName": "Puesto de la Alumna",
      "name": "Tacos de pastor (3 pza)",
      "description": "Tacos con piña",
      "ingredients": ["Piña", "Cilantro"],
      "categoryId": "6afb4bf7b74aeb07483052d1",
      "categoryName": "Comida Preparada",
      "price": 55.50,
      "stock": 10,
      "status": "Active",
      "isAvailable": true,
      "ratingAverage": null,
      "ratingCount": null,
      "createdAt": "2026-11-16T18:00:00Z",
      "updatedAt": "2026-11-16T18:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

Solo se listan productos `Active` de vendedores `Active` con categoría activa. `ratingAverage`/`ratingCount` son `null` (no existe Reviews). Errores: `400 VALIDATION_ERROR`, `429`, `503 CATALOG_UNAVAILABLE`.

### 4.3 `GET /api/v1/products/{productId}` → `200` (público)

Mismo objeto `ProductResponse`. `404 PRODUCT_NOT_FOUND` si el producto no existe, no está disponible o es de otro vendedor no visible; `400 INVALID_PRODUCT_ID` si el id no es un ObjectId de 24 hex.

### 4.4 `POST /api/v1/products` → `201` (Bearer, rol `Seller`)

```json
{
  "name": "Tacos de pastor (3 pza)",
  "description": "Tacos con piña",
  "ingredients": ["Piña", "Cilantro"],
  "categoryId": "6afb4bf7b74aeb07483052d1",
  "price": 55.50,
  "stock": 10,
  "imageUrl": null
}
```

El cliente **no** envía `sellerId`, `status`, `version` ni `categoryName`: el backend los deduce (el vendedor del token, la categoría enviada). Errores: `400 VALIDATION_ERROR`, `401`, `403 SELLER_ROLE_REQUIRED` / `SELLER_ACCOUNT_NOT_ACTIVE`, `404 CATEGORY_NOT_FOUND`, `422 IMAGE_NOT_AVAILABLE`, `429`, `503`.

`imageUrl` es **opcional** y puede omitirse o enviarse en `null`. Mientras no exista el módulo Media (S3 presigned + moderación con Amazon Rekognition, fase 05), el backend **no almacena** ninguna imagen: si llega con valor no vacío responde `422 IMAGE_NOT_AVAILABLE` y el producto se publica sin foto. La app móvil debe enviarlo en `null` u omitirlo.

### 4.5 `PATCH /api/v1/products/{productId}` → `200` (Bearer, dueño)

```json
{ "name": "Tacos de pastor (4 pza)", "price": 59.50, "stock": 8, "ingredients": ["Piña"], "description": "Tacos con piña" }
```

Solo el dueño (verificado por recurso). `409 PRODUCT_CONFLICT` si el `version` quedó desactualizado: releer y reintentar.

### 4.6 `PATCH /api/v1/products/{productId}/availability` → `200` (Bearer, dueño)

```json
{ "status": "Paused" }
```

`Active` publica y `Paused` pausa la publicación. Pausar **nunca** altera el stock.

## 5. Pedidos (API-023 a API-029)

Compra directa e individual: **no hay carrito** y **no hay pasarela de pago**. El único método de pago es `Cash` en el campus.

### 5.0 Flujo de estados y quién puede actuar

```
Pending ──(vendedor, API-027)──> Accepted ──(vendedor, API-027)──> InPreparation ──(vendedor, API-027)──> Ready
   │                                                                                                    │
   │(comprador, API-026)                                                                                │(vendedor, API-029 + token)
   ▼                                                                                                    ▼
CancelledByBuyer                                                                                     Delivered
Pending ──(vendedor, API-027)──> Rejected
```

- Solo el **vendedor del pedido** ejecuta API-027 y API-029; solo el **comprador** ejecuta API-026 y API-028.
- Un tercero recibe `404 ORDER_NOT_FOUND` (no se revela la existencia); la contraparte del pedido recibe `403 ORDER_FORBIDDEN`.
- `Accepted → CancelledByBuyer` está **prohibido**: responde `409 ORDER_STATE_CONFLICT`.
- `Delivered` **solo** se alcanza con API-029 (token de entrega); API-027 no puede fijarlo.

### 5.1 `POST /api/v1/orders` → `201` (Bearer, cuenta `Active`)

```json
{ "productId": "6afb4c1a8b3f2a0012ab34d0", "quantity": 2, "notes": "Sin cebolla, por favor" }
```

`notes` es **opcional**: una nota del comprador para el vendedor, máximo **250 caracteres** (se recorta al trimear; vacía o ausente se guarda como `null`).

Cabecera opcional de idempotencia (recomendada para reintentos de red): un UUID v4 **único por intento de compra**, que el móvil genera y conserva entre reintentos de la misma operación.

```
Idempotency-Key: <uuid-v4-unico-por-intento>
```

```json
{
  "orderId": "6afb4d2b8b3f2a0012ab34d1",
  "productId": "6afb4c1a8b3f2a0012ab34d0",
  "productName": "Tacos de pastor (3 pza)",
  "sellerId": "665f1a108b3f2a0012ab34c1",
  "buyerId": "665f1f77bcf86cd799439001",
  "quantity": 2,
  "unitPrice": 55.50,
  "totalAmount": 111.00,
  "currency": "MXN",
  "paymentMethod": "Cash",
  "status": "Pending",
  "pickupPoint": "Biblioteca central",
  "deliveredAt": null,
  "completedAt": null,
  "createdAt": "2026-11-16T18:00:00Z",
  "updatedAt": "2026-11-16T18:00:00Z",
  "version": 0,
  "notes": "Sin cebolla, por favor"
}
```

Reglas que la app móvil debe respetar:

- El cuerpo acepta **solo** `productId`, `quantity` y `notes`. Enviar `price`, `total`, `totalAmount`, `sellerId`, `buyerId`, `buyerUserId`, `status`, `paymentMethod`, `currency`, `version` o `roles` → `400 VALIDATION_ERROR`.
- El **total lo calcula el servidor** (`unitPrice` vigente × `quantity`); el cliente nunca decide el precio.
- La compra **reserva stock** de forma atómica: si no hay existencias suficientes → `400 INSUFFICIENT_STOCK` y no se crea el pedido.
- `quantity` entre 1 y 50; `productId` con 24 hex.
- Repetir la misma `Idempotency-Key` del mismo comprador devuelve `200` con el pedido ya creado (no `201`) y **no** reserva stock otra vez.
- Estados: el pedido nace `Pending`; las transiciones las hace el vendedor con API-027 y la entrega se confirma con API-029 (ver 5.4..5.7).

Errores: `400 VALIDATION_ERROR` / `400 INSUFFICIENT_STOCK`, `401`, `403 ACCOUNT_NOT_ACTIVE`, `404 PRODUCT_NOT_FOUND`, `422 PRODUCT_NOT_AVAILABLE` (producto pausado, categoría inactiva o vendedor no activo), `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE`.

### 5.2 `GET /api/v1/orders/mine` → `200` (Bearer)

Parámetros: `page` (≥1), `pageSize` (1..50), `status` (opcional).

```json
{
  "items": [
    {
      "orderId": "6afb4d2b8b3f2a0012ab34d1",
      "productId": "6afb4c1a8b3f2a0012ab34d0",
      "productName": "Tacos de pastor (3 pza)",
      "sellerId": "665f1a108b3f2a0012ab34c1",
      "buyerId": "665f1f77bcf86cd799439001",
      "quantity": 2,
      "unitPrice": 55.50,
      "totalAmount": 111.00,
      "currency": "MXN",
      "status": "Pending",
      "role": "buyer",
      "createdAt": "2026-11-16T18:00:00Z",
      "notes": "Sin cebolla, por favor"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

Solo devuelve pedidos del comprador autenticado (el filtro por rol vendedor aún no existe). Cada ítem trae la relación completa: `orderId`, `productId`, `productName`, `sellerId`, `buyerId`, `quantity`, `unitPrice`, `totalAmount`, `currency`, `status`, `role`, `createdAt` y `notes`.

### 5.3 `GET /api/v1/orders/{orderId}` → `200` (Bearer, participante)

Devuelve el mismo objeto de `POST /orders` (`orderId`, `productId`, `productName`, `sellerId`, `buyerId`, `quantity`, `unitPrice`, `totalAmount`, `currency`, `paymentMethod`, `status`, `pickupPoint`, `deliveredAt`, `completedAt`, `createdAt`, `updatedAt`, `version`, `notes`). Solo lo leen el comprador o el vendedor del pedido; un tercero recibe `404 ORDER_NOT_FOUND` **sin** que se revele la existencia.

### 5.4 `POST /api/v1/orders/{orderId}/cancel` → `200` (Bearer, comprador)

Sin cuerpo de petición.

```json
{
  "orderId": "6afb4d2b8b3f2a0012ab34d1",
  "status": "CancelledByBuyer",
  "completedAt": "2026-11-16T18:10:00Z",
  "version": 1
}
```

- Solo el **comprador** y solo desde `Pending`; desde `Accepted` o cualquier otro estado → `409 ORDER_STATE_CONFLICT`.
- **Idempotente**: repetir la cancelación devuelve `200` con el estado vigente, sin duplicar auditoría ni devolver el stock dos veces.
- Libera el stock reservado del producto y fija `completedAt`.
- El vendedor recibe `403 ORDER_FORBIDDEN`; un tercero `404 ORDER_NOT_FOUND`.

Errores: `401`, `403 ACCOUNT_NOT_ACTIVE`/`403 ORDER_FORBIDDEN`, `404 ORDER_NOT_FOUND`, `409 ORDER_STATE_CONFLICT`, `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE`.

### 5.5 `PATCH /api/v1/orders/{orderId}/status` → `200` (Bearer, vendedor)

```json
{ "status": "Accepted", "version": 0 }
```

```json
{
  "orderId": "6afb4d2b8b3f2a0012ab34d1",
  "status": "Accepted",
  "version": 1,
  "updatedAt": "2026-11-16T18:05:00Z"
}
```

- Transiciones permitidas: `Pending→Accepted`, `Pending→Rejected`, `Accepted→InPreparation`, `InPreparation→Ready`.
- `version` es **obligatoria** y es la versión que el móvil leyó del pedido: sirve como control de concurrencia. Si otro actor cambió el pedido primero → `409 ORDER_VERSION_CONFLICT` (el móvil debe releer el pedido y reintentar con la nueva versión).
- `Delivered` y `CancelledByBuyer` **no** se pueden fijar por esta ruta → `409 ORDER_STATE_CONFLICT`.
- `Rejected` libera el stock reservado y fija `completedAt` (el pedido queda cerrado).
- El cuerpo acepta **solo** `status` y `version`; cualquier otro campo → `400 VALIDATION_ERROR`.
- El comprador recibe `403 ORDER_FORBIDDEN`; un tercero `404 ORDER_NOT_FOUND`.

Errores: `400 VALIDATION_ERROR`, `401`, `403 ACCOUNT_NOT_ACTIVE`/`403 ORDER_FORBIDDEN`, `404 ORDER_NOT_FOUND`, `409 ORDER_STATE_CONFLICT`, `409 ORDER_VERSION_CONFLICT`, `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE`.

### 5.6 `POST /api/v1/orders/{orderId}/delivery-token` → `201` (Bearer, comprador)

Sin cuerpo de petición. Solo con el pedido en `Ready`.

```json
{
  "orderId": "6afb4d2b8b3f2a0012ab34d1",
  "token": "<token-base64url-de-43-caracteres>",
  "expiresAt": "2026-11-16T18:25:00Z"
}
```

- El token es de un solo uso, tiene **43 caracteres** en base64url (32 bytes aleatorios) y **caduca a los 15 minutos** (`expiresAt`).
- Pedir un token nuevo **invalida** el anterior: el móvil debe usar siempre el último recibido.
- El backend guarda **solo** el hash del token; el token en claro nunca se devuelve en el detalle del pedido ni se registra en logs.
- El **QR lo genera el móvil** a partir de `token` (el backend no genera imágenes).
- Desde un estado distinto de `Ready` → `409 ORDER_STATE_CONFLICT`; el vendedor recibe `403 ORDER_FORBIDDEN`; un tercero `404 ORDER_NOT_FOUND`.

Errores: `401`, `403 ACCOUNT_NOT_ACTIVE`/`403 ORDER_FORBIDDEN`, `404 ORDER_NOT_FOUND`, `409 ORDER_STATE_CONFLICT`, `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE`.

### 5.7 `POST /api/v1/orders/{orderId}/delivery-confirmations` → `200` (Bearer, vendedor)

```json
{ "token": "<token-base64url-de-43-caracteres>" }
```

```json
{
  "orderId": "6afb4d2b8b3f2a0012ab34d1",
  "status": "Delivered",
  "deliveredAt": "2026-11-16T18:20:00Z",
  "version": 5
}
```

- Solo el **vendedor del pedido** confirma la entrega en efectivo (BR-003): el comprador recibe `403 ORDER_FORBIDDEN` y un tercero `404 ORDER_NOT_FOUND`.
- El token debe ser el vigente emitido por API-028: si falta o no coincide → `422 DELIVERY_TOKEN_INVALID`; si expiró → `422 DELIVERY_TOKEN_EXPIRED`; si ya se usó → `409 DELIVERY_TOKEN_ALREADY_USED`.
- El token se marca como usado de forma atómica: **no se puede reutilizar** para una segunda entrega.
- Fija `deliveredAt` y `completedAt`, confirma el stock (la reserva pasa a venta) y el pedido queda en `Delivered`.
- Si otro actor cambió el pedido mientras tanto → `409 ORDER_VERSION_CONFLICT` o `409 ORDER_STATE_CONFLICT`.

Errores: `400 VALIDATION_ERROR`, `401`, `403 ACCOUNT_NOT_ACTIVE`/`403 ORDER_FORBIDDEN`, `404 ORDER_NOT_FOUND`, `409 ORDER_STATE_CONFLICT`, `409 ORDER_VERSION_CONFLICT`, `409 DELIVERY_TOKEN_ALREADY_USED`, `422 DELIVERY_TOKEN_INVALID`, `422 DELIVERY_TOKEN_EXPIRED`, `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE`.

### 5.8 `GET /api/v1/sellers/me/orders` → `200` (Bearer, vendedor)

Query: `page` (≥1), `pageSize` (≤50) y `status` opcional (`Pending|Accepted|Rejected|InPreparation|Ready|Delivered|CancelledByBuyer`).

```json
{
  "items": [
    {
      "orderId": "6afb4d2b8b3f2a0012ab34d1",
      "buyerId": "6afb4a1c8b3f2a0012ab3310",
      "sellerId": "6afb4b5d8b3f2a0012ab3402",
      "productName": "Tacos de canasta",
      "unitPrice": 45.0,
      "quantity": 2,
      "totalAmount": 90.0,
      "status": "Pending",
      "role": "seller",
      "notes": null,
      "createdAt": "2026-11-18T17:05:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 3
}
```

- **Ficha nueva aprobada (D-ORD-05, 2026-11-18)**: es la bandeja «Pedidos Recibidos» del vendedor; no es una de las 42 fichas del Excel.
- Cada item trae `role: "seller"`; el `sellerId` lo deriva el servidor del perfil autenticado (el cliente nunca lo envía).
- Solo el rol `Seller` con cuenta `Active`: sin rol → `403 SELLER_ROLE_REQUIRED`; cuenta inactiva → `403 SELLER_ACCOUNT_NOT_ACTIVE`.
- `/api/v1/orders/mine` **no cambia** (sigue siendo solo del comprador, `role: "buyer"`); para ver el detalle de un pedido ya recibido sigue existiendo `GET /api/v1/orders/{orderId}` (participantes; un tercero recibe `404`).
- Rate limit `order-read` (60/60 s) fail-closed: Redis caído → `503 ORDER_UNAVAILABLE`.

Errores: `400 VALIDATION_ERROR`, `401`, `403 SELLER_ROLE_REQUIRED`/`403 SELLER_ACCOUNT_NOT_ACTIVE`, `429 RATE_LIMITED`, `503 ORDER_UNAVAILABLE`.

## 6. Contrato único de error

```json
{
  "correlationId": "0HNEU7KQ3F5V2:00000003",
  "status": 400,
  "code": "INSUFFICIENT_STOCK",
  "message": "No hay existencias suficientes."
}
```

Códigos en uso: `VALIDATION_ERROR`, `UNAUTHORIZED`, `INVALID_CREDENTIALS`, `INSTITUTIONAL_EMAIL_REQUIRED`, `PROFILE_NOT_FOUND`, `ACCOUNT_NOT_ACTIVE`, `SELLER_ROLE_REQUIRED`, `SELLER_ACCOUNT_NOT_ACTIVE`, `SELLER_CONFLICT`, `SELLER_PROFILE_NOT_FOUND`, `PRODUCT_NOT_FOUND`, `CATEGORY_NOT_FOUND`, `INVALID_PRODUCT_ID`, `PRODUCT_CONFLICT`, `ORDER_NOT_FOUND`, `ORDER_FORBIDDEN`, `ORDER_STATE_CONFLICT`, `ORDER_VERSION_CONFLICT`, `DELIVERY_TOKEN_INVALID`, `DELIVERY_TOKEN_EXPIRED`, `DELIVERY_TOKEN_ALREADY_USED`, `INSUFFICIENT_STOCK`, `PRODUCT_NOT_AVAILABLE`, `IMAGE_NOT_AVAILABLE`, `RATE_LIMITED`, `AUTH_UNAVAILABLE`, `CATALOG_UNAVAILABLE`, `ORDER_UNAVAILABLE`, `SELLER_UNAVAILABLE`.

## 7. Flujo mínimo de integración

1. `POST /api/v1/auth/register` → `202`; el móvil pide a Supabase `verifyOtp`/`resend` con el correo `@uttt.edu.mx` confirmado.
2. `POST /api/v1/auth/login` → `200`; guardar `accessToken` (expira en 3600 s) y `user.roles`.
3. `GET /api/v1/categories` → `200`; usar los `id` de categoría al publicar productos.
4. Vendedor: `POST /api/v1/sellers/activation-requests` → `201` (autoactivación) y luego `POST /api/v1/products` → `201`.
5. Comprador: `GET /api/v1/products?search=...` → `200`; `POST /api/v1/orders` → `201` con `Idempotency-Key`; `GET /api/v1/orders/mine` y `GET /api/v1/orders/{orderId}` → `200`.
6. Vendedor: `PATCH /api/v1/orders/{orderId}/status` → `200` con `{status, version}` siguiendo `Pending → Accepted → InPreparation → Ready`.
7. Comprador (pedido `Ready`): `POST /api/v1/orders/{orderId}/delivery-token` → `201`; el móvil dibuja el QR con `token` y se lo muestra al vendedor.
8. Vendedor (tras cobrar en efectivo): `POST /api/v1/orders/{orderId}/delivery-confirmations` → `200` con `{token}`; el pedido queda en `Delivered`.
9. Comprador (mientras el pedido siga en `Pending`): `POST /api/v1/orders/{orderId}/cancel` → `200`.

## 8. Límites conocidos (no confundir con fallos)

- **Sin imágenes**: no existe Media/S3 presigned; `avatarUrl` es `null`, los productos no tienen imágenes y `POST /products` con `imageUrl` con valor responde `422 IMAGE_NOT_AVAILABLE`.
- El logout revoca el access token actual en Redis; el access token expira como máximo a la hora y no se envía refresh token al backend.
- **Sin reseñas**: `ratingAverage` y `ratingCount` son `null` en productos y vendedores.
- **Sin "mis productos" ni filtro `sellerId`**: el catálogo no permite filtrar por vendedor; está decidido para la siguiente iteración (el índice `sellerId_status` ya existe).
- **Sin eliminación de producto**: no hay DELETE.
- **Sin chat ni notificaciones push**: pertenecen a fases posteriores; la Fase 2 de pedidos no emite push (D-ORD-15).
- **Sin `role` en `/orders/mine`**: siempre `buyer`. El listado del vendedor es la ruta separada `GET /sellers/me/orders` (§5.8) con `role: "seller"`.
- **Sin reseñas tras la entrega**: la entrega no genera automáticamente una reseña (fase 06 pendiente).

## 9. Cambios incompatibles del contrato (v1.3, 2026-11-16)

Estos cambios son **rompientes** para el móvil y deben aplicarse en el mismo despliegue:

| Antes | Ahora | Dónde |
|---|---|---|
| `businessName` | `storeName` | `POST /sellers/activation-requests`, `GET/PATCH /sellers/me` |
| `pickupPoint` (en el vendedor) | `phone` | `POST /sellers/activation-requests`, `GET/PATCH /sellers/me` |
| `total` (contador de página) | `totalCount` | `GET /products`, `GET /categories`, `GET /orders/mine`, `GET /admin/users` |
| `buyerUserId` | `buyerId` | `POST /orders`, `GET /orders/{id}`, `GET /orders/mine` |
| `total` (importe del pedido) | `totalAmount` | `POST /orders`, `GET /orders/{id}`, `GET /orders/mine` |

Notes:

- `pickupPoint` **sigue existiendo** en la respuesta del **pedido** (`POST /orders`, `GET /orders/{id}`): es el punto de recogida que ve el comprador. Solo cambió el nombre del campo del **perfil del vendedor**.
- El `phone` del vendedor se guarda internamente como punto de recogida del pedido, así que el valor que el comprador ve en `pickupPoint` es el mismo `phone` que registró el vendedor.
- El OpenAPI vivo (`GET /openapi/v1.json`) ya publica estos esquemas con sus respuestas reales (200/201/4xx/5xx) para las rutas de vendedores, catálogo, pedidos y admin.
