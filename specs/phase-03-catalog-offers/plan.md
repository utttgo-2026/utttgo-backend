# plan.md — Catálogo de productos por vendedor · Backend UTTT-GO
> Fase histórica `phase-03-catalog-offers` · Versión 2.0 · **Borrador técnico pendiente de revisión integral** · 2026-11-03.
> Deriva de `spec.md` v2.0 y `docs/constitution.md` v2.3. No autoriza código de producción ni rutas sin ficha. El plan v1.1 de ofertas compartidas queda superado funcionalmente.

## 1. Alcance y fronteras

Diseño objetivo **propuesto**: `Catalog` es dueño de `products` y `categories`; un producto pertenece a un solo Seller, no a un producto maestro. Se retira como requisito la colección `offers`, la creación de oferta inicial y el contrato público `/offers/*`. `IdentityAndUsers` publica la identidad/estado del vendedor, `Media` valida imágenes aprobadas, `Reviews` suministra calificaciones y `Orders` consume precio/stock/disponibilidad mediante contratos de aplicación de `Catalog`, sin acceder a sus colecciones. La conveniencia de una migración de datos queda pendiente si alguna vez se implementó el esquema antiguo; no existe código de producción documentado en esta fase.

Estructura objetivo, condicionada a aprobar el spec: `src/UtttGo.Api/Modules/Catalog/{Endpoints,Application,Domain,Infrastructure,Contracts,Validators}`; `ProductEndpoints` cubre solo API-011..015, `CategoryEndpoints` API-019 y `AdminCategoryEndpoints` API-020..022. **No** registrar API-016 como ofertas compartidas, una ruta de eliminación, ni un endpoint de recuperación sin ficha conciliada.

## 2. Modelo de datos propuesto (no contrato final aprobado)

| Colección / dueño | Campos e invariantes a especificar | Decisión / pendiente |
|---|---|---|
| `products` / Catalog | `_id`, `sellerId` inmutable derivado del perfil local, `name`, `normalizedName` para búsqueda, `description`, ingredientes, `categoryId`, `imageMediaAssetId` aprobado, `price` decimal MXN, `stock` entero no negativo, estado de publicación, timestamps UTC y versión para concurrencia. | **No** contiene `canonicalProductId`, `offerId` ni un `createdBy` que sea distinto del dueño comercial por defecto. Decidir formato de ingredientes, estados y política de eliminación en contratos revisados. |
| `categories` / Catalog | Identidad, nombre normalizado, datos públicos, `isActive`, orden y fechas UTC. | API-020 crea, API-021 edita/reactiva, API-022 desactiva; categoría inactiva oculta productos sin borrar los de sus vendedores. Revisar `slug` y demás campos de ficha antes de fijar DTO. |
| Calificaciones / Reviews | Calificaciones verificadas de producto/vendedor según su spec. | Catalog consume contrato del dueño, no copia colecciones ni inventa agregados/índices sin acuerdo. |
| Identidad/activos / otros módulos | Perfil y roles en Identity, imágenes en Media/Blob, pedidos en Orders. | La baja de vendedor obliga a coordinar eliminación de publicaciones, imágenes propias e historial propio sin afectar de modo arbitrario registros compartidos (DEC-003). |

**Índices a revisar antes de implementación:** índice de búsqueda/paginación por estado/categoría/precio y pertenencia a `sellerId`; índice para ingredientes solo cuando se defina la consulta real; índices de categorías para nombre y listado. Retirar el índice único global `{ normalizedName: 1, categoryId: 1 }`: impediría dos publicaciones homónimas de vendedores distintos. La eventual unicidad *por vendedor* es decisión técnica/funcional pendiente, no se impone aquí. Eliminar índices de `offers` del plan propuesto.

## 3. Contratos HTTP bajo revisión

| Ficha | Ruta del Excel (intacta) | Diseño propuesto y gate |
|---|---|---|
| API-011 | `GET /api/v1/products` | Búsqueda/filtros/paginación de publicaciones independientes; texto, ingredientes, precios, categoría y ratings **según ficha revisada**. No prometer criterios de orden no aprobados. |
| API-012 | `GET /api/v1/products/{productId}` | Detalle con dueño y datos aprobados, sin ofertas hijas. |
| API-013 | `POST /api/v1/products` | Seller crea producto propio con precio/stock/ingredientes y media aprobada; confirmar campos y errores de ficha antes de contrato definitivo. |
| API-014 | `PATCH /api/v1/products/{productId}` | Seller propietario actualiza información, precio y stock con validación; revisa conflicto con borrador anterior de solo nombre/descripción/imagen. Permiso Admin anterior no se presupone vigente sin cotejar ficha/política. |
| API-015 | `PATCH /api/v1/products/{productId}/availability` | Seller propietario pausa/reactiva; distinguir pausa voluntaria de agotamiento por stock. Payload `Available`/`SoldOut` del Excel se concilia antes de fijar enum. |
| API-016 | `GET /api/v1/products/{productId}/offers` | **Ficha incompatible, pendiente de aprobación de retiro/sustitución funcional por API-011.** No documentar respuesta implementable ni redireccionar silenciosamente. |
| API-019..022 | `/api/v1/categories`, `/api/v1/admin/categories[/{categoryId}]` | Conservan métodos/rutas y política previa; revisar DTOs de `slug`, estado y orden con ficha. |
| API-023 | `POST /api/v1/orders` | Orders debe referenciar `productId` y tomar precio/stock del backend; revisión de payload de ficha pendiente. |

**Eliminación de producto:** se confirmó el permiso del vendedor para eliminar sus productos, pero no existe ruta de eliminación en el inventario; no asignar `DELETE /products/{productId}` sin ficha nueva aprobada. API-016 no se reutiliza para eliminar. La eliminación de cuenta API-005 es distinta y se coordina por contrato interno entre módulos, no abre un endpoint público de Catalog.

## 4. Operaciones internas y seguridad

- Autorización por recurso: `product.sellerId == sellerId` del perfil local y rol Seller activo. Si en el futuro se permite intervención Admin, documentarla en su política/ficha; no heredar sin análisis el permiso de v1.1.
- El backend valida importes/stock solicitados por el vendedor; el comprador solo escoge producto y cantidad. Nunca aceptar `sellerId`, total ni estado del pedido desde el comprador.
- Disponibilidad comprable = categoría activa ∧ publicación activa ∧ stock suficiente ∧ vendedor habilitado según política de autorización que se acuerde. Pausa y stock cero son causas diferentes; no introducir horarios.
- Contrato interno propuesto `IProductPurchaseService` (nombre no vinculante) para reservar/liberar stock del `productId` y consultar precio vigente. La actualización de stock puede ser atómica en un documento `products`; **no** vuelve atómica por sí sola la comprobación de categoría, vendedor ni creación de `orders`. Definir coordinación, reintentos, snapshots de precio e idempotencia de liberación en Orders.
- Redis: caché fail-open; rate limiting y revocación fail-closed. Invalidar resultados de búsqueda/detalle tras cambios de ingredientes, precio, stock, estado, categoría o calificaciones disponibles. Las invalidaciones fallidas no deben permitir afirmar comprabilidad a partir del caché sin validación backend.
- Imágenes: SAS solo backend y validación `Approved` de Media; al eliminar cuenta, definir titularidad de blobs antes de borrarlos.
- Errores: contrato constitucional `{ correlationId, status, code, message, details? }`; no reutilizar códigos `OFFER_NOT_OWNED`, `OFFER_NOT_AVAILABLE` ni conflicto por duplicado global como si fueran vigentes.

## 5. Verificación y dependencias

- Unitarias: precio decimal, stock, propiedad, estado de publicación, búsqueda/filtrado, autorización y validadores de contratos aprobados.
- Integración: productos homónimos de vendedores diferentes, búsqueda/ratings (Reviews simulado), categorías, media moderada, caché/invalidación y una reserva concurrente por última unidad con MongoDB/Redis de pruebas.
- Seguridad: vendedor ajeno no edita/pausa/elimina, comprador no inyecta precio/identidad, Redis fail-closed donde corresponde; eliminación de cuenta no expone publicaciones/historial propio.
- Pedidos: pruebas cruzadas sobre carrera entre pausa/desactivación y reserva, creación de pedido, snapshot de precio y liberación una sola vez. No declarar atomicidad entre colecciones por `FindOneAndUpdate` de un documento.
- Estado documental: sin suites ejecutables todavía; no marcar pruebas ni tareas completadas.

## 6. Gates de aprobación

1. Aprobar `spec.md` v2.0 integralmente antes de aprobar este plan.
2. Conciliar fichas API-011..016 y API-023 con el equipo; decisión formal sobre API-016 y ficha nueva para eliminación pública de producto, si se implementa.
3. Cerrar contrato de ratings con Reviews, baja con Identity y compra con Orders; aprobar la estrategia técnica de almacenamiento/índices según consultas verificables.
4. Actualizar `task.md` y trazabilidad; implementar solo después de los gates. Mantener registro histórico de v1.0 aprobada y v1.1 borrador como premisas superadas, no como contratos alternativos vigentes.
