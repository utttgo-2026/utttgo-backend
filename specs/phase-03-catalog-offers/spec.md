# spec.md — Catálogo de productos por vendedor — Backend UTTT-GO
> Fase: `phase-03-catalog-offers` — Alcance: `Sellers` (autoactivación y perfil propio) y, en versión posterior, `Catalog` con contratos con Reviews, Media y Orders.
> Versión 2.1 · Estado: **Aprobada para el Sprint 1 (vendedor)** · 2026-11-08.
> Deriva de `docs/constitution.md` v2.5 (DEC-019) y del plan aprobado por el propietario del producto el 2026-11-08. Sustituye v2.0 (borrador) y las versiones v1.x, que quedan como historial y no como autorización vigente. No autoriza rutas sin ficha conciliada.

## 1. Objetivo y decisiones ratificadas

Cada vendedor publica y administra **sus propios productos/alimentos**. El comprador busca y filtra publicaciones independientes de distintos vendedores y compara ingredientes, precios y calificaciones disponibles del producto y del vendedor. **No existen producto maestro compartido ni ofertas hijas de varios vendedores** (BR-004..BR-006). Los ingredientes describen un producto, no un menú ni un horario.

Esta versión **aprueba el Sprint 1**: que un usuario autenticado pueda autoactivarse como vendedor (DEC-002) y administra su propio perfil de negocio, dejando el catálogo de productos (`Catalog`) como alcance de Sprint 2. El Sprint 1 existe porque hoy el backend solo expone identidad: la colección `sellers` no tiene escritor, ningún usuario puede obtener el rol `Seller` y `GET /api/v1/sellers/{sellerId}` responde siempre `404`.

### Decisiones ratificadas por el propietario (2026-11-08)

| ID | Decisión | Resolución ratificada |
|---|---|---|
| DEC-019.a | Identidad del vendedor | `sellers._id` es un `ObjectId` **propio** del vendedor y `sellers.ownerUserId` referencia `users._id` con **índice único**; `users.sellerId` guarda el `sellers._id` para navegación inversa. El `sellerId` que publica la API es el `sellers._id`. No se reutiliza el id de usuario como id de vendedor. |
| DEC-019.b | Nombre de la colección | Se mantiene `sellers` (colección ya leída por el código); se corrige la deriva documental que mencionaba `sellerProfiles` en `memoria.md`. |
| DEC-019.c | API-016 | **No se implementa ni se redirige** `GET /api/v1/products/{productId}/offers`. Su función queda cubierta por la búsqueda/filtros de API-011. El inventario Excel de 42 fichas no se modifica; la retirada formal se registra en `docs/constitution.md` § 16 y en `docs/traceability.md`. |
| DEC-019.d | Ficha nueva de lectura propia | `GET /api/v1/sellers/me` se aprueba como **ficha nueva** (no figura en el Excel de 42 fichas) porque el cliente móvil necesita leer el estado de su propia activación. |
| DEC-019.e | Eliminación de producto | Se mantiene en Sprint 2 como **baja lógica** (`status = "Deleted"`); no se expone `DELETE /api/v1/products/{productId}` sin ficha aprobada, y la eliminación de cuenta (API-005) sigue coordinada con DEC-003. |
| DEC-019.f | Imágenes en esta fase | El Sprint 1 y el Sprint 2 publican **sin imágenes**: `mediaAssetIds` queda vacío y sin contrato. Media (S3 presigned + Azure AI Content Safety, BR-007) se implementa en fase 05 con su propio `spec.md`. No se publica ninguna imagen sin moderación aprobada. |
| DEC-019.g | Alcance del Sprint 1 | Solo autoactivación y perfil de negocio. Sin `products`, sin `categories`, sin carrito, sin total, sin pedidos, sin Reviews, sin Media, sin push y sin chat. |

### Reglas permanentes que este Sprint 1 respeta

- El rol `Seller` se asigna **desde el backend** al perfil local en MongoDB; el cliente nunca envía roles, `sellerId`, precios ni estados (BR-009).
- Autorización por recurso: toda escritura de vendedor se resuelve por la identidad de la sesión validada; **ninguna ruta de escritura acepta un identificador de vendedor** y, por tanto, no existe superficie de edición sobre el vendedor ajeno.
- Rate limiting **distribuido** en Redis y **fail-closed** ante caída de Redis; caché (cuando exista en Sprint 2) **fail-open**.
- Contrato de error único `{ correlationId, status, code, message }`; no se filtran mensajes de excepciones, URIs, tokens ni `jti`.
- Cero recursos de nube nuevos: este Sprint no altera Terraform ni introduce servicios pagados.

## 2. Alcance de esta versión

### 2.1 Incluido y aprobado (Sprint 1)

| Ficha | Método y ruta | Actor | Estado |
|---|---|---|---|
| API-009 | `POST /api/v1/sellers/activation-requests` | Usuario autenticado con cuenta `Active` | **Aprobado (Sprint 1)** |
| API-010 | `PATCH /api/v1/sellers/me` | Vendedor autenticado (propietario implícito) | **Aprobado (Sprint 1)** |
| DEC-019.d | `GET /api/v1/sellers/me` | Usuario autenticado | **Aprobado (Sprint 1, ficha nueva)** |

### 2.2 Diferido (no implementado en esta versión)

`products` y `categories` (API-011..015, API-019..022) — Sprint 2, requiere aprobación del diseño de datos y de la ficha de eliminación (DEC-019.e). Media (API-017/018) — fase 05. Orders (API-023), Reviews, Favorites, Notifications y Chat — fases posteriores con `spec.md` propio.

## 3. Historias y reglas trazables

| Historia | Resultado esperado | Reglas |
|---|---|---|
| HU-CAT-01, 03 | El vendedor crea y actualiza su publicación con ingredientes, precio y existencias. | BR-006/009 — Sprint 2 |
| HU-CAT-04, 05 | El vendedor pausa y elimina sus propias publicaciones; nunca las ajenas. | BR-006/009/011 — Sprint 2 |
| HU-CAT-06, 07, 08, 10 | El comprador busca/filtra alimentos de distintos vendedores y compara datos y calificaciones disponibles. | BR-004/005 — Sprint 2 |
| HU-CAT-09 | Moderación administrativa de publicaciones; no inventar ruta. | BR-007/009 — Sprint 2 |
| HU-AUTH-V01 | Un comprador con cuenta activa completa nombre de negocio, descripción y punto de recogida y queda como vendedor **sin aprobación administrativa** en el MVP. | DEC-002, BR-009, § 12 de la constitución |
| HU-AUTH-V02 | El vendedor consulta y actualiza los datos de su propio negocio y puede corregir un registro creado a medias. | BR-009, BR-011 |

HU-CAT-02 (ofertar sobre producto compartido) y la formulación antigua de HU-CAT-07 **quedan superadas**.

## 4. Requisitos funcionales (EARS)

### 4.1 Sprint 1 (aprobados)

| ID | Requisito |
|---|---|
| RF-V01 | Cuando un usuario autenticado con cuenta `Active` envía nombre de negocio, descripción y punto de recogida válidos a API-009, el backend debe crear su perfil de vendedor, asignarle el rol `Seller` en su perfil local y devolver el `sellerId` generado por el servidor, sin pedir aprobación administrativa. |
| RF-V02 | Cuando API-009 se repite con el mismo usuario, el backend debe devolver el mismo `sellerId` y el mismo perfil de negocio sin duplicar documentos ni roles, y responder `200` en lugar de `201`. |
| RF-V03 | Cuando la promoción del rol `Seller` falla después de crear el registro del vendedor, el backend debe dejar el vendedor en estado no visible y devolver `503`; un reintento posterior completa la activación sin duplicar rol ni perfil. |
| RF-V04 | Cuando el usuario no está autenticado, su token es inválido o su perfil local no existe, API-009 debe responder `401` o `404` sin revelar si existe algún vendedor asociado. |
| RF-V05 | Cuando la cuenta del usuario no está en estado `Active`, API-009 debe responder `403` y no crear ni activar ningún vendedor. |
| RF-V06 | Cuando el usuario no es vendedor, `GET /api/v1/sellers/me` debe responder `404` sin revelar información de otros vendedores. |
| RF-V07 | Cuando el vendedor envía un cambio válido a API-010, el backend debe aplicarlo solo sobre su propio perfil y devolver el perfil actualizado con `version` incrementada. |
| RF-V08 | Cuando dos actualizaciones concurrentes actúan sobre el mismo perfil de vendedor, el backend debe conservar una sola y responder `409` en la petición que pierde, sin sobrescribir silenciosamente datos. |
| RF-V09 | Cuando el cuerpo de API-009 o API-010 contiene campos desconocidos, valores nulos o fuera de rango, el backend debe responder `400` sin escribir nada. |
| RF-V10 | Cuando el rate limit distribuido de las rutas de vendedor se agota, el backend debe responder `429` con `Retry-After`; si Redis no responde, debe responder `503` sin ejecutar la operación (fail-closed). |
| RF-V11 | Mientras no exista el módulo Media, ningún campo de imagen debe poblarse ni inventarse: `avatarUrl` del perfil propio sigue en `null` y el perfil de vendedor no expone imágenes. |
| RF-V12 | El perfil público de API-008 debe seguir exponiendo solo `sellerId`, `displayName`, `ratingAverage`, `ratingCount` e `isActive`, sin datos de contacto, y sin calificaciones inventadas mientras Reviews no exista. |

### 4.2 Sprint 2 (propuestos, no aprobados)

| ID | Requisito | Estado |
|---|---|---|
| RF-01 | Cuando un Seller activo publica, el backend debe crear un producto propio con `sellerId` derivado de su identidad local, nombre, descripción, ingredientes, precio, existencias, categoría activa e imagen aprobada; sin producto maestro ni oferta inicial separada. | Confirmado funcionalmente; contrato API-013 a revisar |
| RF-02 | Cuando vendedores distintos publican alimentos con el mismo nombre/categoría, el sistema debe poder conservar ambas publicaciones; no devolver `409` por duplicado global. | Confirmado para propiedad independiente; detalle pendiente |
| RF-03 | Cada producto debe asociarse a un único vendedor propietario y contener sus datos comerciales; no requiere `canonicalProductId`, `offerId` ni colección `offers`. | Confirmado funcionalmente |
| RF-04 | Cuando el vendedor propietario edita información, ingredientes, precio o existencias, el backend debe validar permisos, formato y rangos; otro vendedor no puede modificar el recurso. | Confirmado funcionalmente |
| RF-05 | Cuando el vendedor propietario pausa su publicación, esta deja de poder comprarse; el estado resultante requiere contrato actualizado, sin traducir automáticamente a `SoldOut`. | Confirmado funcionalmente; ficha pendiente |
| RF-06 | Cuando el vendedor propietario elimina su producto, deja de publicarse y de ser comprable; la política física/lógica sigue **DEC-019.e** (baja lógica). | Confirmado funcionalmente; **ruta sin ficha** |
| RF-07 | Cuando un visitante busca/filtra, debe recibir productos de distintos vendedores con datos del producto y del vendedor, filtrables por parámetros aprobados y paginados. | Confirmado funcionalmente; query/orden pendientes de ficha |
| RF-08 | Cuando se presentan calificaciones, deben proceder de Reviews mediante contrato del módulo dueño; no inventar puntuaciones, votos ni índices. | Confirmado funcionalmente; dependencia de Reviews |
| RF-09 | Solo una publicación activa, en categoría activa y con existencias suficientes puede comprarse; el backend vuelve a comprobarlo en Orders. | Confirmado funcionalmente |
| RF-10 | Cuando se reserva stock para un pedido, no debe haber sobreventa; la liberación ante cancelación/rechazo debe ser idempotente. | Regla confirmada; diseño pendiente |
| RF-11 | Toda imagen publicada debe referenciar un `mediaAssetId` aprobado por Media; moderación fallida bloquea publicación. | Constitucional (fase 05) |
| RF-12 | La gestión administrativa de categorías conserva API-019..022; ocultar categoría no borra ni modifica productos del vendedor. | Decisión previa confirmada |
| RF-13 | La lectura pública puede usar Redis con degradación fail-open para caché, sin relajar rate limit fail-closed. | Constitucional; diseño pendiente |
| RF-14 | Cuando se elimina la cuenta de un vendedor, sus productos/publicaciones y contenido propio deben eliminarse conforme a BR-011. | Confirmado funcionalmente; coordinación pendiente |

### 4.3 Reglas de validación y seguridad (transversales)

- `sellerId`, roles y propiedad se derivan del perfil local y de la sesión validada, nunca del cuerpo de la petición.
- El cliente nunca decide precio, total, estado ni identidad; el backend valida y autoriza por recurso (BR-009).
- Precio `decimal` MXN; existencias enteras `>= 0`; total de pedido = `price × quantity` en servidor (BR-010).
- La unicidad global anterior `{ normalizedName, categoryId }` queda **invalidada**; no se presupone clave de agrupación equivalente.
- La visibilidad de categoría y la disponibilidad del producto se verifican también en backend; la caché no sustituye esas validaciones.
- Las calificaciones del producto/vendedor no se almacenan ni editan por el cliente.

## 5. Contratos HTTP del Sprint 1 (aprobados)

### 5.1 API-009 — `POST /api/v1/sellers/activation-requests`

- **Acceso**: requiere `Authorization: Bearer <jwt>` válido, no expirado y no revocado en Redis, con perfil local en estado `Active`. El token se valida con el mismo gate que API-002/API-004 (firma, issuer, audience, expiración y blacklist Redis).
- **Cuerpo** (los tres campos son obligatorios):

```json
{
  "businessName": "Campus Snacks",
  "description": "Snacks y bebidas preparados en el campus.",
  "pickupPoint": "Biblioteca central, planta baja"
}
```

- **Contratos de validación**: `businessName` 1..100 caracteres tras `Trim`; `description` 1..500 caracteres; `pickupPoint` 1..160 caracteres. Cualquier campo ausente, nulo, vacío o con longitud fuera de rango → `400 VALIDATION_ERROR`. Campos desconocidos → `400 VALIDATION_ERROR`. No se acepta `roles`, `sellerId`, `status` ni `version` en el cuerpo.
- **Respuestas**:

| Código | `code` | Condición |
|---|---|---|
| `201` | — | Vendedor creado y activado. Cuerpo = perfil de vendedor. |
| `200` | — | El usuario ya tenía un vendedor activo; respuesta idempotente con el mismo perfil. |
| `400` | `VALIDATION_ERROR` | Cuerpo inválido. |
| `401` | `UNAUTHORIZED` | Token ausente, inválido, expirado o revocado. |
| `403` | `SELLER_ACCOUNT_NOT_ACTIVE` | La cuenta local no está en estado `Active`. |
| `404` | `PROFILE_NOT_FOUND` | El token es válido pero no existe perfil local para el sujeto. |
| `409` | `SELLER_STATE_CONFLICT` | El vendedor existe en estado `Suspended` o `Deleted`; no se reactiva automáticamente. |
| `429` | `RATE_LIMITED` | Límite distribuido agotado; incluye `Retry-After`. |
| `503` | `SELLER_UNAVAILABLE` | MongoDB, Supabase o Redis no disponible (fail-closed). |

- **Cuerpo de éxito** (`201`/`200`):

```json
{
  "sellerId": "6f1c2b8e4a1d4f7c9b0a2d3e",
  "businessName": "Campus Snacks",
  "description": "Snacks y bebidas preparados en el campus.",
  "pickupPoint": "Biblioteca central, planta baja",
  "status": "Active",
  "isActive": true,
  "roles": ["Buyer", "Seller"],
  "createdAt": "2026-11-08T12:00:00Z",
  "updatedAt": "2026-11-08T12:00:00Z"
}
```

### 5.2 DEC-019.d — `GET /api/v1/sellers/me`

- **Acceso**: requiere Bearer válido. No acepta parámetros de consulta; cualquier query se ignora (no existe superficie para cambiar la identidad consultada).
- **Respuestas**: `200` con el mismo cuerpo que API-009 · `401 UNAUTHORIZED` · `404 SELLER_PROFILE_NOT_FOUND` (el usuario autenticado no es vendedor) · `429 RATE_LIMITED` con `Retry-After` · `503 SELLER_UNAVAILABLE`.
- No expone `email`, `phone`, `supabaseUserId`, `version` ni datos de otros vendedores.

### 5.3 API-010 — `PATCH /api/v1/sellers/me`

- **Acceso**: requiere Bearer válido. El perfil objetivo es **siempre** el del usuario autenticado: la ruta no acepta `sellerId` y, por tanto, no existe caso de edición sobre el vendedor ajeno.
- **Cuerpo**: objeto JSON con **al menos uno** de los campos opcionales (los tres permitidos, en cualquier combinación):

```json
{ "businessName": "Campus Snacks", "pickupPoint": "Biblioteca central, planta baja" }
```

- **Contratos de validación**: mismas longitudes que API-009. `null` explícito → `400 VALIDATION_ERROR` (las reglas de borrado de campos requieren ficha aprobada). Campos desconocidos → `400 VALIDATION_ERROR`. Cuerpo no objeto → `400 VALIDATION_ERROR`.
- **Respuestas**: `200` con el perfil actualizado · `400 VALIDATION_ERROR` · `401 UNAUTHORIZED` · `404 SELLER_PROFILE_NOT_FOUND` · `409 SELLER_CONFLICT` (actualización concurrente detectada por `version`) · `429 RATE_LIMITED` con `Retry-After` · `503 SELLER_UNAVAILABLE`.

### 5.4 Límites de tasa (Redis, fail-closed)

| Política | Ámbito | Límite | Ventana |
|---|---|---|---|
| `seller-activation` | Por `users._id` autenticado | 5 | 900 s |
| `seller-write` | Por `users._id` autenticado | 30 | 60 s |
| `seller-me` | Por `users._id` autenticado | 60 | 60 s |

Las particiones son identificadores internos hasheados por SHA-256 en la clave `ratelimit:{policy}:{hash}`; nunca se almacenan correos ni datos personales en claro.

## 6. Modelo de datos del Sprint 1

### 6.1 Colección `sellers` (dueño: módulo `Sellers`)

| Campo BSON | Tipo | Reglas |
|---|---|---|
| `_id` | `ObjectId` | Identificador del vendedor; es el `sellerId` que publica la API. |
| `ownerUserId` | `string` | `users._id` en hexadecimal; **índice único parcial**; inmutable. |
| `displayName` | `string` | Nombre visible del negocio (1..100). Se expone como `businessName` en los contratos de vendedor y como `displayName` en la proyección pública API-008. |
| `description` | `string` | Descripción del negocio (1..500). |
| `pickupPoint` | `string` | Punto de recogida (1..160). |
| `status` | `string` | `PendingActivation` \| `Active` \| `Inactive` \| `Suspended` \| `Deleted`. |
| `version` | `int64` | Control de concurrencia optimista; se incrementa en cada escritura. |
| `createdAt` / `updatedAt` | `DateTime` UTC | Fechas UTC. |

**Índices**: `ownerUserId` con unicidad **parcial** (`ownerUserId_unique`, `partialFilterExpression` = existe `ownerUserId`): la unicidad solo aplica a documentos creados por este Sprint, de modo que documentos históricos sin `ownerUserId` no bloquean la creación del índice ni se ven afectados. `status + updatedAt desc` (`status_updatedAt_desc`). El documento no incluye `mediaAssetIds` ni `avatarUrl` mientras Media no exista.

**Estados y visibilidad**: `PendingActivation` significa "perfil creado pero rol todavía no promovido" → **no visible** en API-008 y no completable como vendedor; `Active` es el único estado visible como vendedor; `Inactive` sigue visible en API-008 con `isActive: false`; `Suspended` y `Deleted` no se publican.

### 6.2 Colección `users` (dueño: módulo `Identity`)

Se añade el campo opcional `sellerId` (string hexadecimal) para navegación inversa. `roles` pasa a contener `Seller` tras la autoactivación. El módulo `Sellers` **nunca** escribe en `users`: solicita la promoción mediante el contrato del módulo dueño (`ISellerRolePromotion`).

### 6.3 Colección `products` y `categories` (Sprint 2)

No se crean en esta versión. El diseño previsto (sprint 2) es: `products` con `_id`, `sellerId`, `name`, `normalizedName`, `description`, `ingredients`, `categoryId`, `mediaAssetIds` (opcional, fase 05), `price` decimal MXN, `stock` entero `>= 0`, `status`, `createdAt`/`updatedAt` y `version`; `categories` con identidad, nombre normalizado, `isActive`, orden y fechas. No se implementa el índice global `{ normalizedName, categoryId }` porque impediría publicaciones homónimas de vendedores distintos.

## 7. Casos de aceptación ejecutables

| ID | Caso Given/When/Then | Verificación |
|---|---|---|
| T-CAT-V01 | Given un comprador con cuenta `Active`, when envía API-009 con los tres datos válidos, then recibe `201` con `sellerId` generado por el servidor y `roles` con `Seller`; no envía roles ni ids en el cuerpo. | `SellerActivationServiceTests`, `SellerActivationEndpointTests` |
| T-CAT-V02 | Given un vendedor ya activado, when repite API-009, then recibe `200` con el mismo `sellerId` y no se duplica documento ni rol. | `SellerActivationServiceTests` |
| T-CAT-V03 | Given un usuario no autenticado, when llama API-009/API-010/GET `/sellers/me`, then recibe `401` con contrato de error único. | `SellerActivationSecurityTests` |
| T-CAT-V04 | Given un usuario autenticado sin rol `Seller`, when consulta `GET /api/v1/sellers/me`, then recibe `404` y ningún dato de otro vendedor. | `SellerActivationSecurityTests` |
| T-CAT-V05 | Given un usuario con cuenta no `Active`, when llama API-009, then recibe `403` y no se crea ningún vendedor. | `SellerActivationServiceTests` |
| T-CAT-V06 | Given una promoción de rol fallida, when se reintenta API-009, then la activación se completa sin duplicar rol ni perfil. | `SellerActivationServiceTests` |
| T-CAT-V07 | Given un cuerpo con campos desconocidos, nulos o fuera de rango, when llama API-009 o API-010, then recibe `400` y no escribe nada. | `SellerActivationEndpointTests`, `UpdateOwnSellerProfileEndpointTests` |
| T-CAT-V08 | Given dos actualizaciones concurrentes, when ambas escriben API-010, then una gana y la otra recibe `409`. | `UpdateOwnSellerProfileServiceTests` |
| T-CAT-V09 | Given el rate limit agotado o Redis caído, when llama una ruta de vendedor, then recibe `429` con `Retry-After` o `503` sin ejecutar la operación. | `SellerActivationSecurityTests` |
| T-CAT-V10 | Given un vendedor activo, when se consulta API-008 con su `sellerId`, then devuelve solo la proyección pública approved, sin datos de contacto y con ratings `null`. | `SellerProfileEndpointTests`, `SellerProfileSecurityTests` |
| T-CAT-V11 | Given un vendedor en `PendingActivation`, when se consulta API-008, then no se publica (404). | `SellerProfileServiceTests` |
| T-CAT-V12 | Given el despliegue en la EC2, when se consulta `/health`, then devuelve `200` con MongoDB y Redis en `Healthy`. | `HealthEndpointTests` + evidencia de despliegue |

Los escenarios T-CAT-01..08 de v2.0 (catálogo, homónimos, concurrencia de stock, baja de cuenta) **no se ejecutan en esta versión**: pertenecen al Sprint 2 y a fases posteriores.

## 8. Criterios de cierre y aprobación

- [x] Constitución v2.5 reflejada en historias, fichas y decisiones DEC-019.
- [x] Fichas API-009, API-010 y `GET /api/v1/sellers/me` con payloads, actores, códigos HTTP y validaciones comprobables.
- [x] API-016 resuelta por decisión del propietario: no se implementa ni redirige (DEC-019.c).
- [x] Diseño de datos del Sprint 1 con invariantes, estados e índices justificables.
- [x] `plan.md` v2.1 y `task.md` v2.1 alineados con este spec.
- [ ] Pruebas unitarias, de integración y de seguridad del Sprint 1 ejecutadas y en verde en Release.
- [ ] Evidencia de despliegue en la EC2 con `/health` en `Healthy` y los nuevos endpoints verificados.

**Aprobación:** propietario del producto UTTT-GO, 2026-11-08 (Sprint 1). El Sprint 2 (catálogo) requiere aprobación propia antes de escribir código.

**Historial:** v1.1 aprobada (2026-10-29); v1.2 borrador (2026-11-03); v2.0 borrador (2026-11-03) con cinco decisiones funcionales; v2.1 aprobada (2026-11-08) para el Sprint 1 de vendedor con DEC-019.
