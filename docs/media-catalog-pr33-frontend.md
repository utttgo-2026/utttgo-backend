# Media + Catalog — contrato frontend de PR #33

Fuente: `GET /openapi/v1.json` de la aplicación local, copia en
[`docs/openapi/media-catalog-pr33.json`](openapi/media-catalog-pr33.json).
Los ID y fechas de abajo son **datos ilustrativos**, no registros existentes.
`<...>` representa valores devueltos por el backend, no URLs firmadas válidas.
JSON en camelCase; fechas UTC. No conservar ni registrar una URL firmada después de su expiración.

## 1. Solicitar intención de subida (vendedor autenticado)

`POST /api/v1/media/upload-url` · `Authorization: Bearer <JWT>` · `201`

<!-- contract:media-upload-request -->
```json
{"fileName":"producto.jpg","mimeType":"image/jpeg","size":10}
```

<!-- contract:media-upload-response -->
```json
{
  "mediaAssetId": "507f1f77bcf86cd799439011",
  "uploadUrl": "<PRESIGNED_PUT_URL_FROM_BACKEND>",
  "expiresAt": "2026-11-20T12:05:00Z",
  "headers": {"Content-Type": "image/jpeg"}
}
```

El cliente hace `PUT` directamente al `uploadUrl`, con el **mismo**
`Content-Type: image/jpeg` y los bytes exactos. **Cambio de contrato Media
pendiente de aprobación final de Expo Go:** el backend de esta rama rechaza
`size` debe estar entre **1 y 4 000 000 bytes inclusivos** (antes: 5 MiB);
Expo Go debe validar el tamaño binario antes de solicitar la URL y mostrar un
error si excede **4 000 000 bytes**, sin comprimir ni transformar automáticamente.
MIME `image/jpeg`, `image/png` o `image/webp` y extensión correspondiente.
La URL expira a más tardar en 5 minutos; el backend comprueba tamaño y MIME
del objeto real con S3 HEAD y comprueba firma binaria antes de llamar a Azure durante la confirmación. El estado inicial en
Mongo es `PendingUpload`.

## 2. Confirmar subida y leer estado

`POST /api/v1/media/confirm` · `Authorization: Bearer <JWT>` · `200`

<!-- contract:media-confirm-request -->
```json
{"mediaAssetId":"507f1f77bcf86cd799439011"}
```

<!-- contract:media-confirm-response -->
```json
{"mediaAssetId":"507f1f77bcf86cd799439011","status":"PendingModeration"}
```

La ruta del inventario `POST /api/v1/media/{mediaAssetId}/complete` **no
requiere body** y comparte la misma confirmación idempotente. La respuesta
refleja el estado persistido: `PendingUpload`, `PendingModeration`, `Approved`,
`Rejected` o `ProcessingFailed` según el momento y el resultado. Un objeto
ausente, de tipo o tamaño distinto, no pasa a moderación (`400 INVALID_OBJECT`).
La lectura del estado propio es
`GET /api/v1/media/{mediaAssetId}` · `Authorization: Bearer <JWT>` · `200`:

<!-- contract:media-status-response -->
```json
{
  "mediaAssetId": "507f1f77bcf86cd799439011",
  "status": "PendingModeration",
  "imageUrl": null,
  "imageUrlExpiresAt": null
}
```

En estado `Approved`, `imageUrl` e `imageUrlExpiresAt` son URL GET presignada
y caducidad UTC; en los demás estados son `null`. Otro propietario recibe
`404 ASSET_NOT_FOUND`. Actualmente **no hay umbrales aprobados ni configuración
Azure**, por lo que una confirmación real no puede producir `Approved`.

## 3. Crear y consultar producto

`POST /api/v1/products` · `Authorization: Bearer <JWT>` · `201` solo
con un `mediaAssetId` propio y `Approved`. Omitir `mediaAssetId` crea
el producto **sin fotografía**. `imageUrl` de entrada no sustituye al ID:
un valor no vacío recibe `422 IMAGE_NOT_AVAILABLE`.

<!-- contract:product-create-request -->
```json
{
  "name": "Producto de ejemplo",
  "description": "Descripción de ejemplo",
  "ingredients": ["Ingrediente"],
  "categoryId": "507f1f77bcf86cd799439031",
  "price": 55.50,
  "stock": 10,
  "mediaAssetId": "507f1f77bcf86cd799439011"
}
```

`GET /api/v1/products/{productId}` · `200` devuelve el mismo DTO público que
POST y PATCH. Ejemplo **condicional** (solo después de `Approved`):

<!-- contract:product-response -->
```json
{
  "id": "507f1f77bcf86cd799439021",
  "sellerId": "507f1f77bcf86cd799439041",
  "sellerDisplayName": "Puesto de ejemplo",
  "name": "Producto de ejemplo",
  "description": "Descripción de ejemplo",
  "ingredients": ["Ingrediente"],
  "categoryId": "507f1f77bcf86cd799439031",
  "categoryName": "Comida Preparada",
  "price": 55.50,
  "stock": 10,
  "status": "Active",
  "isAvailable": true,
  "ratingAverage": null,
  "ratingCount": null,
  "createdAt": "2026-11-20T12:00:00Z",
  "updatedAt": "2026-11-20T12:00:00Z",
  "imageUrl": "<PRESIGNED_GET_URL_FROM_BACKEND>",
  "imageUrlExpiresAt": "2026-11-20T12:05:00Z"
}
```

Sin fotografía ambos campos de imagen son `null`. La misma proyección se usa
en `GET /api/v1/products` (`items`, `page`, `pageSize`, `totalCount`). Ninguna
respuesta de producto expone `mediaAssetId`; conservarlo desde la intención.
Con ID ajeno, inexistente o no aprobado, POST/PATCH devuelven
`422 IMAGE_NOT_APPROVED`.

## 4. PATCH de producto propio

`PATCH /api/v1/products/{productId}` · `Authorization: Bearer <JWT>` · `200`
devuelve el **ProductResponse completo** del ejemplo anterior (URL GET temporal
o `null`). La URL firmada puede cambiar en cada lectura; el control CAS por
versión del producto es interno (`409 PRODUCT_CONFLICT` ante carrera).

**Agregar:**
<!-- contract:product-patch-add -->
```json
{"mediaAssetId":"507f1f77bcf86cd799439011"}
```

**Reemplazar por otro asset propio aprobado:**
<!-- contract:product-patch-replace -->
```json
{"mediaAssetId":"507f1f77bcf86cd799439012"}
```

**Quitar (responde `imageUrl:null`, `imageUrlExpiresAt:null`):**
<!-- contract:product-patch-remove -->
```json
{"mediaAssetId":null}
```

**Conservar (campo omitido al editar otro dato):**
<!-- contract:product-patch-preserve -->
```json
{"stock":9}
```

## Disponibilidad para frontend

- **Integrable ya con backend local:** formularios/DTO de intención, PUT con
  headers devueltos, confirmación idempotente, lectura de estado, crear sin foto,
  PATCH quitar/conservar, visualización de URL GET temporal cuando aparezca;
  OpenAPI versionado y E2E local documentan el flujo.
- **No prometer aún fotografía real aprobada:** PUT/GET requieren bucket privado
  e IAM de AWS verificados en entorno controlado; análisis real requiere endpoint
  y clave Azure por secretos y umbrales de cuatro categorías aprobados por el
  equipo. Mientras falten, la moderación es fail-closed y Catalog rechazará
  `mediaAssetId` no `Approved`. LocalStack no demuestra privacidad AWS.
