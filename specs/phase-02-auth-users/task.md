# task.md — Tareas atómicas: Autenticación y Usuarios · Backend UTTT-GO
> Fase: `phase-02-auth-users` · Módulo: `IdentityAndUsers`
> Versión: 1.0 · Estado: **Aprobado** · Fecha: 2026-10-29
> Deriva de `specs/phase-02-auth-users/spec.md` v1.0 y `specs/phase-02-auth-users/plan.md` v1.1.
> Cada tarea está dimensionada a **20–30 minutos**. No se escribe código de producción fuera de estas tareas.

---

## Convenciones

- Formato: `- [ ] TASK-AUTH-xxx — Título`.
- `RF:` requisitos de `spec.md` cubiertos.
- `Hecho cuando:` condición única, explícita y verificable (comando, prueba o archivo).
- Una tarea no se marca hasta cumplir su "Hecho cuando" y dejar build/tests en verde.
- Las tareas se ejecutan en el orden listado salvo que se indique dependencia alternativa.

---

## Bloque 0 — Preparación

- [ ] **TASK-AUTH-001 — Crear estructura del módulo `IdentityAndUsers`**
  - RF: RF-03, RF-04, RF-05, RF-07, RF-08, RF-09, RF-11
  - Depende de: fase 00 (esqueleto base).
  - Hecho cuando: existen las carpetas `Endpoints`, `Application`, `Domain`, `Infrastructure`, `Contracts`, `Validators` bajo `src/UtttGo.Api/Modules/IdentityAndUsers/`, con `DependencyInjection.cs` registrado en `Program.cs`, y `dotnet build -c Release` compila sin errores.

- [ ] **TASK-AUTH-002 — Definir opciones tipadas del módulo**
  - RF: RF-01, RF-07
  - Depende de: TASK-AUTH-001.
  - Hecho cuando: existen `IdentityAndUsersOptions` (`AllowedEmailDomain`, `DefaultPageSize`, `MaxPageSize`) y `SupabaseOptions` enlazadas desde configuración, con validación al arranque que falla si falta `AllowedEmailDomain`.

---

## Bloque 1 — Dominio, modelos MongoDB y DTOs con FluentValidation

- [ ] **TASK-AUTH-003 — Crear enums de dominio**
  - RF: RF-03, RF-08, RF-09, RF-11
  - Hecho cuando: `UserRole` (`Buyer`, `Seller`, `Admin`), `AccountStatus` (`Active`, `Suspended`, `Deleted`) y `SellerStatus` (`Active`, `Inactive`) compilan y tienen prueba unitaria que fija sus valores numéricos.

- [ ] **TASK-AUTH-004 — Implementar POCO `UserProfile` y class map**
  - RF: RF-03, RF-04, RF-11
  - Hecho cuando: `UserProfile` contiene los campos de `plan.md` §3.1 (`supabaseUserId`, `email`, `normalizedEmail`, `displayName`, `phone`, `avatarMediaAssetId`, `role`, `status`, fechas UTC y campos de suspensión/baja), y una prueba de serialización BSON conserva `_id`, enums como string y fechas UTC.

- [ ] **TASK-AUTH-005 — Implementar POCO `SellerProfile`**
  - RF: RF-05
  - Hecho cuando: `SellerProfile` contiene `userId`, `businessName`, `description`, `pickupPoint`, `status`, `activatedAt` y `updatedAt`, y una prueba de serialización BSON redondea el documento sin pérdida.

- [ ] **TASK-AUTH-006 — Declarar índices idempotentes**
  - RF: RF-03, RF-07
  - Depende de: TASK-AUTH-004, TASK-AUTH-005.
  - Hecho cuando: `MongoIndexes.EnsureIndexesAsync` crea los índices de `plan.md` §3.3 (único `supabaseUserId`, único `normalizedEmail`, compuesto `status+role+createdAt`, único `sellerProfiles.userId`) y ejecutarlo dos veces no lanza error.

- [ ] **TASK-AUTH-007 — Implementar `IUserRepository` y `MongoUserRepository` (lectura/creación)**
  - RF: RF-03, RF-04, RF-07
  - Depende de: TASK-AUTH-006.
  - Hecho cuando: existen `GetBySupabaseUserIdAsync`, `GetByIdAsync`, `CreateIdempotentAsync` (no duplica por `supabaseUserId`) y `ExistsByNormalizedEmailAsync`, con pruebas de integración contra MongoDB Testcontainer en verde.

- [ ] **TASK-AUTH-008 — Extender `IUserRepository` (actualización y listado paginado)**
  - RF: RF-04, RF-07, RF-08, RF-09, RF-11
  - Depende de: TASK-AUTH-007.
  - Hecho cuando: existen `UpdateProfileAsync`, `UpdateStatusAsync`, `AnonymizeAsync` y `QueryPagedAsync(search, role, status, page, pageSize)` con `totalItems` correcto y test de paginación/filtros en verde.

- [ ] **TASK-AUTH-009 — Implementar `ISellerProfileRepository` y su repositorio**
  - RF: RF-05
  - Depende de: TASK-AUTH-006.
  - Hecho cuando: existen `GetByUserIdAsync`, `CreateAsync` y `ExistsByUserIdAsync`; dos llamadas a `CreateAsync` para el mismo `userId` no crean duplicados y la prueba lo verifica.

- [ ] **TASK-AUTH-010 — Definir DTOs de request y response**
  - RF: RF-01, RF-02, RF-03, RF-04, RF-05, RF-06, RF-07, RF-08, RF-09, RF-10, RF-11
  - Depende de: TASK-AUTH-003.
  - Hecho cuando: existen los registros de `plan.md` §4.5 (`RegisterRequest`, `LoginRequest`, `RecoverRequest`, `UpdateProfileRequest`, `ActivateSellerRequest`, `AdminUserQuery`, `AuthTokenResponse`, `UserProfileResponse`, `SellerProfileResponse`, `UserSummaryResponse`, `PagedResponse<T>`) y `dotnet build -c Release` compila.

- [ ] **TASK-AUTH-011 — Centralizar códigos de error del módulo**
  - RF: RF-01, RF-02, RF-06, RF-07, RF-08, RF-09, RF-10, RF-11
  - Depende de: TASK-AUTH-010.
  - Hecho cuando: `IdentityErrorCodes` expone los códigos de `plan.md` §4.6 y una prueba unitaria verifica que cada código mapea a su `HttpStatusCode` esperado.

- [ ] **TASK-AUTH-012 — Implementar `RegisterRequestValidator`**
  - RF: RF-01
  - Depende de: TASK-AUTH-010.
  - Hecho cuando: acepta `@uttt.edu.mx` exacto (case-insensitive, sin subdominios) y rechaza otros dominios, email inválido, password fuera de rango y `displayName` vacío; pruebas unitarias en verde.

- [ ] **TASK-AUTH-013 — Implementar `LoginRequestValidator` y `RecoverRequestValidator`**
  - RF: RF-02, RF-06
  - Depende de: TASK-AUTH-010.
  - Hecho cuando: ambos validadores exigen email con formato válido y password no vacío (login), y las pruebas unitarias cubren campos faltantes y formatos inválidos.

- [ ] **TASK-AUTH-014 — Implementar `UpdateProfileRequestValidator`**
  - RF: RF-04
  - Depende de: TASK-AUTH-010.
  - Hecho cuando: exige `displayName` no vacío, acepta `phone` y `avatarMediaAssetId` nulos, y rechaza `phone` con formato inválido; pruebas unitarias en verde.

- [ ] **TASK-AUTH-015 — Implementar `ActivateSellerRequestValidator`**
  - RF: RF-05
  - Depende de: TASK-AUTH-010.
  - Hecho cuando: exige `businessName`, `description` y `pickupPoint` no vacíos con longitudes máximas definidas, y las pruebas unitarias cubren cada campo faltante.

---

## Bloque 2 — Servicios de Aplicación e integración con Supabase Auth y Redis

- [ ] **TASK-AUTH-016 — Definir `IAuthProvider` e implementar `SupabaseAuthProvider` (sign up / sign in)**
  - RF: RF-02, RF-03
  - Depende de: TASK-AUTH-002, TASK-AUTH-010.
  - Hecho cuando: `IAuthProvider.SignUpAsync` y `SignInAsync` usan únicamente Supabase Auth y devuelven `sub` y tokens; una prueba con `HttpMessageHandler` falso verifica request/response y no se registra la contraseña en logs.

- [ ] **TASK-AUTH-017 — Añadir recuperación de contraseña a `IAuthProvider`**
  - RF: RF-06
  - Depende de: TASK-AUTH-016.
  - Hecho cuando: `RecoverAsync` invoca el endpoint de recuperación de Supabase y responde de forma uniforme; prueba con handler falso verifica que un correo inexistente no cambia la respuesta.

- [ ] **TASK-AUTH-018 — Implementar `ITokenBlacklist` y `RedisTokenBlacklist`**
  - RF: RF-10
  - Depende de: TASK-AUTH-002.
  - Hecho cuando: `RevokeAsync(jti, ttl)` escribe `revoked:{jti}` con TTL igual al restante hasta `exp` e `IsRevokedAsync` lo detecta; prueba de integración con Redis Testcontainer en verde y no-op cuando `ttl <= 0`.

- [ ] **TASK-AUTH-019 — Implementar `AuthService.RegisterAsync`**
  - RF: RF-01, RF-03
  - Depende de: TASK-AUTH-012, TASK-AUTH-016, TASK-AUTH-007.
  - Hecho cuando: registra en Supabase, crea el perfil local idempotente con rol `Buyer` y estado `Active`; un segundo registro con el mismo `sub` no duplica y devuelve el perfil existente; pruebas unitarias en verde.

- [ ] **TASK-AUTH-020 — Implementar `AuthService.LoginAsync`**
  - RF: RF-02, RF-08
  - Depende de: TASK-AUTH-013, TASK-AUTH-016, TASK-AUTH-007.
  - Hecho cuando: autentica contra Supabase, rechaza `Status = Deleted`, devuelve `403 ACCOUNT_SUSPENDED` si `Status = Suspended` y tokens en caso contrario; pruebas unitarias en verde.

- [ ] **TASK-AUTH-021 — Implementar `AuthService.RecoverAsync`**
  - RF: RF-06
  - Depende de: TASK-AUTH-013, TASK-AUTH-017.
  - Hecho cuando: siempre responde aceptación uniforme, sin consultar ni revelar la existencia del correo, y la prueba unitaria confirma el mismo resultado para correo existente e inexistente.

- [ ] **TASK-AUTH-022 — Implementar `AuthService.LogoutAsync`**
  - RF: RF-10
  - Depende de: TASK-AUTH-018.
  - Hecho cuando: revoca el `jti` del token actual con TTL hasta `exp`; si falta `jti` o Redis falla, lanza el error que el middleware traduce a `401`/`503` (fail-closed); prueba unitaria en verde.

- [ ] **TASK-AUTH-023 — Implementar `UserProfileService` (consulta y actualización)**
  - RF: RF-04, RF-13
  - Depende de: TASK-AUTH-014, TASK-AUTH-007, `IMediaAssetValidator`.
  - Hecho cuando: `GetMeAsync` devuelve el perfil por `sub` y `UpdateMeAsync` actualiza solo campos permitidos; un `avatarMediaAssetId` no aprobado produce `422 MEDIA_ASSET_NOT_APPROVED`; pruebas unitarias en verde.

- [ ] **TASK-AUTH-024 — Implementar baja lógica/anonimización de cuenta**
  - RF: RF-11
  - Depende de: TASK-AUTH-008.
  - Hecho cuando: la baja marca `Status = Deleted`, anonimiza `displayName`, `phone` y `avatarMediaAssetId`, conserva `email` para bloquear re-registro y no borra el documento; prueba unitaria verifica que no existe borrado duro.

- [ ] **TASK-AUTH-025 — Implementar `SellerActivationService`**
  - RF: RF-05
  - Depende de: TASK-AUTH-015, TASK-AUTH-009, TASK-AUTH-008.
  - Hecho cuando: crea `sellerProfiles` 1:1, cambia `users.role` a `Seller` y es idempotente; una segunda activación responde `409 SELLER_ALREADY_ACTIVE`; pruebas unitarias en verde.

- [ ] **TASK-AUTH-026 — Implementar `AdminUserService` (listado)**
  - RF: RF-07
  - Depende de: TASK-AUTH-008.
  - Hecho cuando: lista usuarios con `search`, `role`, `status`, `page` y `pageSize`, aplica el máximo configurado y devuelve `PagedResponse`; prueba unitaria de filtros/paginación en verde.

- [ ] **TASK-AUTH-027 — Implementar `AdminUserService` (suspender/reactivar) + auditoría**
  - RF: RF-08, RF-09
  - Depende de: TASK-AUTH-026.
  - Hecho cuando: suspender cambia a `Suspended` y reactivar a `Active`; auto-suspensión responde `409 SELF_SUSPENSION`, reactivar una cuenta no suspendida responde `409 INVALID_STATE_TRANSITION`, y se registra auditoría con actor, acción, timestamp y `correlationId` sin datos sensibles.

---

## Bloque 3 — Endpoints HTTP y Middlewares

- [ ] **TASK-AUTH-028 — Configurar `AddJwtBearer` para Supabase**
  - RF: RF-02, RF-10
  - Depende de: TASK-AUTH-002.
  - Hecho cuando: la autenticación valida issuer, audience, expiración y firma; `MapInboundClaims = false` y `NameClaimType = "sub"`; un JWT válido de prueba se autentica y uno inválido se rechaza con `401` y contrato de error.

- [ ] **TASK-AUTH-029 — Implementar `JwtRevocationMiddleware`**
  - RF: RF-10
  - Depende de: TASK-AUTH-018, TASK-AUTH-028.
  - Hecho cuando: rechaza `401 TOKEN_REVOKED` si `jti` está en Redis, exige `jti` en tokens autenticados y responde `503 DEPENDENCY_UNAVAILABLE` si Redis falla (fail-closed); pruebas de integración en verde.

- [ ] **TASK-AUTH-030 — Implementar `LocalProfileMiddleware`**
  - RF: RF-03, RF-08
  - Depende de: TASK-AUTH-028, TASK-AUTH-007.
  - Hecho cuando: carga el perfil por `sub`, añade el rol desde MongoDB (nunca del JWT), responde `403 ACCOUNT_SUSPENDED` para suspendidos y `401` para perfiles inexistentes o eliminados; pruebas de integración en verde.

- [ ] **TASK-AUTH-031 — Implementar rate limiting distribuido en Redis**
  - RF: RF-02, RF-10
  - Depende de: TASK-AUTH-002, TASK-AUTH-018.
  - Hecho cuando: `AuthPolicy` y `GeneralPolicy` usan claves `ratelimit:{policy}:{partición}`, exceder el límite devuelve `429 RATE_LIMITED` con contrato de error y Redis caído en `/auth/*` responde `503` (fail-closed).

- [ ] **TASK-AUTH-032 — Implementar endpoints `/api/v1/auth/*`**
  - RF: RF-01, RF-02, RF-06, RF-10
  - Depende de: TASK-AUTH-019, TASK-AUTH-020, TASK-AUTH-021, TASK-AUTH-022.
  - Hecho cuando: `register`, `login`, `recover` y `logout` responden con los códigos de `plan.md` §4.2 y sus DTOs; pruebas de integración de los cuatro endpoints en verde.

- [ ] **TASK-AUTH-033 — Implementar endpoints `/api/v1/users/me`**
  - RF: RF-04, RF-11
  - Depende de: TASK-AUTH-023, TASK-AUTH-024, TASK-AUTH-030.
  - Hecho cuando: `GET`, `PATCH` y `POST /delete` operan solo sobre el perfil del `sub` autenticado, respetan códigos de `plan.md` §4.3 y un usuario no puede modificar otro perfil; pruebas de integración en verde.

- [ ] **TASK-AUTH-034 — Implementar endpoint `/api/v1/sellers/activate`**
  - RF: RF-05
  - Depende de: TASK-AUTH-025, TASK-AUTH-030.
  - Hecho cuando: la autoactivación responde `201 SellerProfileResponse` con perfil completo, `422` con campos faltantes y `409` en segunda activación; pruebas de integración en verde.

- [ ] **TASK-AUTH-035 — Implementar endpoints `/api/v1/admin/users/*`**
  - RF: RF-07, RF-08, RF-09
  - Depende de: TASK-AUTH-026, TASK-AUTH-027, TASK-AUTH-030.
  - Hecho cuando: listar/suspender/reactivar exigen rol `Admin` desde MongoDB, respetan códigos de `plan.md` §4.4 y un no-admin recibe `403`; pruebas de integración en verde.

- [ ] **TASK-AUTH-036 — Integrar middleware y orden de pipeline**
  - RF: RF-02, RF-08, RF-10
  - Depende de: TASK-AUTH-029, TASK-AUTH-030, TASK-AUTH-031.
  - Hecho cuando: `Program.cs` aplica el orden de `plan.md` §5.6 (`CorrelationId → ExceptionHandler → Https → CORS → RateLimiter → Authentication → JwtRevocation → LocalProfile → Authorization → Endpoints`) y una prueba de humo confirma `401` sin token y `200` con token válido.

- [ ] **TASK-AUTH-037 — Homologar el contrato único de error en el módulo**
  - RF: RF-01, RF-04, RF-05, RF-06, RF-07, RF-08, RF-09, RF-10, RF-11
  - Depende de: TASK-AUTH-011, TASK-AUTH-032..035.
  - Hecho cuando: todas las respuestas de error del módulo incluyen `correlationId`, `status`, `code`, `message` y `details?` con la forma de `plan.md` §4.6; prueba de contrato recorre al menos un error de cada endpoint.

---

## Bloque 4 — Pruebas Unitarias, de Integración y de Seguridad

- [ ] **TASK-AUTH-038 — Pruebas unitarias de validadores de autenticación**
  - RF: RF-01, RF-02, RF-06
  - Depende de: TASK-AUTH-012, TASK-AUTH-013.
  - Hecho cuando: las pruebas cubren dominio permitido/rechazado, email inválido, password inválido y recuperación uniforme, y `dotnet test` del proyecto unitario queda en verde.

- [ ] **TASK-AUTH-039 — Pruebas unitarias de validadores de perfil y vendedor**
  - RF: RF-04, RF-05
  - Depende de: TASK-AUTH-014, TASK-AUTH-015.
  - Hecho cuando: las pruebas cubren campos obligatorios, opcionales y longitudes, y el proyecto unitario queda en verde.

- [ ] **TASK-AUTH-040 — Pruebas unitarias de servicios**
  - RF: RF-03, RF-08, RF-09, RF-11
  - Depende de: TASK-AUTH-019..027.
  - Hecho cuando: las pruebas cubren registro idempotente, login con suspensión, baja lógica sin borrado duro y transiciones administrativas, y el proyecto unitario queda en verde.

- [ ] **TASK-AUTH-041 — Montar harness de integración (Testcontainers + `WebApplicationFactory`)**
  - RF: RF-01, RF-02, RF-03, RF-04, RF-05, RF-06, RF-07, RF-08, RF-09, RF-10, RF-11
  - Depende de: TASK-AUTH-036.
  - Hecho cuando: el harness levanta MongoDB 7 y Redis 7 con Testcontainers, inyecta `FakeSupabaseAuthProvider`, siembra perfiles con `TestDataFactory` y limpia claves `revoked:*`/`ratelimit:*` entre pruebas.

- [ ] **TASK-AUTH-042 — Pruebas de integración de autenticación**
  - RF: RF-01, RF-02, RF-03, RF-06, RF-10
  - Depende de: TASK-AUTH-041, TASK-AUTH-032.
  - Hecho cuando: cubren registro válido/ inválido, login ok/fallido, recuperación uniforme, logout y rechazo de token revocado; pruebas en verde (T-AUTH-01, 02, 03).

- [ ] **TASK-AUTH-043 — Pruebas de integración de usuarios, vendedores y admin**
  - RF: RF-04, RF-05, RF-07, RF-08, RF-09, RF-11
  - Depende de: TASK-AUTH-041, TASK-AUTH-033..035.
  - Hecho cuando: cubren perfil propio, autoactivación, listado paginado, suspender/reactivar y baja lógica; pruebas en verde (T-AUTH-07, 09, 10).

- [ ] **TASK-AUTH-044 — Pruebas de seguridad de JWT y revocación**
  - RF: RF-10
  - Depende de: TASK-AUTH-041.
  - Hecho cuando: cubren token ausente, expirado, firma inválida, sin `jti` y con `jti` en blacklist; todos devuelven `401` con contrato de error (T-AUTH-04).

- [ ] **TASK-AUTH-045 — Pruebas de seguridad de autorización por recurso y rol**
  - RF: RF-04, RF-07, RF-08, RF-09
  - Depende de: TASK-AUTH-041.
  - Hecho cuando: cubren `403` para no-admin en `/admin/*`, usuario que intenta editar perfil ajeno y suspensión que bloquea operaciones; pruebas en verde (T-AUTH-06).

- [ ] **TASK-AUTH-046 — Pruebas de seguridad de fail-closed, rate limit y enumeración**
  - RF: RF-06, RF-10
  - Depende de: TASK-AUTH-041.
  - Hecho cuando: con Redis detenido, logout y `/auth/*` responden `503`; exceder el límite responde `429`; y `recover` no revela existencia de correo (T-AUTH-05, 08).

---

## Bloque 5 — Cierre y trazabilidad

- [ ] **TASK-AUTH-047 — Actualizar matriz de trazabilidad**
  - RF: RF-01..RF-11
  - Depende de: TASK-AUTH-042..046.
  - Hecho cuando: `docs/traceability.md` mapea cada RF a endpoint, tarea y prueba, sin RF huérfano ni prueba sin RF.

- [ ] **TASK-AUTH-048 — Verificación final de calidad**
  - RF: RF-01..RF-11
  - Depende de: todas las anteriores.
  - Hecho cuando: `dotnet build -c Release`, `dotnet format --verify-no-changes` y las suites unitaria, integración y seguridad pasan en verde; no hay secretos en el diff.

- [ ] **TASK-AUTH-049 — Registrar cierre de la fase en `memoria.md`**
  - RF: RF-01..RF-11
  - Depende de: TASK-AUTH-048.
  - Hecho cuando: `memoria.md` registra el estado final de la fase, los RF cubiertos y cualquier desviación aprobada.

---

## Orden de ejecución

```
Bloque 0 → Bloque 1 → Bloque 2 → Bloque 3 → Bloque 4 → Bloque 5
```

Dentro de cada bloque, respetar el orden numérico. Las tareas de pruebas de seguridad (044–046) pueden paralelizarse tras TASK-AUTH-041.

---

## Criterios de cierre de la fase

- [ ] Las 49 tareas marcadas.
- [ ] RF-01 a RF-11 cubiertos por al menos una tarea y una prueba.
- [ ] T-AUTH-01 a T-AUTH-10 en verde.
- [ ] Build Release y `dotnet format` en verde.
- [ ] `docs/traceability.md` y `memoria.md` actualizados.
- [ ] Sin código de producción fuera de este `task.md` ni secretos en el repositorio.
