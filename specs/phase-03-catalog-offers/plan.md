# plan.md — Arquitectura e Implementación: Catálogo y Ofertas Comparables · Backend UTTT-GO
> Fase: `phase-03-catalog-offers` · Módulo de implementación: `CatalogAndOffers`
> Versión: 1.0 · Estado: **Aprobado** · Fecha: 2026-10-29
> Deriva de `specs/phase-03-catalog-offers/spec.md` v1.1 y se subordina a `constitution.md` v2.1 y `AGENTS.md` v2.0.
> Este documento **no** contiene código de producción; describe estructura, modelos, contratos, caché, reserva de stock y pruebas.

---

## 1. Objetivo y Alcance del Plan

Materializar el spec `phase-03-catalog-offers` en el monolito modular `UtttGo.Api`:

- Módulo `CatalogAndOffers` con fronteras internas entre catálogo, ofertas, caché y reserva de stock.
- Persistencia en MongoDB Atlas de `products`, `offers` y `categories` (semilla de solo lectura).
- Contratos públicos `/api/v1/products/*` y contratos de escritura `/api/v1/offers/*`.
- Caché Redis `cache:catalog:*` con TTL 60 s, invalidación en escritura y fail-open.
- Reserva/liberación atómica de stock como contrato para el módulo `Orders`.
- Pruebas unitarias, integración con Testcontainers/Azurite y seguridad.
- Trazabilidad RF-01..RF-15 → sección → prueba.

Fuera de este plan: código de producción, Terraform, ciclo de vida de pedidos, reseñas/favoritos y CRUD de categorías.

---

## 2. Estructura Interna del Módulo

Ruta raíz: `src/UtttGo.Api/Modules/CatalogAndOffers/`

```
CatalogAndOffers/
├── DependencyInjection.cs                  # AddCatalogAndOffers(...)
├── Endpoints/
│   ├── ProductEndpoints.cs                 # GET /api/v1/products*
│   ├── CategoryEndpoints.cs                # GET /api/v1/products/categories
│   ├── SellerOfferEndpoints.cs             # POST /api/v1/products, /offers/*
│   └── AdminOfferEndpoints.cs              # PATCH /api/v1/offers/{id}/suspend
├── Application/
│   ├── Abstractions/
│   │   ├── IProductRepository.cs
│   │   ├── IOfferRepository.cs
│   │   ├── ICategoryRepository.cs
│   │   ├── ICatalogCache.cs                # get/set/invalidate
│   │   ├── IStockReservationService.cs     # contrato para Orders
│   │   ├── IMediaAssetValidator.cs         # contrato de Media
│   │   └── ISellerDirectory.cs             # contrato de IdentityAndUsers
│   ├── Services/
│   │   ├── ProductService.cs
│   │   ├── OfferService.cs
│   │   ├── CatalogQueryService.cs
│   │   ├── CategoryService.cs
│   │   ├── StockReservationService.cs
│   │   └── AdminOfferService.cs
│   ├── Caching/
│   │   ├── CatalogCacheKeys.cs
│   │   └── CatalogCacheInvalidator.cs
│   └── Mappings/
│       └── CatalogMappings.cs
├── Domain/
│   ├── Entities/
│   │   ├── Product.cs
│   │   ├── Offer.cs
│   │   └── Category.cs
│   ├── Enums/
│   │   ├── ProductStatus.cs                # Active | Inactive
│   │   ├── OfferStatus.cs                  # Active | Inactive | Suspended | Deleted
│   │   └── OfferAvailability.cs            # Available | SoldOut | Inactive | Suspended | Deleted
│   ├── Normalization/
│   │   └── ProductNameNormalizer.cs
│   └── Errors/
│       └── CatalogErrorCodes.cs
├── Infrastructure/
│   ├── Persistence/
│   │   ├── MongoProductRepository.cs
│   │   ├── MongoOfferRepository.cs
│   │   ├── MongoCategoryRepository.cs
│   │   ├── MongoIndexes.cs                 # EnsureIndexes idempotente
│   │   └── CatalogClassMap.cs
│   └── Redis/
│       └── RedisCatalogCache.cs
├── Contracts/
│   ├── Requests/
│   │   ├── CreateProductWithOfferRequest.cs
│   │   ├── CreateOfferRequest.cs
│   │   ├── UpdateOfferRequest.cs
│   │   ├── ProductQuery.cs
│   │   └── CategoryQuery.cs
│   └── Responses/
│       ├── ProductSummaryResponse.cs
│       ├── ProductDetailResponse.cs
│       ├── OfferResponse.cs
│       ├── OfferComparisonResponse.cs
│       ├── CategoryResponse.cs
│       └── PagedResponse.cs
└── Validators/
    ├── CreateProductWithOfferRequestValidator.cs
    ├── CreateOfferRequestValidator.cs
    ├── UpdateOfferRequestValidator.cs
    └── ProductQueryValidator.cs
```

Reglas de frontera:

- `Endpoints` solo orquestan; `Services` contienen reglas; `Infrastructure` accede a Mongo/Redis.
- `CatalogAndOffers` posee `products`, `offers` y `categories`; no accede a colecciones de `IdentityAndUsers` ni `Media` (usa `ISellerDirectory` e `IMediaAssetValidator`).
- `Orders` consume `IStockReservationService`; no se comparte el repositorio de ofertas.
- El `sellerId` se deriva del perfil autenticado, nunca del body.

**Trazabilidad:** RF-01..RF-15 (fronteras), RNF-02, RNF-03.

---

## 3. Modelo NoSQL (MongoDB Atlas / POCOs)

### 3.1 Colección `products` (producto canónico)

```csharp
public sealed class Product
{
    [BsonId] public ObjectId Id { get; set; }

    [BsonElement("name")] public string Name { get; set; } = default!;
    [BsonElement("normalizedName")] public string NormalizedName { get; set; } = default!;
    [BsonElement("description")] public string Description { get; set; } = default!;
    [BsonElement("categoryId")] public ObjectId CategoryId { get; set; }
    [BsonElement("imageMediaAssetId")] public string ImageMediaAssetId { get; set; } = default!;

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public ProductStatus Status { get; set; } = ProductStatus.Active;

    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }
    [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; }
}
```

`ProductStatus`: `Active = 0`, `Inactive = 1` (reservado para operación; en MVP los productos nacen `Active`). **No** contiene `price`, `stock` ni `sellerId` (RF-03).

### 3.2 Colección `offers` (oferta de vendedor)

```csharp
public sealed class Offer
{
    [BsonId] public ObjectId Id { get; set; }

    [BsonElement("productId")] public ObjectId ProductId { get; set; }
    [BsonElement("sellerId")] public ObjectId SellerId { get; set; }

    // Snapshot de presentación, refrescado al crear/editar la oferta.
    [BsonElement("sellerBusinessName")] public string SellerBusinessName { get; set; } = default!;
    [BsonElement("pickupPoint")] public string PickupPoint { get; set; } = default!;

    [BsonElement("price")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Price { get; set; }               // MXN, > 0

    [BsonElement("stock")] public int Stock { get; set; }  // >= 0
    [BsonElement("conditionNotes")] public string? ConditionNotes { get; set; }

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public OfferStatus Status { get; set; } = OfferStatus.Active;

    [BsonElement("version")] public int Version { get; set; }  // concurrencia optimista

    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }
    [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; }
    [BsonElement("suspendedAt")] public DateTime? SuspendedAt { get; set; }
    [BsonElement("suspendedBy")] public ObjectId? SuspendedBy { get; set; }
    [BsonElement("deletedAt")] public DateTime? DeletedAt { get; set; }
}
```

`OfferStatus`: `Active = 0`, `Inactive = 1`, `Suspended = 2`, `Deleted = 3`.

Disponibilidad derivada (no persistida):

| Condición | `OfferAvailability` | Comprable |
|---|---|---|
| `Status = Active && Stock > 0` | `Available` | Sí |
| `Status = Active && Stock = 0` | `SoldOut` | No |
| `Status = Inactive` | `Inactive` | No |
| `Status = Suspended` | `Suspended` | No |
| `Status = Deleted` | `Deleted` | No |

### 3.3 Colección `categories` (semilla, solo lectura)

```csharp
public sealed class Category
{
    [BsonId] public ObjectId Id { get; set; }
    [BsonElement("name")] public string Name { get; set; } = default!;
    [BsonElement("normalizedName")] public string NormalizedName { get; set; } = default!;
    [BsonElement("sortOrder")] public int SortOrder { get; set; }
    [BsonElement("isActive")] public bool IsActive { get; set; } = true;
}
```

Sin endpoints de escritura (RF-14). Se aprovisiona por seed/operación.

### 3.4 Índices

| Colección | Índice | Tipo | Justificación |
|---|---|---|---|
| `products` | `{ normalizedName: 1, categoryId: 1 }` | Único | Unicidad por nombre normalizado + categoría (RF-02). |
| `products` | `{ status: 1, categoryId: 1, createdAt: -1 }` | Compuesto | Listado/filtro público paginado (RF-09). |
| `offers` | `{ productId: 1, status: 1, price: 1 }` | Compuesto | Comparativa de ofertas comprables ordenadas por precio (RF-10). |
| `offers` | `{ sellerId: 1, status: 1, updatedAt: -1 }` | Compuesto | Listado/gestión de ofertas propias. |
| `offers` | `{ productId: 1, sellerId: 1 }` | Compuesto (no único) | Búsqueda de oferta por vendedor/producto; el spec no exige unicidad. |
| `categories` | `{ normalizedName: 1 }` | Único | Evita categorías duplicadas en la semilla. |
| `categories` | `{ isActive: 1, sortOrder: 1 }` | Compuesto | Listado de categorías activas ordenadas. |

Invariantes:

- `Product.NormalizedName` calculado con `ProductNameNormalizer` (minúsculas, sin acentos, espacios colapsados).
- `Product` no contiene precio/stock/vendedor; la oferta sí (RF-03).
- `Offer.Price` es `decimal` serializado como `Decimal128`; nunca `float`/`double`.
- `Offer.Stock` es entero `>= 0`; `Price > 0`.
- Fechas en UTC (RNF-09).
- `Version` se incrementa en cada mutación; `UpdateOfferRequest` exige `ExpectedVersion` para evitar escrituras perdidas.

**Trazabilidad:** RF-02, RF-03, RF-09, RF-10, RF-14; RNF-05, RNF-09, RNF-12.

---

## 4. Contrato Técnico de Endpoints

### 4.1 Convenciones

- Base `/api/v1`; JSON camelCase; IDs `string`; paginación `page`/`pageSize`.
- Lectura pública con rate limit; escritura con JWT + rol `Seller`/`Admin` desde MongoDB.
- Validación con FluentValidation antes del servicio.
- Error estándar: `{ correlationId, status, code, message, details? }`.

### 4.2 Endpoints públicos de catálogo

| Método | Ruta | Query | Éxito | Errores |
|---|---|---|---|---|
| GET | `/api/v1/products` | `search?`, `categoryId?`, `minPrice?`, `maxPrice?`, `sort?`, `page`, `pageSize` | `200 PagedResponse<ProductSummaryResponse>` | `400`, `429`, `503` |
| GET | `/api/v1/products/categories` | `page`, `pageSize` | `200 PagedResponse<CategoryResponse>` | `400`, `429`, `503` |
| GET | `/api/v1/products/{id}` | — | `200 ProductDetailResponse` | `404 PRODUCT_NOT_FOUND`, `429` |
| GET | `/api/v1/products/{id}/offers` | — | `200 OfferComparisonResponse` | `404 PRODUCT_NOT_FOUND`, `429` |

`sort`: `price_asc` (default), `price_desc`, `name_asc`, `newest`.
`GET /products/{id}/offers` devuelve solo ofertas `Available` ordenadas por `price` ascendente, con `sellerBusinessName`, `pickupPoint`, `price`, `stock`.

### 4.3 Endpoints de escritura (Seller)

| Método | Ruta | Request DTO | Éxito | Errores |
|---|---|---|---|---|
| POST | `/api/v1/products` | `CreateProductWithOfferRequest` | `201 ProductDetailResponse` | `400`, `401`, `403`, `409 PRODUCT_ALREADY_EXISTS`, `422 CATEGORY_NOT_FOUND`, `422 MEDIA_ASSET_NOT_APPROVED` |
| POST | `/api/v1/products/{productId}/offers` | `CreateOfferRequest` | `201 OfferResponse` | `400`, `401`, `403`, `404 PRODUCT_NOT_FOUND`, `409 OFFER_NOT_AVAILABLE`, `422` |
| PATCH | `/api/v1/offers/{offerId}` | `UpdateOfferRequest` | `200 OfferResponse` | `400`, `401`, `403 OFFER_NOT_OWNED`, `404`, `409 CONCURRENCY_CONFLICT`, `422` |
| PATCH | `/api/v1/offers/{offerId}/pause` | — | `200 OfferResponse` | `401`, `403`, `404`, `409 INVALID_STATE_TRANSITION` |
| PATCH | `/api/v1/offers/{offerId}/reactivate` | — | `200 OfferResponse` | `401`, `403`, `404`, `409 INVALID_STATE_TRANSITION` |
| DELETE | `/api/v1/offers/{offerId}` | — | `204 No Content` | `401`, `403`, `404`, `409 ALREADY_DELETED` |

Reglas:

- `CreateProductWithOfferRequest` incluye `name`, `description`, `categoryId`, `imageMediaAssetId` y `offer { price, stock, conditionNotes? }`; se crea en un solo flujo (RF-01). Si el producto ya existe por nombre normalizado + categoría, responde `409` con `details[].productId` (RF-02).
- `sellerId`, `sellerBusinessName` y `pickupPoint` se obtienen del perfil autenticado vía `ISellerDirectory`; se ignoran si vienen en el body (RF-04).
- `UpdateOfferRequest`: `price?`, `stock?`, `conditionNotes?`, `expectedVersion`; actualización atómica con filtro `{ _id, sellerId, version: expectedVersion }` y `$inc: { version: 1 }` (RNF-05, RNF-08). Si no coincide ⇒ `409 CONCURRENCY_CONFLICT`.
- `pause`: `Active → Inactive`. `reactivate`: dueño `Inactive → Active`; Admin `Suspended → Active`; solo si el producto está `Active` y la oferta no está `Deleted`.
- `DELETE`: `Deleted` lógico; no borra el documento (RF-07).

### 4.4 Endpoint administrativo

| Método | Ruta | Request | Éxito | Errores |
|---|---|---|---|---|
| PATCH | `/api/v1/offers/{offerId}/suspend` | — | `200 OfferResponse` | `401`, `403`, `404`, `409 INVALID_STATE_TRANSITION` |

Suspend: `Active|Inactive → Suspended`, registra `suspendedAt`/`suspendedBy` y auditoría (RF-08). Reactivar lo cubre `PATCH /offers/{id}/reactivate` con rol Admin.

### 4.5 DTOs (forma)

```csharp
public sealed record CreateProductWithOfferRequest(
    string Name, string Description, string CategoryId, string ImageMediaAssetId,
    CreateOfferRequest Offer);

public sealed record CreateOfferRequest(decimal Price, int Stock, string? ConditionNotes);
public sealed record UpdateOfferRequest(decimal? Price, int? Stock, string? ConditionNotes, int ExpectedVersion);
public sealed record ProductQuery(string? Search, string? CategoryId, decimal? MinPrice, decimal? MaxPrice,
    string? Sort, int Page = 1, int PageSize = 20);
public sealed record CategoryQuery(int Page = 1, int PageSize = 20);

public sealed record ProductSummaryResponse(string Id, string Name, string Description, string CategoryId,
    string CategoryName, string ImageMediaAssetId, decimal? LowestPrice, int AvailableOffers);
public sealed record ProductDetailResponse(string Id, string Name, string Description, string CategoryId,
    string CategoryName, string ImageMediaAssetId, string Status, DateTime CreatedAt);
public sealed record OfferResponse(string Id, string ProductId, string SellerId, string SellerBusinessName,
    string PickupPoint, decimal Price, int Stock, string? ConditionNotes, string Status,
    string Availability, int Version, DateTime UpdatedAt);
public sealed record OfferComparisonResponse(string ProductId, string ProductName,
    IReadOnlyList<OfferResponse> Offers);
public sealed record CategoryResponse(string Id, string Name, int SortOrder);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalItems);
```

### 4.6 Contrato de error

```json
{
  "correlationId": "3f6c2b1e-9a4d-4c1e-8a77-0b2d5f9c1e42",
  "status": 409,
  "code": "PRODUCT_ALREADY_EXISTS",
  "message": "Ya existe un producto con ese nombre en la categoría.",
  "details": [{ "productId": "665f1c2a9b1e4a0012ab34cd" }]
}
```

| HTTP | `code` | Caso |
|---|---|---|
| 400 | `VALIDATION_ERROR` | Formato/rango inválido. |
| 401 | `UNAUTHORIZED` | Token ausente/inválido/revocado. |
| 403 | `FORBIDDEN` / `OFFER_NOT_OWNED` | Rol insuficiente o recurso ajeno. |
| 404 | `PRODUCT_NOT_FOUND` / `OFFER_NOT_FOUND` | Recurso inexistente. |
| 409 | `PRODUCT_ALREADY_EXISTS` | Duplicado normalizado (incluye `productId`). |
| 409 | `OFFER_NOT_AVAILABLE` | Oferta no comprable para la operación. |
| 409 | `INVALID_STATE_TRANSITION` | Pausar/reactivar/suspender inválido. |
| 409 | `CONCURRENCY_CONFLICT` | `expectedVersion` desactualizado. |
| 409 | `INSUFFICIENT_STOCK` | Reserva mayor al stock disponible. |
| 422 | `CATEGORY_NOT_FOUND` | Categoría inexistente/inactiva. |
| 422 | `MEDIA_ASSET_NOT_APPROVED` | Imagen no aprobada o inexistente. |
| 429 | `RATE_LIMITED` | Rate limit público excedido. |
| 503 | `DEPENDENCY_UNAVAILABLE` | MongoDB/Media caídos. |

**Trazabilidad:** RF-01..RF-15; RNF-01, RNF-02, RNF-03, RNF-05, RNF-06, RNF-07, RNF-11.

---

## 5. Caching Redis e Invalidación

### 5.1 Claves y TTL

| Dato | Clave | TTL |
|---|---|---|
| Listado de productos | `cache:catalog:products:v{generation}:{queryHash}` | 60 s |
| Detalle de producto | `cache:catalog:product:{productId}` | 60 s |
| Comparativa de ofertas | `cache:catalog:offers:{productId}` | 60 s |
| Categorías | `cache:catalog:categories:v{generation}:{queryHash}` | 300 s |
| Generación (contador) | `cache:catalog:generation` | Sin TTL (se reinicia si se vacía Redis) |

- `queryHash` = SHA-256 de los parámetros normalizados (`search`, `categoryId`, `minPrice`, `maxPrice`, `sort`, `page`, `pageSize`).
- Serialización JSON (`System.Text.Json`) con DTOs de respuesta; nunca se cachean documentos crudos ni datos sensibles.
- TTL por defecto del catálogo: 60 s (constitución §6.2).

### 5.2 Flujo de lectura (cache-aside)

1. Leer `cache:catalog:generation`; si no existe, usar `1`.
2. Construir clave; intentar `GET`.
3. Hit válido → deserializar y responder.
4. Miss → consultar MongoDB, `SET` con TTL y responder.
5. Error de Redis → **fail-open**: consultar MongoDB, registrar degradación y responder (CL-15, RNF-04).

### 5.3 Invalidación

| Evento | Acciones |
|---|---|
| Crear producto (+ primera oferta) | `INCR cache:catalog:generation`; `DEL cache:catalog:product:{id}` |
| Crear oferta | `DEL cache:catalog:offers:{productId}`, `cache:catalog:product:{productId}`; `INCR generation` |
| Editar oferta (precio/stock/condiciones) | Igual que crear oferta. |
| Pausar/reactivar/suspender/eliminar oferta | Igual que crear oferta. |
| Reservar/liberar stock | `DEL cache:catalog:offers:{productId}`, `cache:catalog:product:{productId}`; `INCR generation` |
| Cambio de semilla de categorías | `INCR generation` |

- El contador de generación evita `KEYS`/`SCAN` costosos: las claves viejas expiran por TTL.
- La escritura responde **después** de intentar la invalidación; si Redis falla, la escritura se completa y se registra alerta, confiando en el TTL de 60 s (CL-16).
- Prohibido cachear ofertas no comprables como si lo fueran: la comparativa se cachea ya filtrada.

### 5.4 Reserva atómica de stock (contrato para `Orders`)

`IStockReservationService`:

| Operación | Firma | Semántica |
|---|---|---|
| Reservar | `ReserveAsync(offerId, quantity, cancellationToken)` | Descuenta stock atómicamente si hay suficiente. |
| Liberar | `ReleaseAsync(offerId, quantity, cancellationToken)` | Devuelve stock reservado. |
| Disponibilidad | `GetAvailabilityAsync(offerId, cancellationToken)` | `Available`/`SoldOut`/estado. |

Implementación:

- `ReserveAsync` usa `FindOneAndUpdate` con filtro `{ _id: offerId, status: Active, stock: { $gte: quantity } }` y update `{ $inc: { stock: -quantity, version: 1 }, $set: { updatedAt: now } }`.
- Si no hay documento: releer para distinguir `OFFER_NOT_AVAILABLE` (estado) de `INSUFFICIENT_STOCK` (stock) y responder `409`.
- `ReleaseAsync` usa `{ _id: offerId }` y `{ $inc: { stock: quantity, version: 1 } }`; es seguro aunque la oferta esté `Inactive`/`Suspended`/`Deleted`.
- La idempotencia de liberación la garantiza `Orders` (una orden solo puede rechazarse/cancelarse una vez); este módulo expone primitivas atómicas y no crea un ledger adicional.
- No se usa transacción multi-documento aquí: la atomicidad es de un solo documento. Si `Orders` requiere reservar y crear pedido juntos, usará transacción en Atlas (fuera de este módulo).
- Tras reservar/liberar se invalida la caché de §5.3.
- `quantity` se valida `> 0`; el precio no interviene en la reserva (BR-010 se calcula en `Orders`).

**Trazabilidad:** RF-10, RF-11, RF-12, RF-15; RNF-01, RNF-04, RNF-10; CL-09, CL-10, CL-11, CL-15, CL-16, CL-17.

---

## 6. Estrategia de Pruebas

Proyectos: `UtttGo.UnitTests`, `UtttGo.IntegrationTests`, `UtttGo.SecurityTests`.

### 6.1 Unitarias

- `ProductNameNormalizer`: mayúsculas, acentos, espacios múltiples, signos y cadena vacía (RF-02).
- Validadores: `price > 0`, `stock >= 0`, longitudes, `categoryId` válido (RNF-05).
- `Offer`/`OfferAvailability`: matriz de estados y stock (RF-03, RF-11).
- Propiedad de oferta (`sellerId` del contexto vs oferta) (RF-05, RNF-03).
- Transiciones `pause`/`reactivate`/`suspend`/`delete` (RF-06, RF-07, RF-08, CL-12, CL-13).
- `CatalogCacheKeys`: hash determinista y claves por recurso (RF-15).
- `StockReservationService` con repositorio mockeado: filtro atómico, distinción de errores (RF-12).

### 6.2 Integración (Testcontainers + Azurite)

- MongoDB 7 y Redis 7 reales por corrida; Azurite para el borde de imagen/SAS.
- `FakeMediaAssetValidator` para la mayoría de casos; una prueba con Azurite verifica que solo un `mediaAssetId` aprobado es aceptado (RF-01, RF-13, RNF-11).
- Casos: creación producto+oferta, duplicado normalizado, catálogo con búsqueda/filtros/orden/paginación, comparativa ordenada, edición/pausa/reactivación/baja, suspensión admin, caché hit/miss/invalidación, reserva concurrente de la última unidad (T-CAT-01, 02, 05, 06, 07, 08, 09, 10).

### 6.3 Seguridad

- JWT válido/inválido/expirado/revocado en rutas de escritura (RNF-02).
- `Seller` activo vs suspendido; `Admin` vs no-admin (RF-08, RNF-02).
- `sellerId` inyectado en body se ignora (RF-04).
- Vendedor edita/pausa/elimina oferta ajena ⇒ `403` (RF-05, RNF-03).
- Rate limit público ⇒ `429` (RNF-01, CL-17).
- Revisión estática de ausencia de carrito/menú/horario (RNF-14).

### 6.4 Concurrencia y caché

- `ReserveAsync` en paralelo sobre la última unidad: exactamente una reserva exitosa y el resto `INSUFFICIENT_STOCK` (RNF-10, CL-10).
- Liberación devuelve stock y la oferta vuelve a ser comprable si corresponde (CL-11).
- Redis caído en lectura ⇒ respuesta desde MongoDB (fail-open); Redis caído en invalidación ⇒ escritura exitosa + alerta (CL-15, CL-16).

**Trazabilidad:** T-CAT-01..T-CAT-10; RNF-01..RNF-14; CL-01..CL-18.

---

## 7. Fases de Implementación (orden sugerido)

| Fase | Contenido | Depende de |
|---|---|---|
| F1 | Esqueleto `CatalogAndOffers` + DI + registro en `Program.cs` | Fase 00 |
| F2 | POCOs, normalizador, índices y repositorios | F1 |
| F3 | DTOs + validadores | F2 |
| F4 | `ProductService`, `OfferService`, `CategoryService`, `AdminOfferService` | F2, F3 |
| F5 | `CatalogQueryService` + caché Redis e invalidación | F4 |
| F6 | `StockReservationService` atómico + contrato para Orders | F4 |
| F7 | Endpoints `/products/*` y `/offers/*` + autorización | F4, F5 |
| F8 | Tests unitarios, integración y seguridad | F5..F7 |
| F9 | Trazabilidad, DoD y cierre de fase | F8 |

Sin código de producción hasta ejecutar `task.md`.

---

## 8. Riesgos y Decisiones Abiertas

| Riesgo / decisión | Mitigación / estado |
|---|---|
| Cambio de prefijos `/catalog`→`/products` y `/seller`→`/offers` | Registrado en spec v1.1; plan y endpoints alineados. |
| Varias ofertas del mismo vendedor para un producto | El spec no lo prohíbe; no se aplica índice único. Si se decide limitar, actualizar spec antes. |
| `sellerBusinessName`/`pickupPoint` denormalizados | Snapshot refrescado en cada escritura de oferta; si cambia el perfil, puede quedar desactualizado hasta la próxima edición. |
| Edición de stock del vendedor vs reservas concurrentes | `expectedVersion` + `$inc version` evitan escrituras perdidas; conflicto ⇒ `409`. |
| Cache stampede en claves calientes | TTL corto y generación; opcional lock `SETNX` si se observa contención. |
| Redis vaciado reinicia `generation` a 1 | Las claves antiguas ya no existen; correcto. |
| `decimal` ↔ `Decimal128` | Pruebas de serialización y redondeo a 2 decimales MXN. |
| Dependencia de `Media` para publicar | `IMediaAssetValidator` desacoplado; sin aprobación ⇒ `422` (fail-closed). |
| Baja lógica de oferta con pedidos históricos | No se borra el documento; `Orders` conserva `offerId`. |

---

## 9. Fuera de Alcance del Plan

- Código de producción, Terraform, pipelines y despliegue.
- Ciclo de vida de pedidos, cálculo de total y máquina de estados (`Orders`).
- CRUD de categorías por API; solo semilla de lectura.
- Galería multiimagen; una imagen principal.
- Reseñas, calificaciones y favoritos.
- Carrito, menús, horarios y pasarela de pago.
- Edición del producto canónico tras su creación.
- Moderación de texto y notificaciones de catálogo.
- Admin modera productos (solo ofertas).

---

## 10. Matriz de Trazabilidad

### 10.1 Por sección

| Sección del plan | RF | RNF | CL | Tests |
|---|---|---|---|---|
| § 2 Estructura del módulo | RF-01..RF-15 | RNF-02, RNF-03 | — | — |
| § 3 Modelo NoSQL | RF-02, RF-03, RF-09, RF-10, RF-14 | RNF-05, RNF-09, RNF-12 | CL-01, CL-02, CL-09 | T-CAT-02, 03, 08 |
| § 4 Contratos HTTP | RF-01..RF-15 | RNF-01, RNF-02, RNF-03, RNF-05, RNF-06, RNF-07, RNF-11 | CL-01..CL-18 | T-CAT-01..T-CAT-08 |
| § 5 Caché y reserva | RF-10, RF-11, RF-12, RF-15 | RNF-01, RNF-04, RNF-10 | CL-09, CL-10, CL-11, CL-15, CL-16, CL-17 | T-CAT-08, 09, 10 |
| § 6 Pruebas | RF-01..RF-15 | RNF-01..RNF-14 | CL-01..CL-18 | T-CAT-01..T-CAT-10 |

### 10.2 Por requisito funcional

| RF | Secciones del plan | Pruebas |
|---|---|---|
| RF-01 | §2, §3.1, §4.3, §6.2 | T-CAT-01 |
| RF-02 | §3.1, §3.4, §4.3, §6.1 | T-CAT-02 |
| RF-03 | §3.1, §3.2, §4.5, §6.1 | T-CAT-03 |
| RF-04 | §2, §4.3, §6.3 | T-CAT-04 |
| RF-05 | §4.3, §6.1, §6.3 | T-CAT-04, T-CAT-05 |
| RF-06 | §3.2, §4.3, §6.1 | T-CAT-05 |
| RF-07 | §3.2, §4.3, §6.1 | T-CAT-05 |
| RF-08 | §4.4, §6.1, §6.3 | T-CAT-06 |
| RF-09 | §3.4, §4.2, §6.2 | T-CAT-07 |
| RF-10 | §3.2, §4.2, §5.3, §6.2 | T-CAT-08 |
| RF-11 | §3.2, §4.2, §5.4, §6.1 | T-CAT-08 |
| RF-12 | §5.4, §6.4 | T-CAT-09 |
| RF-13 | §2, §3.1, §4.3, §6.2 | T-CAT-01 |
| RF-14 | §3.3, §3.4, §4.2 | T-CAT-07 |
| RF-15 | §5.1..§5.3, §6.4 | T-CAT-10 |

Cada RF del spec queda cubierto por al menos una sección y una prueba.

---

## 11. DoD del Plan

- [ ] Estructura de §2 creada y compilando.
- [ ] POCOs, normalizador e índices de §3 implementados; `EnsureIndexes` idempotente.
- [ ] Contratos de §4 con DTOs, validadores y contrato de error único.
- [ ] Caché `cache:catalog:*` con TTL 60 s, generación e invalidación en escritura.
- [ ] Reserva/liberación atómica de stock expuesta como contrato para `Orders`.
- [ ] Rate limit público y autorización por recurso verificados.
- [ ] T-CAT-01..T-CAT-10 en verde.
- [ ] Build Release y `dotnet format` en verde.
- [ ] Sin secretos en el diff.
- [ ] Trazabilidad y `memoria.md` actualizados.

---

## 12. Aprobación y Control de Cambios

| Campo | Valor |
|---|---|
| Estado | **Aprobado** |
| Fecha | 2026-10-29 |
| Versión | 1.0 |
| Autor | AGENT-BE-01 (Architect) con AGENT-BE-02/BE-03 |
| Revisores | AGENT-BE-04 (Security Reviewer), AGENT-BE-06 (Memory Keeper) |
| Aprobado por | Equipo UTTT-GO |
| Próxima revisión | Al crear `task.md` o al cambiar reglas de stock/caché |

### 12.1 Registro de versiones

| Versión | Fecha | Cambio | Aprobado por |
|---|---|---|---|
| 1.0 | 2026-10-29 | Plan de estructura, modelos, contratos, caché/invalidación, reserva atómica, pruebas y trazabilidad RF-01..RF-15 | Equipo UTTT-GO |

### 12.2 Reglas de control de cambios

1. Cambiar colecciones, índices, claves de caché, TTL, rutas o el contrato de reserva exige nueva versión de este plan.
2. Toda desviación que altere requisitos debe reflejarse primero en `spec.md`.
3. Ningún agente implementa código de producción sin `task.md` derivado de este plan.
