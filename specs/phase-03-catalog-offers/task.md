# task.md — Autoactivación y perfil de vendedor (Sprint 1) · Backend UTTT-GO
> Fase `phase-03-catalog-offers` · Versión 2.1 · **Aprobada para el Sprint 1 (vendedor)** · 2026-11-08.
> Deriva de `spec.md` v2.1 → `plan.md` v2.1. Autoriza el Sprint 1 de vendedor (API‑009, API‑010 y ficha nueva `GET /api/v1/sellers/me`). El Sprint 2 (catálogo, categorías) permanece bloqueado. Las tareas se marcan solo con su "Hecho cuando" cumplido y pruebas verdes.

## Gates previos (documentales)

- [x] **TASK-CATALOG-001 — Aprobar spec del Sprint 1 de vendedor.** RF-V01..RF-V12. Hecho cuando: `spec.md` v2.1 aprobado con contratos, códigos HTTP, validaciones y casos de aceptación; sin producto canónico ni ofertas compartidas. **Cumplido 2026-11-08** (DEC‑019 en `docs/constitution.md` v2.5).
- [x] **TASK-CATALOG-003 — Resolver API-016 formalmente.** Hecho cuando: el propietario aprueba su retirada funcional sin alterar el Excel ni redirigir la ruta, y su función queda cubierta por API‑011. **Cumplido 2026-11-08** (DEC‑019.c); API‑016 no se implementa en ninguna versión.
- [x] **TASK-CATALOG-005 — Aprobar plan y dependencias del Sprint 1.** Hecho cuando: `plan.md` v2.1 y `task.md` v2.1 alineados con el spec, con fronteras de módulo, modelo de datos, rate limits y verificación. **Cumplido 2026-11-08**.
- [ ] **TASK-CATALOG-002 — Conciliar fichas de lectura y escritura del catálogo (Sprint 2).** Hecho cuando: API‑011..015 tienen payloads, queries, actores y permisos revisados contra el Excel sin cambiar método/ruta unilateralmente. **Bloqueado:** requiere aprobación propia del Sprint 2.
- [ ] **TASK-CATALOG-004 — Definir ficha de eliminación de producto (Sprint 2).** Hecho cuando: método/ruta, autorización, efectos en pedidos/media y pruebas están aprobados. **Bloqueado:** mientras tanto solo existe baja lógica interna (DEC‑019.e) y ninguna ruta `DELETE /products/*`.

## Sprint 1 — Implementación (autorizada)

### Datos y contratos
- [x] **TASK-SELLER-010 — Documento y escritura de `sellers`.** RF-V01/02/03/07/08. Depende de: 001/003/005. Hecho cuando: `SellerDocument` incorpora `ownerUserId`, `description`, `pickupPoint`, `version`, `createdAt`, `updatedAt` conservando `displayName`/`status`; existe escritor idempotente por `ownerUserId` (crear `PendingActivation`, activar, actualizar con `version`) e índice `ownerUserId_unique` **parcial** más `status_updatedAt_desc`, sin tocar los documentos históricos.
- [x] **TASK-SELLER-011 — Contratos de Identity para el módulo Sellers.** RF-V01/03/04/05. Depende de: 001/005. Hecho cuando: `IAuthenticatedLocalProfileReader` resuelve token a perfil local validado distinguiendo `Unauthorized`/`ProfileMissing`/`Unavailable` sin exigir `Active`, e `ISellerRolePromotion` asigna `Seller` y `users.sellerId` con `$addToSet` de forma idempotente; `Identity` no depende de `Sellers`.
- [x] **TASK-SELLER-012 — Contratos HTTP, DTOs y validadores.** RF-V09. Depende de: 001/005. Hecho cuando: existen `SellerActivationRequest`, `SellerOwnProfileResponse` y `UpdateSellerProfileRequest` con whitelist estricta (campos desconocidos, `null` y longitudes fuera de rango → `400`), validadores FluentValidation (1..100 / 1..500 / 1..160) y el contrato de error único.

### Servicios y rutas
- [x] **TASK-SELLER-020 — Servicio de autoactivación (API-009).** RF-V01..RF-V05. Depende de: 010/011/012. Hecho cuando: `SellerActivationService` crea y activa el vendedor con la secuencia documentada (upsert `PendingActivation` → promoción de rol → `Active`), es idempotente (`200` sin duplicar documento ni rol), responde `403` si la cuenta no está `Active`, `404` si no hay perfil local, `409` si el vendedor está `Suspended`/`Deleted` y `503` si una dependencia crítica falla.
- [x] **TASK-SELLER-021 — Lectura del perfil propio (ficha nueva).** RF-V06/11/12. Depende de: 010/012. Hecho cuando: `GET /api/v1/sellers/me` devuelve solo el perfil propio con su `sellerId`, responde `404` si el usuario no es vendedor y no expone `email`, `phone`, `supabaseUserId` ni `version`.
- [x] **TASK-SELLER-022 — Edición del perfil propio (API-010).** RF-V07/08/09. Depende de: 010/012. Hecho cuando: `PATCH /api/v1/sellers/me` aplica cambios solo sobre el perfil del usuario autenticado, incrementa `version` y responde `409` ante concurrencia sin sobrescribir silenciosamente; la ruta no acepta `sellerId`.
- [x] **TASK-SELLER-023 — Rate limiting distribuido fail-closed.** RF-V10. Depende de: 005. Hecho cuando: existen políticas `seller-activation` (5/900 s), `seller-write` (30/60 s) y `seller-me` (60/60 s) en `RateLimitingOptions`/`appsettings.json`, particionadas por `users._id` y hasheadas; Redis caído produce `503` sin ejecutar la operación.
- [x] **TASK-SELLER-024 — Registro DI e índices.** Depende de: 010/011/020/021/022/023. Hecho cuando: `Program.cs` registra servicios, validadores, gates, escritor, promoción e índices sin alterar el orden del middleware; los índices se crean al arrancar y un fallo no impide el arranque.

### Pruebas
- [x] **TASK-SELLER-030 — Unitarias.** Depende de: 020/021/022. Hecho cuando: `SellerActivationServiceTests`, `OwnSellerProfileServiceTests` y `UpdateOwnSellerProfileServiceTests` cubren creación, idempotencia, promoción fallida, cuenta no activa, validación, concurrencia y `PendingActivation` no visible.
- [x] **TASK-SELLER-031 — Integración.** Depende de: 020/021/022/023/024. Hecho cuando: `SellerActivationEndpointTests`, `OwnSellerProfileEndpointTests` y `UpdateOwnSellerProfileEndpointTests` verifican `201`, `200`, `400`, `401`, `403`, `404`, `409`, `429`, `503` y la forma exacta del JSON de éxito y del error.
- [x] **TASK-SELLER-032 — Seguridad.** Depende de: 030/031. Hecho cuando: `SellerActivationSecurityTests` y `UpdateOwnSellerProfileSecurityTests` verifican token ausente/inválido, cuenta no activa, campos privilegiados rechazados, identidad no alterable por query, no filtración de datos ajenos, rate limit agotado y Redis caído.

### Cierre
- [x] **TASK-SELLER-040 — Verificación local y CI.** Depende de: 030/031/032. Hecho cuando: `dotnet build -c Release` sin errores, `dotnet format --verify-no-changes` sin diferencias, las tres suites en verde, `docker compose config` válido y smoke local de `/health` y de las rutas de vendedor; CI‑1 en verde en la PR.
- [ ] **TASK-SELLER-041 — Evidencia de despliegue en la EC2.** Depende de: 040 y merge a `main`. Hecho cuando: CD‑1 despliega el SHA de la PR y la evidencia registra `/health` en `Healthy` y las rutas de vendedor en la instancia `3.129.142.54` con una cuenta real `@uttt.edu.mx` confirmada.
- [x] **TASK-SELLER-042 — Trazabilidad y memoria.** Depende de: 030/031/032. Hecho cuando: `memoria.md` registra DEC‑019, el mapa de colecciones con `sellers`/`users.sellerId` y el estado de la fase 03; `docs/traceability.md` actualiza API‑008, API‑009, API‑010 y la ficha nueva; `PROJECT_STATUS.md` refleja el despliegue en AWS y `/health` operativo.

## Sustituciones y alcance

TASK-CATALOG-001..059 y 060..064 de v1.x son **históricos**, no un segundo backlog implementable. Los IDs de este archivo son los de **v2.1**; no implican que se ejecutaran tareas anteriores con los mismos números. API‑016 y la eliminación pública de producto permanecen bloqueadas (DEC‑019.c/e). T‑CAT‑01..08 de v2.0 son escenarios del Sprint 2 y nunca pruebas ejecutadas; T‑CAT‑V01..V12 son los casos ejecutables de este Sprint.
