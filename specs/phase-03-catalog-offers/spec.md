# spec.md — Catálogo de Productos Canónicos y Ofertas Comparables · Backend UTTT-GO
> Fase: `phase-03-catalog-offers` · Módulos: `Catalog`, `Offers` (consumo de `Media` y `Admin`)
> Versión: 1.1 · Estado: **Aprobado** · Fecha: 2026-10-29
> Documento subordinado a `constitution.md` v2.1 y a `AGENTS.md` v2.0. En caso de conflicto, prevalece la constitución.
> Este spec **no** autoriza escribir código de producción; deriva del discovery de 5 preguntas.

---

## 1. Contexto y Objetivo

Este spec define el módulo de **catálogo de productos canónicos** y **ofertas comparables tipo Eneba** del backend UTTT-GO. Es la propuesta de valor central del producto: un comprador compara varias ofertas de distintos vendedores para el **mismo producto canónico** y elige una.

Reglas innegociables aplicadas (BR-001, BR-004, BR-005, BR-006, BR-007, BR-009, BR-010):

- **Producto canónico** = qué se vende (nombre, descripción, categoría, imagen principal). **No** contiene precio, stock ni vendedor.
- **Oferta** = quién lo vende, a qué precio, con qué stock y condiciones. Es la unidad de compra y comparación.
- **No existen menús ni horarios por franja**; la disponibilidad depende solo de stock/estado de la oferta.
- **No existe carrito**; la compra se ejecuta sobre una única oferta.
- Toda imagen usa SAS generada por el backend y moderación previa con Azure AI Content Safety.
- Precio, stock, `sellerId` y estado se derivan server-side; nunca del cliente.

Decisiones de discovery incorporadas:

| # | Decisión | RF |
|---|---|---|
| D1 | El vendedor crea Producto canónico + primera Oferta en un solo flujo, **autoaprobado**, con moderación automática de imagen como única validación. | RF-01 |
| D2 | Unicidad por **nombre normalizado + categoría**; duplicado ⇒ `409` con el `productId` existente. | RF-02 |
| D3 | El stock se **reserva atómicamente al crear el pedido** y se libera al rechazar/cancelar; stock 0 ⇒ `SoldOut`. | RF-11, RF-12 |
| D4 | Catálogo y comparativa de **lectura pública** con rate limit; escritura solo para vendedor autenticado. | RF-09, RF-10 |
| D5 | MVP de ofertas: editar, pausar/reactivar, baja lógica y suspensión admin; búsqueda/filtros/orden. Fuera: CRUD de categorías, galería multi-imagen, reseñas y favoritos. | RF-04..RF-10, RF-13, RF-14 |

---

## 2. Usuarios y Actores

| Actor | Descripción | Permisos en el módulo |
|---|---|---|
| **Visitante / Comprador** | Cualquier persona que consulta el catálogo público. | Leer productos, categorías y comparativa de ofertas; sin autenticación. |
| **Vendedor (Seller)** | Usuario con rol `Seller` activo (autoactivado en phase-02). | Crear producto+oferta, crear ofertas sobre productos existentes, editar/pausar/reactivar/eliminar lógicamente sus propias ofertas. |
| **Administrador (Admin)** | Usuario con rol `Admin` desde MongoDB. | Suspender/reactivar ofertas; auditar. No modera productos (autoaprobados). |
| **Sistema** | Validación, moderación y caché. | Normalizar nombres, validar imágenes, invalidar caché, reservar stock vía contrato con `Orders`. |

Regla de autorización: el `sellerId` se deriva del perfil autenticado (`sub` + MongoDB), nunca del body. Un vendedor solo opera sobre sus propias ofertas.

---

## 3. Historias de Usuario

| ID | Como… | Quiero… | Para… |
|---|---|---|---|
| HU-CAT-01 | Vendedor | publicar un producto canónico con mi primera oferta en un solo flujo | empezar a vender sin aprobación manual |
| HU-CAT-02 | Vendedor | ofertar sobre un producto ya existente | competir en la comparativa del mismo producto |
| HU-CAT-03 | Vendedor | editar precio, stock y condiciones de mi oferta | ajustarme al mercado y a mi inventario |
| HU-CAT-04 | Vendedor | pausar o reactivar mi oferta | dejar de vender temporalmente sin perderla |
| HU-CAT-05 | Vendedor | dar de baja lógica mi oferta | retirarme sin borrar historial |
| HU-CAT-06 | Comprador | buscar y filtrar productos por texto, categoría y precio | encontrar rápido qué comer |
| HU-CAT-07 | Comprador | comparar ofertas del mismo producto por precio y vendedor | elegir la mejor opción |
| HU-CAT-08 | Comprador | ver solo ofertas realmente comprables | no perder tiempo con ofertas agotadas |
| HU-CAT-09 | Admin | suspender una oferta que incumple reglas | moderar el catálogo |
| HU-CAT-10 | Comprador | consultar el catálogo sin iniciar sesión | explorar antes de registrarme |

---

## 4. Requisitos Funcionales (EARS)

| ID | Requisito (EARS) | Prioridad |
|---|---|---|
| **RF-01** | **Cuando** un vendedor autenticado y activo crea un producto canónico con su primera oferta, el sistema **debe** crearlo en un solo flujo **sin aprobación manual**, siempre que la imagen principal esté `Approved`; si la imagen no está aprobada, **debe** rechazar la operación (`422`). | Obligatorio |
| **RF-02** | **Cuando** se crea un producto canónico, el sistema **debe** normalizar el nombre (minúsculas, sin acentos, espacios colapsados) y **debe** validar unicidad por `normalizedName + categoryId`; si ya existe, **debe** responder `409` incluyendo el `productId` existente. | Obligatorio |
| **RF-03** | El producto canónico **debe** contener solo datos de catálogo (nombre, descripción, categoría, imagen principal y estado) y **no debe** contener precio, stock ni `sellerId`; la oferta **debe** contener `sellerId`, `price`, `stock`, `conditionNotes` y `status`. | Obligatorio |
| **RF-04** | **Cuando** un vendedor crea una oferta sobre un producto existente, el sistema **debe** derivar `sellerId` del perfil autenticado y **debe** ignorar/rechazar cualquier `sellerId` enviado por el cliente. | Obligatorio |
| **RF-05** | **Cuando** un vendedor edita su propia oferta, el sistema **debe** permitir modificar `price`, `stock` y `conditionNotes`; **si** la oferta pertenece a otro vendedor, **debe** responder `403`. | Obligatorio |
| **RF-06** | **Cuando** un vendedor pausa su oferta, el sistema **debe** cambiarla a `Inactive`; **cuando** la reactiva, **debe** volver a `Active` solo si el producto sigue `Active`. | Obligatorio |
| **RF-07** | **Cuando** un vendedor da de baja lógica su oferta, el sistema **debe** marcarla `Deleted` y **no debe** borrar el documento; la oferta deja de ser visible y comprable. | Obligatorio |
| **RF-08** | **Cuando** un admin suspende una oferta, el sistema **debe** marcarla `Suspended`; **cuando** la reactiva, **debe** volver a `Active` solo si no está `Deleted`; toda acción **debe** quedar auditada. | Obligatorio |
| **RF-09** | **Cuando** un visitante consulta el catálogo, el sistema **debe** permitir listar productos `Active` de forma paginada y pública, con búsqueda por texto, filtro por categoría, rango de precio y ordenamiento (precio, nombre, más reciente). | Obligatorio |
| **RF-10** | **Cuando** un visitante consulta las ofertas de un producto, el sistema **debe** devolver únicamente ofertas **comprables** (`Active`, stock > 0, producto `Active`), ordenadas por precio ascendente, con vendedor, precio y stock; sin autenticación. | Obligatorio |
| **RF-11** | El sistema **debe** considerar una oferta no comprable cuando su stock sea `0` o su estado no sea `Active`; **no debe** existir disponibilidad por menú, horario o franja. | Obligatorio |
| **RF-12** | **Cuando** se crea un pedido, el sistema **debe** reservar/descontar stock de forma atómica y **no debe** permitir sobreventa; **cuando** el pedido se rechaza o cancela, **debe** liberar el stock reservado. | Obligatorio |
| **RF-13** | El producto canónico **debe** tener una única imagen principal referenciada por `mediaAssetId` en estado `Approved`; **no debe** existir galería multi-imagen en el MVP. | Obligatorio |
| **RF-14** | El sistema **debe** exponer las categorías como catálogo semilla de solo lectura; **no debe** permitir CRUD de categorías por API en el MVP; todo producto **debe** referenciar una categoría existente y activa. | Obligatorio |
| **RF-15** | **Cuando** se lee el catálogo o la comparativa, el sistema **debe** servirlos desde Redis (`cache:catalog:{recurso}:{clave}`, TTL 60 s) y **debe** invalidar las claves afectadas al crear, editar, pausar, reactivar, suspender o eliminar un producto u oferta; **si** Redis falla, **debe** ir a MongoDB (fail-open). | Obligatorio |

### 4.1 Normalización de nombre

- `normalizedName = lower(removeDiacritics(trim(collapseSpaces(name))))`.
- La comparación es exacta sobre `normalizedName + categoryId`; no se permiten duplicados dentro de la misma categoría.
- Categorías distintas pueden tener el mismo nombre de producto.

---

## 5. Requisitos No Funcionales

| ID | Requisito | Verificación |
|---|---|---|
| **RNF-01** | Las rutas públicas de catálogo **deben** tener rate limiting distribuido con Redis y responder `429` con el contrato de error. | Tests de integración |
| **RNF-02** | Las rutas de escritura **deben** exigir JWT válido y rol `Seller`/`Admin` leído de MongoDB (nunca del JWT). | `UtttGo.SecurityTests` |
| **RNF-03** | La autorización **debe** validar propiedad por recurso: un vendedor solo edita/pausa/elimina sus ofertas. | Tests de seguridad |
| **RNF-04** | La caché **debe** ser fail-open y las escrituras **deben** invalidar las claves de catálogo/comparativa afectadas antes de responder. | Test de invalidación |
| **RNF-05** | Toda entrada **debe** validarse server-side: `price > 0` como `decimal` MXN, `stock` entero `>= 0`, longitudes máximas de textos y `categoryId` válido. | Pruebas unitarias |
| **RNF-06** | Los listados **deben** paginar con `page` + `pageSize` y un máximo configurable; nunca devolver colecciones ilimitadas. | Tests de paginación |
| **RNF-07** | Toda respuesta de error **debe** incluir `correlationId` y seguir el contrato único de `constitution.md` § 14. | Tests de contrato |
| **RNF-08** | La creación de producto/oferta **debe** ser idempotente ante reintentos razonables (misma solicitud no genera duplicados). | Test de idempotencia |
| **RNF-09** | Las fechas **deben** persistirse y exponerse en UTC ISO 8601. | Revisión de modelo |
| **RNF-10** | La reserva de stock **debe** ser atómica (`findOneAndUpdate` con condición `stock >= quantity`) para evitar sobreventa bajo concurrencia. | Test de concurrencia |
| **RNF-11** | La imagen principal **debe** validarse contra `Media` como `Approved`; sin moderación aprobada no hay publicación (fail-closed). | Tests con Media mockeado |
| **RNF-12** | Los índices **deben** soportar las consultas reales: unicidad `normalizedName+categoryId`, búsqueda de ofertas por `productId+status+price`, filtros de catálogo y paginación. | Revisión + tests |
| **RNF-13** | Los logs **no deben** contener secretos, tokens ni datos personales sensibles; **deben** incluir `correlationId`. | Revisión/secret scanning |
| **RNF-14** | El módulo **no debe** introducir entidades ni campos de carrito, menú, horario, franja o pasarela de pago. | Revisión estática + tests de negocio |

---

## 6. Contratos (resumen)

> Los DTOs y contratos detallados se definirán en `plan.md`/`task.md`. Rutas bajo `/api/v1`.

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| GET | `/api/v1/products` | Público | Lista paginada con `search`, `categoryId`, `minPrice`, `maxPrice`, `sort`, `page`, `pageSize`. |
| GET | `/api/v1/products/categories` | Público | Catálogo semilla de categorías (solo lectura); se registra antes de `{id}`. |
| GET | `/api/v1/products/{id}` | Público | Detalle del producto canónico. |
| GET | `/api/v1/products/{id}/offers` | Público | Comparativa de ofertas comprables, orden por precio. |
| POST | `/api/v1/products` | Seller | Crea producto canónico + primera oferta en un flujo. |
| POST | `/api/v1/products/{productId}/offers` | Seller | Crea oferta sobre producto existente. |
| PATCH | `/api/v1/offers/{offerId}` | Seller | Edita `price`, `stock`, `conditionNotes` de oferta propia. |
| PATCH | `/api/v1/offers/{offerId}/pause` | Seller | Pausa oferta propia (`Inactive`). |
| PATCH | `/api/v1/offers/{offerId}/reactivate` | Seller/Admin | Reactiva (`Inactive`→`Active` para el dueño; `Suspended`→`Active` para Admin). |
| DELETE | `/api/v1/offers/{offerId}` | Seller | Baja lógica (`Deleted`). |
| PATCH | `/api/v1/offers/{offerId}/suspend` | Admin | Suspende oferta (`Suspended`). |

Contrato de error: `{ correlationId, status, code, message, details? }` (constitution §14).

Códigos de negocio previstos: `PRODUCT_ALREADY_EXISTS` (409, con `productId`), `CATEGORY_NOT_FOUND` (422), `MEDIA_ASSET_NOT_APPROVED` (422), `OFFER_NOT_OWNED` (403), `OFFER_NOT_AVAILABLE` (409), `INVALID_STATE_TRANSITION` (409), `INSUFFICIENT_STOCK` (409), `PRODUCT_NOT_FOUND`/`OFFER_NOT_FOUND` (404), `RATE_LIMITED` (429).

---

## 7. Casos Límite

| ID | Caso | Comportamiento esperado |
|---|---|---|
| CL-01 | Producto con nombre normalizado ya existente en la misma categoría | `409 PRODUCT_ALREADY_EXISTS` con `productId` existente. |
| CL-02 | Nombre con acentos/mayúsculas/espacios extra que normaliza a uno existente | Mismo `409`; la normalización es la base de la unicidad. |
| CL-03 | Producto con `mediaAssetId` inexistente o no `Approved` | `422 MEDIA_ASSET_NOT_APPROVED`; no se crea. |
| CL-04 | Categoría inexistente o inactiva | `422 CATEGORY_NOT_FOUND`; no se crea. |
| CL-05 | Vendedor no autenticado o sin rol `Seller` activo | `401`/`403`; no se crea ni edita. |
| CL-06 | `sellerId` enviado en el body | Se ignora/rechaza; prevalece el perfil autenticado. |
| CL-07 | Vendedor intenta editar/pausar/eliminar oferta ajena | `403 OFFER_NOT_OWNED`. |
| CL-08 | `price <= 0`, `stock < 0` o texto fuera de longitud | `400 VALIDATION_ERROR`. |
| CL-09 | Stock llega a 0 | Oferta `SoldOut` (no comprable); no se elimina. |
| CL-10 | Dos pedidos concurrentes por la última unidad | Solo uno reserva; el otro recibe `409 INSUFFICIENT_STOCK`. |
| CL-11 | Pedido rechazado o cancelado | El stock reservado se libera y la oferta vuelve a ser comprable si corresponde. |
| CL-12 | Reactivar oferta cuyo producto está `Inactive` | `409 INVALID_STATE_TRANSITION`; no se reactiva. |
| CL-13 | Reactivar oferta `Deleted` (por vendedor o admin) | `409 INVALID_STATE_TRANSITION`. |
| CL-14 | Producto sin ofertas comprables | Se muestra el producto con comparativa vacía; no se oculta el producto. |
| CL-15 | Redis no disponible al leer catálogo | Fail-open: se responde desde MongoDB y se registra degradación. |
| CL-16 | Redis no disponible al invalidar tras escritura | La escritura se completa; se registra alerta y se fuerza expiración corta de la clave. |
| CL-17 | Consulta pública excede rate limit | `429 RATE_LIMITED` con contrato de error. |
| CL-18 | Imagen de producto intenta sustituirse por URL arbitraria | `422`; solo se acepta `mediaAssetId` aprobado. |

---

## 8. Fuera de Alcance

- **CRUD de categorías** por API: se usa catálogo semilla de solo lectura.
- **Galería multi-imagen**: solo una imagen principal moderada por producto.
- **Reseñas, calificaciones y favoritos** (módulos separados).
- **Carrito de compras** (prohibido por BR-001).
- **Menús, horarios, franjas o disponibilidad temporal** (prohibido por BR-004).
- **Pasarela de pago o datos financieros** (prohibido por BR-003).
- **Ciclo de vida del pedido** y cálculo de total: pertenece al módulo `Orders`; aquí solo se define el contrato de reserva/liberación de stock.
- **Aprobación manual de productos** por admin (D1: autoaprobados con moderación de imagen).
- **Edición del producto canónico** tras su creación (nombre, descripción, categoría e imagen fija en el MVP).
- **Moderación de texto** (solo moderación de imagen).
- **Búsqueda con motor externo/Elasticsearch**, recomendaciones o personalización.
- **Importación masiva** de productos/ofertas.
- **Notificaciones push** por cambios de catálogo/stock.
- **Admin suspende productos** (solo ofertas).

---

## 9. Tests y Criterios de Aceptación

| ID | Nivel | Verifica |
|---|---|---|
| T-CAT-01 | Integración | Creación producto+oferta autoaprobada con imagen `Approved` (RF-01, RF-13). |
| T-CAT-02 | Integración | Unicidad normalizada por nombre+categoría y `409` con `productId` (RF-02, CL-01, CL-02). |
| T-CAT-03 | Unitarias | Separación Producto (sin precio/vendedor) vs Oferta (con precio/stock/vendedor) (RF-03). |
| T-CAT-04 | Seguridad | `sellerId` derivado del token; edición de oferta ajena ⇒ `403` (RF-04, RF-05, RNF-02, RNF-03). |
| T-CAT-05 | Integración | Editar/pausar/reactivar/baja lógica de oferta propia (RF-05, RF-06, RF-07). |
| T-CAT-06 | Integración | Admin suspende/reactiva oferta y se audita (RF-08, RNF-02). |
| T-CAT-07 | Integración | Catálogo público paginado con búsqueda/filtros/orden (RF-09, CL-17, RNF-01, RNF-06). |
| T-CAT-08 | Integración | Comparativa devuelve solo ofertas comprables ordenadas por precio (RF-10, RF-11, CL-09, CL-14). |
| T-CAT-09 | Concurrencia | Reserva atómica sin sobreventa y liberación al cancelar (RF-12, RNF-10, CL-10, CL-11). |
| T-CAT-10 | Integración | Caché hit/miss, invalidación en escritura y fail-open (RF-15, RNF-04, CL-15, CL-16). |

Criterios de aceptación:

- [ ] **AC-001** El producto canónico no contiene precio, stock ni vendedor; la oferta sí.
- [ ] **AC-002** Creación producto+oferta sin aprobación manual, con imagen `Approved`.
- [ ] **AC-003** Duplicado normalizado por nombre+categoría devuelve `409` con `productId`.
- [ ] **AC-004** Solo ofertas `Active` con stock > 0 aparecen como comprables, ordenadas por precio.
- [ ] **AC-005** Un vendedor no puede operar sobre ofertas ajenas.
- [ ] **AC-006** La reserva de stock es atómica y no hay sobreventa.
- [ ] **AC-007** No existen endpoints ni campos de carrito, menú, horario o categoría CRUD.
- [ ] **AC-008** Caché Redis con TTL 60 s, invalidación en escritura y fail-open.
- [ ] **AC-009** T-CAT-01 a T-CAT-10 en verde y trazabilidad actualizada.

---

## 10. Definición de Hecho (DoD)

- [ ] Este `spec.md` aprobado (2026-10-29) y registrado en `memoria.md`.
- [ ] `plan.md` y `task.md` de la fase derivados de este spec.
- [ ] Contratos/DTOs y validaciones implementados conforme a §6.
- [ ] Índices MongoDB de RNF-12 creados de forma idempotente.
- [ ] Caché Redis con namespaces `cache:catalog:*`, TTL 60 s e invalidación en escritura.
- [ ] Reserva/liberación de stock expuesta como contrato para `Orders`.
- [ ] Build Release y `dotnet format` en verde.
- [ ] T-CAT-01 a T-CAT-10 en verde.
- [ ] Sin secretos en el diff; configuración vía Key Vault/entorno.
- [ ] Trazabilidad `spec ↔ endpoint ↔ test` actualizada.

---

## 11. Aprobación y Control de Cambios

| Campo | Valor |
|---|---|
| Estado | **Aprobado** |
| Fecha | 2026-10-29 |
| Versión | 1.0 |
| Autor | AGENT-BE-02 (API Designer) |
| Revisores | AGENT-BE-01 (Architect), AGENT-BE-03 (Data Modeler), AGENT-BE-04 (Security Reviewer), AGENT-BE-06 (Memory Keeper) |
| Aprobado por | Equipo UTTT-GO |
| Próxima revisión | Al cambiar las reglas de stock, el alcance de ofertas o la política de caché |

### 11.1 Registro de versiones

| Versión | Fecha | Cambio | Aprobado por |
|---|---|---|---|
| 1.0 | 2026-10-29 | Formalización del catálogo canónico y ofertas comparables (discovery de 5 preguntas) | Equipo UTTT-GO |
| 1.1 | 2026-10-29 | Alineación de rutas canónicas a `/api/v1/products/*` y `/api/v1/offers/*` (sustituye los prefijos provisionales `/catalog` y `/seller`) | Equipo UTTT-GO |

### 11.2 Reglas de control de cambios

1. Cambiar el flujo de creación producto+oferta, la unicidad, las reglas de stock, el acceso público o la política de caché exige nueva versión de este spec y registro en `memoria.md`.
2. Ningún agente implementa código que contradiga la constitución o este spec.
3. Ampliar el alcance (categorías CRUD, galería, reseñas, favoritos) requiere nuevo spec o enmienda aprobada.
