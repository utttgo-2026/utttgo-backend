# spec.md — Catálogo de productos por vendedor · Backend UTTT-GO
> Fase histórica: `phase-03-catalog-offers` · Alcance propuesto: `Catalog` y contratos con Sellers, Reviews, Media y Orders.
> Versión 2.0 · Estado: **Borrador de conciliación pendiente de revisión integral** · 2026-11-03.
> Sustituye el diseño de v1.2 (no aprobado integralmente); versiones antiguas quedan como historial, no como autorización de un modelo compartido. Subordinado a `docs/constitution.md` v2.3.

## 1. Objetivo y decisiones confirmadas

Cada vendedor publica y administra **sus propios productos/alimentos**. El comprador busca y filtra publicaciones independientes de distintos vendedores y compara sus ingredientes, precios y calificaciones disponibles del producto y del vendedor. **No existen producto maestro compartido ni ofertas hijas de varios vendedores.** Los ingredientes describen un producto, no un menú u horario (BR-004..006).

Un vendedor puede crear, editar información, cambiar precio y existencias, pausar y eliminar sus publicaciones. El backend siempre valida que sea dueño del producto (BR-009). Sigue vigente la compra de un único producto por pedido, pago en efectivo, imagen moderada antes de publicar y total calculado en servidor (BR-001..003, BR-007, BR-010).

Las decisiones funcionales están confirmadas; **el esquema, semánticas detalladas de eliminación, DTOs y rutas no cubiertas por las fichas están pendientes de revisión técnica/contractual**. Ni el nombre de esta carpeta ni documentos históricos obligan a crear un módulo `Offers`.

## 2. Historias y reglas trazables

| Historia de esta fase | Resultado esperado | Reglas |
|---|---|---|
| HU-CAT-01, 03 | Vendedor crea y actualiza su publicación con ingredientes, precio y existencias. | BR-006/009 |
| HU-CAT-04, 05 | Vendedor pausa y elimina sus propias publicaciones; jamás las ajenas. | BR-006/009/011 |
| HU-CAT-06, 07, 08, 10 | Comprador busca/filtra alimentos de distintos vendedores y compara datos y calificaciones disponibles, sin mostrar productos no comprables como comprables. | BR-004/005 |
| HU-CAT-09 | Moderación administrativa de publicaciones: revisar contrato; no inventar ruta. | BR-007/009 |

HU-CAT-02 (ofertar sobre producto compartido) y la formulación antigua de HU-CAT-07 **quedan superadas** y requieren conciliación formal con historias fuente. No declarar aprobados criterios nuevos de calificación, equivalencia exacta o ordenamiento sin ficha/spec correspondiente.

## 3. Requisitos funcionales propuestos (EARS)

| ID | Requisito | Estado |
|---|---|---|
| RF-01 | Cuando un Seller activo publica, el backend debe crear un producto propio con `sellerId` derivado de su identidad local, nombre, descripción, ingredientes, precio, existencias, categoría activa e imagen aprobada; sin producto maestro ni oferta inicial separada. | Confirmado funcionalmente; contrato API-013 a revisar |
| RF-02 | Cuando vendedores distintos publican alimentos con el mismo nombre/categoría, el sistema debe poder conservar ambas publicaciones; no devolver `409` por duplicado global. La política de duplicados del mismo vendedor queda por definir. | Confirmado para propiedad independiente; detalle pendiente |
| RF-03 | Cada producto debe asociarse a un único vendedor propietario y contener sus datos comerciales; no requiere `canonicalProductId`, `offerId` ni colección `offers` para publicar. | Confirmado funcionalmente |
| RF-04 | Cuando el vendedor propietario edita información, ingredientes, precio o existencias, el backend debe validar permisos, formato y rangos; otro vendedor no puede modificar el recurso. | Confirmado funcionalmente; campos de API-014 a conciliar |
| RF-05 | Cuando el vendedor propietario pausa su publicación, esta deja de poder comprarse; el comportamiento de API-015 y el estado resultante requieren contrato actualizado, no traducir automáticamente a `SoldOut`. | Confirmado funcionalmente; ficha pendiente |
| RF-06 | Cuando el vendedor propietario elimina su producto, deja de publicarse y de ser comprable; política física/lógica, pedidos existentes y ficha de ruta de eliminación siguen pendientes. | Confirmado funcionalmente; **ruta sin ficha** |
| RF-07 | Cuando un visitante busca/filtra, debe recibir productos de distintos vendedores con datos del producto y del vendedor, filtrables por parámetros aprobados (texto/ingredientes, precio, categoría y calificaciones cuando existan), paginados. | Confirmado funcionalmente; query/orden pendientes de ficha |
| RF-08 | Cuando se presentan calificaciones, deben proceder de Reviews mediante contrato del módulo dueño; no inventar puntuaciones, votos o índices hasta su spec. | Confirmado funcionalmente; dependencia de Reviews |
| RF-09 | Solo una publicación activa, en categoría activa y con existencias suficientes puede comprarse; el backend vuelve a comprobarlo en Orders y toma precio de la base de datos. No mostrar como comprable un recurso oculto. | Confirmado funcionalmente; coordinación con Orders pendiente |
| RF-10 | Cuando se reserva stock para un pedido, no debe haber sobreventa; la liberación ante cancelación/rechazo debe ser idempotente. La coordinación con baja/pausa y pedidos se define en Orders. | Regla confirmada; diseño pendiente |
| RF-11 | Toda imagen publicada debe referenciar un `mediaAssetId` aprobado por Media; moderación fallida bloquea publicación. | Constitucional |
| RF-12 | La gestión administrativa de categorías conserva API-019..022 y su política de desactivación/reactivación; ocultar categoría no borra ni modifica productos del vendedor. | Decisión previa confirmada |
| RF-13 | La lectura pública puede usar Redis con degradación fail-open para caché, sin relajar rate limit fail-closed; cambios de precio/stock/estado/categoría invalidan lecturas afectadas. | Constitucional; diseño pendiente |
| RF-14 | Cuando se elimina la cuenta de un vendedor, sus productos/publicaciones y contenido propio deben eliminarse conforme a BR-011; referencias compartidas requieren el diseño de DEC-003. | Confirmado funcionalmente; coordinación pendiente |

### Reglas de validación y seguridad

- `sellerId`, roles y dueño se derivan del perfil local, nunca del body; un cambio solicitado de precio/stock por el vendedor **sí** puede ser dato de entrada validado. El comprador no define precio, total ni estado al comprar.
- Precio `decimal` MXN; existencias enteras `>= 0`; el total de un pedido es `price × quantity` obtenido por el servidor. Nombre, ingredientes y filtros tienen validación server-side; límites y formato se fijan al cerrar contrato.
- Unicidad global anterior `{ normalizedName, categoryId }` queda **invalidada**. No se presupone clave de agrupación equivalente ni relación compartida.
- La visibilidad de categoría y la disponibilidad del producto se verifican también en backend; el caché no sustituye esas validaciones.
- Las calificaciones del producto/vendedor no se almacenan o editan por el cliente como verdad del catálogo; se integran por contrato del módulo Reviews.

## 4. Contratos del inventario provisional (sin editar el Excel)

| Ficha | Método y ruta exactos | Lectura propuesta / contradicción |
|---|---|---|
| API-011 | `GET /api/v1/products` | Buscar/filtrar publicaciones por vendedor; definir query exacta, ingredientes y ratings. |
| API-012 | `GET /api/v1/products/{productId}` | Detalle de la publicación propia de un vendedor. |
| API-013 | `POST /api/v1/products` | Publicar producto propio con precio y existencias; revisar payload/response de ficha. |
| API-014 | `PATCH /api/v1/products/{productId}` | Edición por vendedor dueño, incluidos precio y existencias; supera la restricción descriptiva anterior; conciliar payload/actor Admin. |
| API-015 | `PATCH /api/v1/products/{productId}/availability` | Pausar/reactivar publicación; revisar `Available`/`SoldOut` de ficha sin equiparar stock cero a pausa. |
| API-016 | `GET /api/v1/products/{productId}/offers` | **Contradice el modelo confirmado. Proponer retiro/sustitución funcional por búsqueda/filtros de API-011; no implementarla ni declararla retirada hasta aprobación formal de ficha.** |
| API-019..022 | `GET /api/v1/categories`, `POST /api/v1/admin/categories`, `PATCH /api/v1/admin/categories/{categoryId}`, `DELETE /api/v1/admin/categories/{categoryId}` | Conservar rutas; categoría desactivada oculta publicaciones, API-021 reactiva; revisar campos existentes. |
| API-023 | `POST /api/v1/orders` | Pedido de un producto por vendedor; revisar `offerId` anterior, cantidad y snapshot de precio con Orders. |

**Eliminación de producto:** no hay `DELETE /api/v1/products/{productId}` entre API-001..042. Es funcionalidad confirmada pero su **ruta/contrato no están aprobados**. Lo mismo vale para rutas antiguas `/offers/*` y `/auth/recover`; no transformarlas en endpoints vigentes por aparecer en borradores.

## 5. Casos de aceptación y pruebas previstas (no ejecutadas)

| ID | Caso Given/When/Then |
|---|---|
| T-CAT-01 | Dos vendedores publican alimentos con mismo nombre/categoría: ambos se conservan y aparecen en búsqueda; no hay conflicto global. |
| T-CAT-02 | Un vendedor intenta editar, pausar o eliminar el producto de otro: `403`; el propietario modifica ingredientes/precio/stock solo en contratos aprobados. |
| T-CAT-03 | Búsqueda y filtros paginados muestran productos distintos, ingredientes, precios y calificaciones reales disponibles; sin `canonicalProductId` ni oferta compartida. |
| T-CAT-04 | Imagen no aprobada impide publicar; vendedor suspendido o sin rol no publica. |
| T-CAT-05 | Categoría o producto pausado/agotado no se compra; reactivación no inventa existencias ni altera pedidos existentes. |
| T-CAT-06 | Dos compras concurrentes por última unidad: una sola reserva; reintentos de cancelación no duplican existencias (con Orders). |
| T-CAT-07 | Borrar cuenta de vendedor elimina sus publicaciones propias sin exponer ni borrar arbitrariamente datos del otro participante (con DEC-003). |
| T-CAT-08 | API-016 no se presenta como comparativa de ofertas compartidas; auditar rutas implementadas contra fichas aprobadas. |

## 6. Criterios de cierre y aprobación

- [ ] Constitución v2.3 reflejada en historias/fichas, y versión integral de este spec aprobada por el equipo antes de código.
- [ ] Conciliadas formalmente fichas API-011..016/023 y aprobada ficha de eliminación de producto si se implementa.
- [ ] Resuelto destino formal de API-016 sin modificar unilateralmente el inventario de 42 fichas.
- [ ] Diseños de Orders/Reviews/Identity y borradores de plan/task alineados; tests reales en verde antes de cerrar fase.

**Historial:** v1.1 aprobada (2026-10-29); v1.2 borrador (2026-11-03); v2.0 borrador que incorpora cinco decisiones funcionales confirmadas por el equipo, sin aprobar contratos o detalles técnicos pendientes.
