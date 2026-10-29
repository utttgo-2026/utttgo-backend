# task.md — Tareas atómicas: Catálogo y Ofertas Comparables · Backend UTTT-GO
> Fase: `phase-03-catalog-offers` · Módulo: `CatalogAndOffers`
> Versión: 1.0 · Estado: **Aprobado** · Fecha: 2026-10-29
> Deriva de `specs/phase-03-catalog-offers/spec.md` v1.1 y `specs/phase-03-catalog-offers/plan.md` v1.0.
> Cada tarea está dimensionada a **20–30 minutos**. No se escribe código de producción fuera de estas tareas.

---

## Convenciones

- Formato: `- [ ] TASK-CATALOG-xxx — Título`.
- `RF:` requisitos de `spec.md` cubiertos.
- `Hecho cuando:` condición única, explícita y verificable (comando, prueba o archivo).
- Una tarea no se marca hasta cumplir su "Hecho cuando" y dejar build/tests en verde.
- Respetar el orden numérico salvo dependencia indicada. Las pruebas de seguridad pueden paralelizarse tras TASK-CATALOG-048.

---

## Bloque 0 — Preparación

- [ ] **TASK-CATALOG-001 — Crear estructura del módulo `CatalogAndOffers`**
  - RF: RF-01, RF-02, RF-03, RF-04, RF-05, RF-06, RF-07, RF-08, RF-09, RF-10, RF-11, RF-12, RF-13, RF-14, RF-15
  - Depende de: fase 00 (esqueleto base).
  - Hecho cuando: existen `Endpoints`, `Application`, `Domain`, `Infrastructure`, `Contracts`, `Validators` bajo `src/UtttGo.Api/Modules/CatalogAndOffers/` con `DependencyInjection.cs` registrado en `Program.cs`, y `dotnet build -c Release` compila sin errores.

- [ ] **TASK-CATALOG-002 — Definir opciones tipadas del módulo**
  - RF: RF-09, RF-15
  - Depende de: TASK-CATALOG-001.
  - Hecho cuando: existen `CatalogOptions` (`CacheTtlSeconds = 60`, `CategoryCacheTtlSeconds = 300`, `DefaultPageSize`, `MaxPageSize`) y `StockReservationOptions` enlazadas desde configuración, con validación al arranque que falla si `MaxPageSize < DefaultPageSize`.

---

## Bloque 1 — Dominio, modelos MongoDB, DTOs y FluentValidation

- [ ] **TASK-CATALOG-003 — Crear enums de dominio**
  - RF: RF-03, RF-11
  - Hecho cuando: `ProductStatus` (`Active`, `Inactive`), `OfferStatus` (`Active`, `Inactive`, `Suspended`, `Deleted`) y `OfferAvailability` (`Available`, `SoldOut`, `Inactive`, `Suspended`, `Deleted`) compilan y una prueba unitaria fija sus valores numéricos.

- [ ] **TASK-CATALOG-004 — Implementar POCO `Product` y class map**
  - RF: RF-03
  - Hecho cuando: `Product` contiene exactamente los campos de `plan.md` §3.1 (sin `price`, `stock` ni `sellerId`) y una prueba de serialización BSON conserva `_id`, `categoryId`, `status` y fechas UTC.

- [ ] **TASK-CATALOG-005 — Implementar POCO `Offer` y class map**
  - RF: RF-03, RF-11
  - Hecho cuando: `Offer` contiene los campos de `plan.md` §3.2 y `Price` se serializa como `Decimal128`; una prueba de round-trip BSON conserva `price`, `stock`, `status` y `version`.

- [ ] **TASK-CATALOG-006 — Implementar POCO `Category`**
  - RF: RF-14
  - Hecho cuando: `Category` contiene `name`, `normalizedName`, `sortOrder` e `isActive`, y una prueba de serialización BSON redondea el documento sin pérdida.

- [ ] **TASK-CATALOG-007 — Implementar `ProductNameNormalizer`**
  - RF: RF-02
  - Hecho cuando: normaliza mayúsculas, acentos, signos y espacios múltiples a la forma de `plan.md` §3.4, y las pruebas cubren al menos 8 casos (incluida cadena vacía).

- [ ] **TASK-CATALOG-008 — Declarar índices idempotentes**
  - RF: RF-02, RF-09, RF-10, RF-14
  - Depende de: TASK-CATALOG-004, TASK-CATALOG-005, TASK-CATALOG-006.
  - Hecho cuando: `MongoIndexes.EnsureIndexesAsync` crea los índices de `plan.md` §3.4 (único `normalizedName+categoryId`, compuestos de catálogo/ofertas/categorías) y ejecutarlo dos veces no lanza error.

- [ ] **TASK-CATALOG-009 — Definir DTOs de request y response**
  - RF: RF-01, RF-02, RF-03, RF-04, RF-05, RF-06, RF-07, RF-08, RF-09, RF-10, RF-11, RF-12, RF-13, RF-14, RF-15
  - Depende de: TASK-CATALOG-003.
  - Hecho cuando: existen los registros de `plan.md` §4.5 (`CreateProductWithOfferRequest`, `CreateOfferRequest`, `UpdateOfferRequest`, `ProductQuery`, `CategoryQuery`, `ProductSummaryResponse`, `ProductDetailResponse`, `OfferResponse`, `OfferComparisonResponse`, `CategoryResponse`, `PagedResponse<T>`) y `dotnet build -c Release` compila.

- [ ] **TASK-CATALOG-010 — Centralizar códigos de error del módulo**
  - RF: RF-01, RF-02, RF-03, RF-04, RF-05, RF-06, RF-07, RF-08, RF-09, RF-10, RF-11, RF-12, RF-13, RF-14, RF-15
  - Depende de: TASK-CATALOG-009.
  - Hecho cuando: `CatalogErrorCodes` expone los códigos de `plan.md` §4.6 y una prueba unitaria verifica que cada código mapea a su `HttpStatusCode` esperado.

- [ ] **TASK-CATALOG-011 — Implementar `CreateProductWithOfferRequestValidator`**
  - RF: RF-01, RF-02, RF-13, RF-14
  - Depende de: TASK-CATALOG-009.
  - Hecho cuando: exige `name`, `description`, `categoryId`, `imageMediaAssetId` y `offer` válidos, y aplica longitudes y rangos de `price > 0` / `stock >= 0`; pruebas unitarias en verde.

- [ ] **TASK-CATALOG-012 — Implementar `CreateOfferRequestValidator`**
  - RF: RF-04, RF-05
  - Depende de: TASK-CATALOG-009.
  - Hecho cuando: exige `price > 0`, `stock >= 0`, `conditionNotes` dentro de longitud y rechaza `sellerId` si se envía; pruebas unitarias en verde.

- [ ] **TASK-CATALOG-013 — Implementar `UpdateOfferRequestValidator`**
  - RF: RF-05
  - Depende de: TASK-CATALOG-009.
  - Hecho cuando: exige `expectedVersion >= 1`, valida campos opcionales solo si vienen y rechaza `price <= 0`/`stock < 0`; pruebas unitarias en verde.

- [ ] **TASK-CATALOG-014 — Implementar `ProductQueryValidator` y `CategoryQueryValidator`**
  - RF: RF-09, RF-14
  - Depende de: TASK-CATALOG-009.
  - Hecho cuando: validan `page >= 1`, `pageSize` dentro del máximo, `sort` en el conjunto permitido, `minPrice <= maxPrice` y `categoryId` con formato ObjectId; pruebas unitarias en verde.

---

## Bloque 2 — Repositorios y Servicios de Aplicación

- [ ] **TASK-CATALOG-015 — Implementar `IProductRepository` y `MongoProductRepository`**
  - RF: RF-01, RF-02, RF-09
  - Depende de: TASK-CATALOG-008.
  - Hecho cuando: existen `CreateAsync`, `GetByIdAsync`, `ExistsByNormalizedNameAndCategoryAsync`, `QueryPagedAsync` y `UpdateStatusAsync`; el intento de duplicado por índice único lanza la excepción de conflicto controlada y la prueba de integración lo verifica.

- [ ] **TASK-CATALOG-016 — Implementar `IOfferRepository` y `MongoOfferRepository` (lectura/creación)**
  - RF: RF-04, RF-10, RF-11
  - Depende de: TASK-CATALOG-008.
  - Hecho cuando: existen `CreateAsync`, `GetByIdAsync`, `GetByProductIdAsync` y `ListAvailableByProductAsync(productId)` que filtra `Status = Active && Stock > 0` y ordena por precio; pruebas de integración en verde.

- [ ] **TASK-CATALOG-017 — Extender `IOfferRepository` (mutaciones y reserva atómica)**
  - RF: RF-05, RF-06, RF-07, RF-08, RF-12
  - Depende de: TASK-CATALOG-016.
  - Hecho cuando: existen `UpdateWithVersionAsync`, `UpdateStatusAsync`, `ReserveStockAsync` (`$inc` con filtro `stock >= qty`) y `ReleaseStockAsync`; pruebas de repositorio contra Testcontainers en verde.

- [ ] **TASK-CATALOG-018 — Implementar `ICategoryRepository` y `MongoCategoryRepository`**
  - RF: RF-14
  - Depende de: TASK-CATALOG-008.
  - Hecho cuando: existen `GetActiveByIdAsync` y `QueryPagedActiveAsync`, sin métodos de escritura; pruebas de integración en verde.

- [ ] **TASK-CATALOG-019 — Implementar `ISellerDirectory` y su adaptador**
  - RF: RF-04
  - Depende de: TASK-CATALOG-001.
  - Hecho cuando: `GetAsync(sellerId)` devuelve `BusinessName` y `PickupPoint` del perfil de vendedor vía contrato de `IdentityAndUsers`, y una prueba con fake verifica el snapshot usado al crear/editar oferta.

- [ ] **TASK-CATALOG-020 — Implementar `IMediaAssetValidator` y su adaptador**
  - RF: RF-01, RF-13
  - Depende de: TASK-CATALOG-001.
  - Hecho cuando: `EnsureApprovedAsync(mediaAssetId)` lanza `MEDIA_ASSET_NOT_APPROVED` si el asset no está `Approved`; pruebas unitarias con fake en verde.

- [ ] **TASK-CATALOG-021 — Implementar `ProductService.CreateWithFirstOfferAsync`**
  - RF: RF-01, RF-02, RF-03, RF-04, RF-13, RF-14
  - Depende de: TASK-CATALOG-007, TASK-CATALOG-011, TASK-CATALOG-015, TASK-CATALOG-017, TASK-CATALOG-018, TASK-CATALOG-019, TASK-CATALOG-020.
  - Hecho cuando: normaliza el nombre, valida categoría activa e imagen aprobada, deriva el vendedor del contexto, crea producto + primera oferta y devuelve `409 PRODUCT_ALREADY_EXISTS` con `productId` si existe; pruebas unitarias en verde.

- [ ] **TASK-CATALOG-022 — Implementar `OfferService.CreateAsync`**
  - RF: RF-04
  - Depende de: TASK-CATALOG-012, TASK-CATALOG-016, TASK-CATALOG-019.
  - Hecho cuando: crea la oferta sobre un producto `Active`, toma `sellerId`, `BusinessName` y `PickupPoint` del contexto/directorio, ignora `sellerId` del body y responde `404 PRODUCT_NOT_FOUND` si no existe; pruebas unitarias en verde.

- [ ] **TASK-CATALOG-023 — Implementar `OfferService.UpdateAsync`**
  - RF: RF-05
  - Depende de: TASK-CATALOG-013, TASK-CATALOG-017.
  - Hecho cuando: valida propiedad, aplica `expectedVersion` con `$inc version` y devuelve `409 CONCURRENCY_CONFLICT` si no coincide; pruebas unitarias de éxito, recurso ajeno y versión desactualizada en verde.

- [ ] **TASK-CATALOG-024 — Implementar pausa, reactivación y baja lógica**
  - RF: RF-06, RF-07
  - Depende de: TASK-CATALOG-017, TASK-CATALOG-021.
  - Hecho cuando: `pause` pasa `Active→Inactive`, `reactivate` pasa `Inactive→Active` solo si el producto está `Active`, `delete` marca `Deleted` sin borrar el documento, y las transiciones inválidas responden `409`; pruebas unitarias en verde.

- [ ] **TASK-CATALOG-025 — Implementar `AdminOfferService` (suspender/reactivar) + auditoría**
  - RF: RF-08
  - Depende de: TASK-CATALOG-017.
  - Hecho cuando: suspender marca `Suspended` con `suspendedAt`/`suspendedBy`, reactivar `Suspended→Active` solo si el producto está `Active` y la oferta no está `Deleted`, y se registra auditoría con actor, acción, timestamp y `correlationId`.

- [ ] **TASK-CATALOG-026 — Implementar `CatalogQueryService.ListProductsAsync`**
  - RF: RF-09
  - Depende de: TASK-CATALOG-015, TASK-CATALOG-018.
  - Hecho cuando: aplica `search` por prefijo/contiene sobre nombre normalizado, `categoryId`, rango de precio sobre ofertas disponibles, orden permitido y paginación con `totalItems`; pruebas unitarias en verde.

- [ ] **TASK-CATALOG-027 — Implementar `CatalogQueryService` (detalle y comparativa)**
  - RF: RF-10, RF-11
  - Depende de: TASK-CATALOG-016, TASK-CATALOG-026.
  - Hecho cuando: el detalle resuelve categoría y `lowestPrice`; la comparativa devuelve solo `Available`, ordenada por precio ascendente, con vendedor, precio y stock, y `SoldOut` queda excluida; pruebas unitarias en verde.

- [ ] **TASK-CATALOG-028 — Implementar `CategoryService.ListAsync`**
  - RF: RF-14
  - Depende de: TASK-CATALOG-018.
  - Hecho cuando: lista solo categorías activas, ordenadas por `sortOrder`, con paginación; pruebas unitarias en verde.

---

## Bloque 3 — Caché Redis e invalidación

- [ ] **TASK-CATALOG-029 — Implementar `CatalogCacheKeys` y hash de consulta**
  - RF: RF-15
  - Depende de: TASK-CATALOG-009.
  - Hecho cuando: genera claves `cache:catalog:products:v{n}:{hash}`, `cache:catalog:product:{id}`, `cache:catalog:offers:{id}` y `cache:catalog:categories:v{n}:{hash}`, con hash SHA-256 determinista de parámetros normalizados; pruebas unitarias en verde.

- [ ] **TASK-CATALOG-030 — Implementar `RedisCatalogCache` (get/set/generación, fail-open)**
  - RF: RF-15
  - Depende de: TASK-CATALOG-029.
  - Hecho cuando: `GetOrCreateAsync` respeta TTL de 60 s (300 s categorías), la generación se lee con default `1`, y un fallo de Redis devuelve miss/consulta MongoDB sin lanzar; pruebas de integración con Redis Testcontainer en verde.

- [ ] **TASK-CATALOG-031 — Implementar `CatalogCacheInvalidator`**
  - RF: RF-15
  - Depende de: TASK-CATALOG-030.
  - Hecho cuando: expone `OnProductChanged`, `OnOfferChanged` y `OnCategoriesChanged`, hace `DEL` de claves puntuales e `INCR cache:catalog:generation`, y el fallo de Redis solo registra alerta sin romper la escritura; pruebas unitarias con fake en verde.

- [ ] **TASK-CATALOG-032 — Integrar cache-aside en las consultas**
  - RF: RF-09, RF-10, RF-15
  - Depende de: TASK-CATALOG-027, TASK-CATALOG-030.
  - Hecho cuando: listado, detalle, comparativa y categorías consultan caché antes de MongoDB y escriben con TTL; una prueba de integración verifica hit/miss y contenido servido desde caché.

- [ ] **TASK-CATALOG-033 — Implementar `StockReservationService.ReserveAsync`**
  - RF: RF-12
  - Depende de: TASK-CATALOG-017.
  - Hecho cuando: descuenta stock atómicamente con filtro `status = Active && stock >= quantity`, distingue `INSUFFICIENT_STOCK` de `OFFER_NOT_AVAILABLE`, valida `quantity > 0` y una prueba concurrente sobre la última unidad deja exactamente una reserva exitosa.

- [ ] **TASK-CATALOG-034 — Implementar `ReleaseAsync` y `GetAvailabilityAsync`**
  - RF: RF-11, RF-12
  - Depende de: TASK-CATALOG-033.
  - Hecho cuando: liberar incrementa stock y `version`, funciona aunque la oferta esté `Inactive`/`Suspended`/`Deleted`, la disponibilidad refleja la matriz de `plan.md` §3.2 y las pruebas unitarias/integración están en verde.

- [ ] **TASK-CATALOG-035 — Invalidar caché tras reservar/liberar stock**
  - RF: RF-12, RF-15
  - Depende de: TASK-CATALOG-031, TASK-CATALOG-034.
  - Hecho cuando: `ReserveAsync` y `ReleaseAsync` invalidan `offers:{productId}` y `product:{productId}` e incrementan la generación, y una prueba de integración confirma que la comparativa deja de servir stock viejo.

---

## Bloque 4 — Endpoints HTTP y autorización

- [ ] **TASK-CATALOG-036 — Configurar políticas de autorización y rate limit público**
  - RF: RF-08, RF-09
  - Depende de: TASK-CATALOG-001, fase 02 (JWT/roles).
  - Hecho cuando: existen políticas `SellerPolicy` y `AdminPolicy` basadas en el rol de MongoDB, y las rutas públicas `/api/v1/products*` usan el rate limit distribuido; pruebas de humo `401`/`403`/`429` en verde.

- [ ] **TASK-CATALOG-037 — Implementar endpoints públicos de catálogo**
  - RF: RF-09, RF-10, RF-14
  - Depende de: TASK-CATALOG-026, TASK-CATALOG-027, TASK-CATALOG-028, TASK-CATALOG-036.
  - Hecho cuando: `GET /api/v1/products`, `/products/categories`, `/products/{id}` y `/products/{id}/offers` responden con los DTOs y códigos de `plan.md` §4.2, y `/products/categories` se registra antes de `{id}`; pruebas de integración en verde.

- [ ] **TASK-CATALOG-038 — Implementar `POST /api/v1/products`**
  - RF: RF-01, RF-02, RF-04, RF-13
  - Depende de: TASK-CATALOG-021, TASK-CATALOG-036.
  - Hecho cuando: exige `SellerPolicy`, responde `201` con `ProductDetailResponse`, `409` con `productId` en `details` para duplicado y `422` para categoría/imagen inválida; pruebas de integración en verde.

- [ ] **TASK-CATALOG-039 — Implementar `POST /api/v1/products/{productId}/offers`**
  - RF: RF-04
  - Depende de: TASK-CATALOG-022, TASK-CATALOG-036.
  - Hecho cuando: exige `SellerPolicy`, responde `201 OfferResponse`, `404` si el producto no existe y un `sellerId` en el body no altera el propietario; pruebas de integración en verde.

- [ ] **TASK-CATALOG-040 — Implementar mutaciones de oferta del vendedor**
  - RF: RF-05, RF-06, RF-07
  - Depende de: TASK-CATALOG-023, TASK-CATALOG-024, TASK-CATALOG-036.
  - Hecho cuando: `PATCH /offers/{id}`, `/pause`, `/reactivate` y `DELETE /offers/{id}` responden según `plan.md` §4.3 y rechazan ofertas ajenas con `403 OFFER_NOT_OWNED`; pruebas de integración en verde.

- [ ] **TASK-CATALOG-041 — Implementar suspensión administrativa**
  - RF: RF-08
  - Depende de: TASK-CATALOG-025, TASK-CATALOG-036.
  - Hecho cuando: `PATCH /offers/{id}/suspend` exige `AdminPolicy`, responde `200`/`404`/`409` según `plan.md` §4.4 y la reactivación de una oferta suspendida funciona solo para Admin; pruebas de integración en verde.

- [ ] **TASK-CATALOG-042 — Aplicar autorización por recurso en ofertas**
  - RF: RF-05
  - Depende de: TASK-CATALOG-040.
  - Hecho cuando: la verificación de propiedad compara el `sellerId` del contexto con `Offer.SellerId` antes de cualquier mutación y existe prueba de seguridad que impide editar/pausar/eliminar ofertas ajenas.

- [ ] **TASK-CATALOG-043 — Homologar el contrato único de error en el módulo**
  - RF: RF-01, RF-02, RF-03, RF-04, RF-05, RF-06, RF-07, RF-08, RF-09, RF-10, RF-11, RF-12, RF-13, RF-14, RF-15
  - Depende de: TASK-CATALOG-010, TASK-CATALOG-037..041.
  - Hecho cuando: todos los errores del módulo incluyen `correlationId`, `status`, `code`, `message` y `details?`, y una prueba de contrato recorre al menos un error de cada endpoint.

---

## Bloque 5 — Pruebas Unitarias, de Integración y de Seguridad

- [ ] **TASK-CATALOG-044 — Pruebas unitarias de normalizador y validadores**
  - RF: RF-01, RF-02, RF-05, RF-09, RF-14
  - Depende de: TASK-CATALOG-007, TASK-CATALOG-011..014.
  - Hecho cuando: cubren normalización, duplicado lógico, rangos de precio/stock, `expectedVersion`, paginación y `sort`; el proyecto unitario queda en verde.

- [ ] **TASK-CATALOG-045 — Pruebas unitarias de disponibilidad y estados**
  - RF: RF-03, RF-06, RF-07, RF-08, RF-11
  - Depende de: TASK-CATALOG-003, TASK-CATALOG-024, TASK-CATALOG-025.
  - Hecho cuando: cubren la matriz `Active/Inactive/Suspended/Deleted` × stock 0/>0, transiciones válidas e inválidas y que `SoldOut` no es comprable; proyecto unitario en verde.

- [ ] **TASK-CATALOG-046 — Pruebas unitarias de servicios**
  - RF: RF-01, RF-02, RF-04, RF-05, RF-09, RF-10
  - Depende de: TASK-CATALOG-021..028.
  - Hecho cuando: cubren creación producto+oferta, duplicado con `productId`, vendedor derivado del contexto, conflicto de versión, filtros/orden de catálogo y comparativa solo con disponibles; proyecto unitario en verde.

- [ ] **TASK-CATALOG-047 — Pruebas unitarias de claves e invalidación de caché**
  - RF: RF-15
  - Depende de: TASK-CATALOG-029, TASK-CATALOG-031.
  - Hecho cuando: el hash es determinista e independiente del orden de parámetros, las claves siguen el namespace `cache:catalog:*` y cada evento invalida las claves esperadas; proyecto unitario en verde.

- [ ] **TASK-CATALOG-048 — Montar harness de integración (Testcontainers/Azurite)**
  - RF: RF-01, RF-02, RF-03, RF-04, RF-05, RF-06, RF-07, RF-08, RF-09, RF-10, RF-11, RF-12, RF-13, RF-14, RF-15
  - Depende de: TASK-CATALOG-037..041.
  - Hecho cuando: el harness levanta MongoDB 7, Redis 7 y Azurite, siembra categorías y vendedores, inyecta fakes de `ISellerDirectory`/`IMediaAssetValidator` donde aplique y limpia `cache:catalog:*` entre pruebas.

- [ ] **TASK-CATALOG-049 — Pruebas de integración de creación y duplicados**
  - RF: RF-01, RF-02, RF-13, RF-14
  - Depende de: TASK-CATALOG-048.
  - Hecho cuando: cubren creación producto+oferta, duplicado normalizado con `productId`, categoría inexistente, imagen no aprobada y producto sin `price`/`sellerId`; pruebas en verde (T-CAT-01, T-CAT-02).

- [ ] **TASK-CATALOG-050 — Pruebas de integración de catálogo**
  - RF: RF-09
  - Depende de: TASK-CATALOG-048.
  - Hecho cuando: cubren búsqueda, filtro por categoría, rango de precio, los cuatro ordenamientos, paginación y rate limit público; pruebas en verde (T-CAT-07).

- [ ] **TASK-CATALOG-051 — Pruebas de integración de comparativa**
  - RF: RF-10, RF-11
  - Depende de: TASK-CATALOG-048.
  - Hecho cuando: la comparativa solo devuelve ofertas `Available`, ordenadas por precio ascendente, excluye `SoldOut`/`Inactive`/`Suspended`/`Deleted` y un producto sin ofertas responde con lista vacía; pruebas en verde (T-CAT-08).

- [ ] **TASK-CATALOG-052 — Pruebas de integración del ciclo de vida de oferta**
  - RF: RF-05, RF-06, RF-07, RF-08
  - Depende de: TASK-CATALOG-048.
  - Hecho cuando: cubren editar, pausar, reactivar, baja lógica, suspensión admin, reactivación admin y transiciones inválidas; pruebas en verde (T-CAT-05, T-CAT-06).

- [ ] **TASK-CATALOG-053 — Pruebas de integración de caché e invalidación**
  - RF: RF-15
  - Depende de: TASK-CATALOG-048.
  - Hecho cuando: cubren hit/miss, TTL, invalidación tras creación/edición/estado/reserva y fail-open con Redis detenido; pruebas en verde (T-CAT-10).

- [ ] **TASK-CATALOG-054 — Pruebas de reserva atómica y liberación**
  - RF: RF-12
  - Depende de: TASK-CATALOG-048.
  - Hecho cuando: cubren reserva exitosa, `INSUFFICIENT_STOCK`, `OFFER_NOT_AVAILABLE`, concurrencia sobre la última unidad y liberación que restaura comprabilidad; pruebas en verde (T-CAT-09).

- [ ] **TASK-CATALOG-055 — Pruebas de seguridad de autenticación y autorización**
  - RF: RF-04, RF-05, RF-08, RF-09
  - Depende de: TASK-CATALOG-048.
  - Hecho cuando: cubren `401` sin token, `403` para rol incorrecto, `403 OFFER_NOT_OWNED`, ignorar `sellerId` inyectado y `429` público; pruebas en verde (T-CAT-04).

- [ ] **TASK-CATALOG-056 — Prueba estática de reglas prohibidas**
  - RF: RF-11, RNF-14
  - Depende de: TASK-CATALOG-048.
  - Hecho cuando: una prueba/scan verifica que el módulo no contiene entidades ni campos de carrito, menú, horario, franja o pasarela de pago, y que no hay `float`/`double` para precio.

---

## Bloque 6 — Cierre y trazabilidad

- [ ] **TASK-CATALOG-057 — Actualizar matriz de trazabilidad**
  - RF: RF-01..RF-15
  - Depende de: TASK-CATALOG-049..056.
  - Hecho cuando: `docs/traceability.md` mapea cada RF a endpoint, tarea y prueba, sin RF huérfano ni prueba sin RF.

- [ ] **TASK-CATALOG-058 — Verificación final de calidad**
  - RF: RF-01..RF-15
  - Depende de: todas las anteriores.
  - Hecho cuando: `dotnet build -c Release`, `dotnet format --verify-no-changes` y las suites unitaria, integración y seguridad pasan en verde; no hay secretos en el diff.

- [ ] **TASK-CATALOG-059 — Registrar cierre de la fase en `memoria.md`**
  - RF: RF-01..RF-15
  - Depende de: TASK-CATALOG-058.
  - Hecho cuando: `memoria.md` registra el estado final de la fase, los RF cubiertos y cualquier desviación aprobada.

---

## Orden de ejecución

```
Bloque 0 → Bloque 1 → Bloque 2 → Bloque 3 → Bloque 4 → Bloque 5 → Bloque 6
```

Dentro de cada bloque, respetar el orden numérico. Las pruebas de seguridad (TASK-CATALOG-055 y 056) pueden ejecutarse en paralelo tras TASK-CATALOG-048.

---

## Criterios de cierre de la fase

- [ ] Las 59 tareas marcadas.
- [ ] RF-01 a RF-15 cubiertos por al menos una tarea y una prueba.
- [ ] T-CAT-01 a T-CAT-10 en verde.
- [ ] Build Release y `dotnet format` en verde.
- [ ] `docs/traceability.md` y `memoria.md` actualizados.
- [ ] Sin código de producción fuera de este `task.md` ni secretos en el repositorio.
