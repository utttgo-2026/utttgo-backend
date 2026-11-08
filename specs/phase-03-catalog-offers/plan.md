# plan.md — Catálogo de productos por vendedor · Backend UTTT-GO
> Fase `phase-03-catalog-offers` · Versión 2.1 · **Aprobado para el Sprint 1 (vendedor)** · 2026-11-08.
> Deriva de `spec.md` v2.1 y `docs/constitution.md` v2.5 (DEC-019). Autoriza el Sprint 1 (autoactivación y perfil de negocio); el Sprint 2 (catálogo de productos) sigue bloqueado hasta su propia aprobación. El plan v1.1 de ofertas compartidas queda superado funcionalmente.

## 1. Alcance y fronteras

**Sprint 1 (autorizado):** el módulo `Sellers` gana un escritor de la colección `sellers` y tres rutas de lectura/escritura sobre el **propio** vendedor. El módulo `Identity` conserva la propiedad de `users` y publica dos contratos de aplicación que `Sellers` consume:

| Contrato (dueño) | Implementación | Propósito |
|---|---|---|
| `IAuthenticatedLocalProfileReader` (Identity) | `AuthenticatedLocalProfileReader` | Resuelve el token a un perfil local validado (firma, issuer, audience, expiración, blacklist Redis) **sin** exigir `Active`, para que el módulo pueda responder `403` por cuenta no activa en lugar de `401`. |
| `ISellerRolePromotion` (Identity) | `MongoSellerRolePromotion` | Asigna el rol `Seller` y `users.sellerId` de forma idempotente sobre `users`. |

**Fronteras innegociables:** `Sellers` escribe únicamente `sellers`; nunca lee ni escribe `users`, `products`, `categories`, `mediaAssets` ni colecciones de `Orders`/`Reviews`. `Identity` no conoce `Sellers` (la dependencia es unidireccional `Sellers → Identity`). No hay ruta que acepte un `sellerId` del cliente: el vendedor objetivo se deriva siempre de la sesión validada, por lo que no existe superficie de edición sobre el vendedor ajeno (BR-009).

**Sprint 2 (no autorizado en esta versión):** módulo `Catalog` con `products` y `categories` (API-011..015, API-019..022). Media (API-017/018) en fase 05 con `spec.md` propio. Orders (API-023), Reviews, Favorites, Notifications y Chat en fases posteriores.

## 2. Modelo de datos del Sprint 1

| Colección / dueño | Campos e invariantes | Índices |
|---|---|---|
| `sellers` / Sellers | `_id` (`ObjectId` propio, es el `sellerId` público), `ownerUserId` (inmutable, referencia `users._id`), `displayName` (nombre visible del negocio, 1..100), `description` (1..500), `pickupPoint` (1..160), `status` (`PendingActivation` \| `Active` \| `Inactive` \| `Suspended` \| `Deleted`), `version` (`int64`, control de concurrencia optimista), `createdAt`/`updatedAt` (UTC). | `ownerUserId_unique` (único **parcial**: solo documentos con `ownerUserId`), `status_updatedAt_desc`. |
| `users` / Identity | Se añade `sellerId` (string hexadecimal, opcional) para navegación inversa. `roles` incorpora `Seller` tras la autoactivación. | Sin índices nuevos: la búsqueda es por `_id` y `supabaseUserId`. |

**Compatibilidad con `sellers` existentes:** `displayName` y `status` ya existen y conservan su significado, por lo que `GET /api/v1/sellers/{sellerId}` (API‑008) sigue leyendo el mismo documento sin migración. Los documentos históricos sin `ownerUserId` no son visibles en las rutas nuevas (no tienen propietario verificable) y no bloquean el índice parcial.

**Estados y visibilidad:** `PendingActivation` = perfil creado pero rol todavía no promovido → no visible en API‑008 y no completable; `Active` = único estado visible como vendedor; `Inactive` sigue visible en API‑008 con `isActive: false`; `Suspended` y `Deleted` no se publican y no se reactivan por API‑009.

**Secuencia de activación (sin transacción multi‑documento):** (1) *upsert* de `sellers` con `status = "PendingActivation"`; (2) promoción del rol `Seller` vía contrato de `Identity`; (3) `status = "Active"`. Si (2) falla, la respuesta es `503` y el vendedor queda invisible; un reintento completa la activación sin duplicar documento ni rol. La promoción usa `$addToSet` y el *upsert* filtra por `ownerUserId`, por lo que ambos pasos son idempotentes.

**Escritura concurrente:** `PATCH /api/v1/sellers/me` actualiza con filtro por `ownerUserId`, `version` esperada y `status = "Active"`; si no casa, se distingue conflicto (`409`) de vendedor inexistente (`404`) releyendo el documento.

## 3. Contratos HTTP del Sprint 1

| Ficha | Método y ruta | Acceso | Códigos |
|---|---|---|---|
| API-009 | `POST /api/v1/sellers/activation-requests` | Bearer + cuenta `Active` | `201` creado · `200` ya activado (idempotente) · `400` `VALIDATION_ERROR` · `401` `UNAUTHORIZED` · `403` `SELLER_ACCOUNT_NOT_ACTIVE` · `404` `PROFILE_NOT_FOUND` · `409` `SELLER_STATE_CONFLICT` · `429` `RATE_LIMITED` · `503` `SELLER_UNAVAILABLE` |
| DEC-019.d | `GET /api/v1/sellers/me` | Bearer | `200` · `401` · `404` `SELLER_PROFILE_NOT_FOUND` · `429` · `503` |
| API-010 | `PATCH /api/v1/sellers/me` | Bearer + propietario implícito | `200` · `400` `VALIDATION_ERROR` · `401` · `404` `SELLER_PROFILE_NOT_FOUND` · `409` `SELLER_CONFLICT` · `429` · `503` |

Detalle de cuerpos, validaciones y ejemplo de respuesta en `spec.md` § 5. El cuerpo de éxito es el mismo en las tres rutas: `sellerId`, `businessName`, `description`, `pickupPoint`, `status`, `isActive`, `roles`, `createdAt`, `updatedAt`. No se expone `email`, `phone`, `supabaseUserId` ni `version`.

**Contratos no tocados:** API‑008 (`GET /api/v1/sellers/{sellerId}`) mantiene su proyección pública de cinco campos; API‑001..008 y API‑039 no cambian de comportamiento ni de forma de payload.

## 4. Operaciones internas y seguridad

- **Autorización:** el `sellerId` objetivo se deriva de `ownerUserId` (perfil local) y nunca del cuerpo ni de la query. Ninguna ruta de escritura acepta identificadores del cliente.
- **Privilegios:** el rol `Seller` lo asigna el backend con `$addToSet` (BR‑009); el cliente nunca envía `roles`, `sellerId`, `status` ni `version`, y el parser de `PATCH` rechaza campos desconocidos con `400`.
- **Rate limiting distribuido (fail‑closed):** políticas `seller-activation` (5/900 s), `seller-write` (30/60 s) y `seller-me` (60/60 s), particionadas por `users._id` y hasheadas con SHA‑256 en `ratelimit:{policy}:{hash}`; nunca se guarda correo ni dato personal en claro. Si Redis no responde, las rutas de vendedor responden `503` sin ejecutar la operación.
- **Errores:** contrato único `{ correlationId, status, code, message }`; sin mensajes de excepción, URIs, tokens ni `jti`. `503` no revela si falló MongoDB o Redis.
- **Logs:** sin payloads sensibles; solo códigos de resultado y `correlationId` implícito en el traza de la petición.
- **Cero recursos nuevos:** el Sprint no altera Terraform, ni introduce servicios pagados, ni SDK de AWS (Media sigue en fase 05).

## 5. Verificación

| Nivel | Cobertura mínima |
|---|---|
| Unitarias | Activación crea y devuelve `sellerId` del servidor; idempotencia (`200` sin duplicar documento ni rol); promoción fallida deja `PendingActivation` y `503`; cuenta no `Active` → `403`; validación de campos; `PATCH` conserva una sola versión ante concurrencia (`409`); lectura propia `404` si no es vendedor; `PendingActivation` no visible en API‑008. |
| Integración | `POST`/`GET`/`PATCH /api/v1/sellers/me` con `WebApplicationFactory` y dobles: `201`, `200`, `400`, `401`, `403`, `404`, `409`, `429`, `503`; forma exacta del JSON de éxito y del contrato de error. |
| Seguridad | Token ausente/inválido/expirado/revocado → `401`; cuenta no activa → `403` sin crear vendedor; campos privilegiados (`roles`, `sellerId`, `status`, `version`) rechazados; query no altera la identidad consultada; no se filtran datos de otro vendedor ni campos privados; rate limit agotado y Redis caído → `429`/`503` sin operación. |
| Operativa | Build Release 0 errores · `dotnet format --verify-no-changes` · las tres suites en verde · `docker compose config` · smoke local (`/health` y rutas de vendedor) · CI‑1 verde en la PR · CD‑1 y evidencia de despliegue en la EC2 tras el merge. |

## 6. Gates de estado

1. `spec.md` v2.1, este `plan.md` v2.1 y `task.md` v2.1 alineados y aprobados (DEC‑019 en `docs/constitution.md` v2.5) → **cumple**.
2. El código de producción solo puede usar los contratos de § 3; ninguna ruta extra → **verificable en la PR**.
3. El Sprint 2 (catálogo, categorías y media) **requiere aprobación propia** antes de escribir código.
4. La evidencia de despliegue en la EC2 se cierra tras el merge y la ejecución de CD‑1; hasta entonces no se afirma disponibilidad productiva de las rutas nuevas.
