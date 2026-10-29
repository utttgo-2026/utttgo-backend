# plan.md — Arquitectura e Implementación: Autenticación y Usuarios · Backend UTTT-GO
> Fase: `phase-02-auth-users` · Módulo de implementación: `IdentityAndUsers`
> Versión: 1.1 · Estado: **Aprobado** · Fecha: 2026-10-29
> Deriva de `specs/phase-02-auth-users/spec.md` v1.0 y se subordina a `constitution.md` v2.1 y `AGENTS.md` v2.0.
> Este documento **no** contiene código de producción; describe estructura, contratos, integración y estrategia de pruebas.

---

## 1. Objetivo y Alcance del Plan

Materializar el spec `phase-02-auth-users` en el monolito modular `UtttGo.Api`:

- Módulo `IdentityAndUsers` con fronteras internas claras (Auth, Users, Sellers, Admin).
- Persistencia local de perfiles en MongoDB Atlas con índices justificados.
- Integración de Supabase Auth con `JwtBearer` y revocación de JWT vía Redis.
- Contratos HTTP `/api/v1/auth/*`, `/api/v1/users/*`, `/api/v1/sellers/*` y `/api/v1/admin/users/*`.
- Estrategia de pruebas xUnit + `WebApplicationFactory` + Testcontainers.
- Trazabilidad explícita de cada sección a los RF/RNF/CL del spec.

Fuera de este plan: código de producción, Terraform, despliegue, gestión de ofertas/productos/pedidos y definición final de DEC-003.

---

## 2. Estructura Interna del Módulo

Ruta raíz: `src/UtttGo.Api/Modules/IdentityAndUsers/`

```
IdentityAndUsers/
├── DependencyInjection.cs              # AddIdentityAndUsers(...)
├── Endpoints/
│   ├── AuthEndpoints.cs                # /api/v1/auth/*
│   ├── UserEndpoints.cs                # /api/v1/users/me
│   ├── SellerEndpoints.cs              # /api/v1/sellers/*
│   └── AdminUserEndpoints.cs           # /api/v1/admin/users/*
├── Application/
│   ├── Abstractions/
│   │   ├── IAuthProvider.cs            # Supabase: signUp, signIn, recover, signOut
│   │   ├── ITokenBlacklist.cs          # revoked:{jti}
│   │   ├── IUserRepository.cs
│   │   ├── ISellerProfileRepository.cs
│   │   └── IMediaAssetValidator.cs     # valida mediaAssetId Approved (Media)
│   ├── Services/
│   │   ├── AuthService.cs
│   │   ├── UserProfileService.cs
│   │   ├── SellerActivationService.cs
│   │   └── AdminUserService.cs
│   └── Mappings/
│       └── UserMappings.cs             # POCO ↔ DTO (mapeo explícito)
├── Domain/
│   ├── Entities/
│   │   ├── UserProfile.cs
│   │   └── SellerProfile.cs
│   ├── Enums/
│   │   ├── UserRole.cs                 # Buyer | Seller | Admin
│   │   ├── AccountStatus.cs            # Active | Suspended | Deleted
│   │   └── SellerStatus.cs             # Active | Inactive
│   └── Errors/
│       └── IdentityErrorCodes.cs       # códigos estables del contrato de error
├── Infrastructure/
│   ├── Persistence/
│   │   ├── MongoUserRepository.cs
│   │   ├── MongoSellerProfileRepository.cs
│   │   ├── MongoIndexes.cs             # EnsureIndexes al arrancar (idempotente)
│   │   └── UserClassMap.cs
│   ├── Supabase/
│   │   └── SupabaseAuthProvider.cs     # único punto que habla con Supabase Auth
│   └── Redis/
│       └── RedisTokenBlacklist.cs
├── Contracts/
│   ├── Requests/
│   │   ├── RegisterRequest.cs
│   │   ├── LoginRequest.cs
│   │   ├── RecoverRequest.cs
│   │   ├── UpdateProfileRequest.cs
│   │   ├── ActivateSellerRequest.cs
│   │   └── AdminUserQuery.cs
│   └── Responses/
│       ├── AuthTokenResponse.cs
│       ├── UserProfileResponse.cs
│       ├── SellerProfileResponse.cs
│       ├── UserSummaryResponse.cs
│       └── PagedResponse.cs
└── Validators/
    ├── RegisterRequestValidator.cs
    ├── LoginRequestValidator.cs
    ├── RecoverRequestValidator.cs
    ├── UpdateProfileRequestValidator.cs
    └── ActivateSellerRequestValidator.cs
```

Reglas de frontera:

- `Endpoints` solo orquestan request → service → response; cero lógica de negocio.
- `Application/Services` contienen reglas (dominio de email, transiciones de estado, autoactivación).
- `Infrastructure` implementa repositorios y clientes externos; ningún acceso directo a MongoDB/Redis fuera de aquí.
- `IdentityAndUsers` posee las colecciones `users` y `sellerProfiles`. No accede a colecciones de otros módulos.
- El avatar se valida contra `IMediaAssetValidator` (contrato publicado por `Media`), sin leer su colección.
- `Admin` no se asigna por API; se aprovisiona por seed/migración operada del lado servidor.

**Trazabilidad:** RF-03, RF-04, RF-05, RF-07, RF-08, RF-09, RF-11; RNF-03, RNF-05, RNF-12.

---

## 3. Modelo de Datos (MongoDB Atlas / POCOs)

### 3.1 Colección `users`

```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public sealed class UserProfile
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("supabaseUserId")]
    public string SupabaseUserId { get; set; } = default!;   // claim "sub"

    [BsonElement("email")]
    public string Email { get; set; } = default!;

    [BsonElement("normalizedEmail")]
    public string NormalizedEmail { get; set; } = default!;  // uppercase invariant

    [BsonElement("displayName")]
    public string DisplayName { get; set; } = default!;

    [BsonElement("phone")]
    public string? Phone { get; set; }

    [BsonElement("avatarMediaAssetId")]
    public string? AvatarMediaAssetId { get; set; }

    [BsonElement("role")]
    [BsonRepresentation(BsonType.String)]
    public UserRole Role { get; set; } = UserRole.Buyer;

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public AccountStatus Status { get; set; } = AccountStatus.Active;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }                  // UTC

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }                  // UTC

    [BsonElement("suspendedAt")]
    public DateTime? SuspendedAt { get; set; }

    [BsonElement("suspendedBy")]
    public ObjectId? SuspendedBy { get; set; }

    [BsonElement("deletedAt")]
    public DateTime? DeletedAt { get; set; }
}
```

`UserRole`: `Buyer = 0`, `Seller = 1`, `Admin = 2`.
`AccountStatus`: `Active = 0`, `Suspended = 1`, `Deleted = 2`.

### 3.2 Colección `sellerProfiles`

```csharp
public sealed class SellerProfile
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public ObjectId UserId { get; set; }                     // ref users._id

    [BsonElement("businessName")]
    public string BusinessName { get; set; } = default!;

    [BsonElement("description")]
    public string Description { get; set; } = default!;

    [BsonElement("pickupPoint")]
    public string PickupPoint { get; set; } = default!;

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public SellerStatus Status { get; set; } = SellerStatus.Active;

    [BsonElement("activatedAt")]
    public DateTime ActivatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
```

### 3.3 Índices e invariantes

| Colección | Índice | Tipo | Justificación |
|---|---|---|---|
| `users` | `{ supabaseUserId: 1 }` | Único | Idempotencia de creación de perfil y lookup por `sub`. |
| `users` | `{ normalizedEmail: 1 }` | Único | Evita duplicados (CL-02/CL-03); el correo no se reutiliza. |
| `users` | `{ status: 1, role: 1, createdAt: -1 }` | Compuesto | Listados/filtros administrativos paginados (RF-07). |
| `sellerProfiles` | `{ userId: 1 }` | Único | Relación 1:1 usuario↔vendedor; evita doble activación. |

Invariantes de aplicación:

- `SupabaseUserId`, `Email` y `NormalizedEmail` inmutables tras creación (cambio de email delegado a Supabase; fuera de alcance).
- `Status = Deleted` implica anonimización de `displayName`, `phone`, `avatarMediaAssetId` (RF-11); `email` se conserva para bloquear re-registro (CL-03) hasta DEC-003.
- `Role = Seller` exige `sellerProfiles.status = Active` (RF-05).
- `CreatedAt`/`UpdatedAt`/`SuspendedAt`/`DeletedAt` en UTC (RNF-14).
- Creación de perfil idempotente por `supabaseUserId` (reintentos de registro seguros).

**Trazabilidad:** RF-03, RF-04, RF-05, RF-07, RF-08, RF-11; RNF-03, RNF-08, RNF-14; CL-02, CL-03, CL-11.

---

## 4. Contrato Técnico de Endpoints

### 4.1 Convenciones

- Base: `/api/v1`; JSON camelCase; IDs como `string`.
- Validación con FluentValidation antes de tocar servicios.
- Autorización: `Buyer`/`Seller` para `/users/me` y `/sellers/*`; `Admin` para `/admin/*`.
- Errores: contrato único de `constitution.md` § 14.

### 4.2 Endpoints de autenticación

| Método | Ruta | Auth | Request DTO | Éxito | Errores |
|---|---|---|---|---|---|
| POST | `/api/v1/auth/register` | Público | `RegisterRequest { email, password, displayName }` | `201 UserProfileResponse` | `400`, `422 EMAIL_DOMAIN_NOT_ALLOWED`, `409 EMAIL_ALREADY_REGISTERED`, `429`, `503` |
| POST | `/api/v1/auth/login` | Público | `LoginRequest { email, password }` | `200 AuthTokenResponse` | `400`, `401 INVALID_CREDENTIALS`, `403 ACCOUNT_SUSPENDED`, `429`, `503` |
| POST | `/api/v1/auth/recover` | Público | `RecoverRequest { email }` | `202 Accepted` (uniforme) | `400`, `429`, `503` |
| POST | `/api/v1/auth/logout` | Autenticado | — | `204 No Content` | `401`, `503` (fail-closed) |

Flujo `register`: validar dominio → `IAuthProvider.SignUpAsync` (Supabase) → crear `users` (idempotente por `sub`) → responder perfil. Si la creación local falla, no se reintenta ciegamente: se registra y se responde `503` (el reintento vuelve a ser idempotente).

Flujo `login`: `IAuthProvider.SignInAsync` → validar perfil local (`status ≠ Deleted`) → si `Suspended` responder `403` → devolver tokens de Supabase. El backend no almacena la contraseña ni el refresh token.

### 4.3 Endpoints de usuario y vendedor

| Método | Ruta | Auth | Request DTO | Éxito | Errores |
|---|---|---|---|---|---|
| GET | `/api/v1/users/me` | Autenticado | — | `200 UserProfileResponse` | `401`, `403 ACCOUNT_SUSPENDED` |
| PATCH | `/api/v1/users/me` | Autenticado | `UpdateProfileRequest { displayName, phone?, avatarMediaAssetId? }` | `200 UserProfileResponse` | `400`, `422`, `401`, `403` |
| POST | `/api/v1/users/me/delete` | Autenticado | — | `202 Accepted` | `401`, `409 ALREADY_DELETED` |
| POST | `/api/v1/sellers/activate` | Autenticado | `ActivateSellerRequest { businessName, description, pickupPoint }` | `201 SellerProfileResponse` | `400`, `422`, `409 SELLER_ALREADY_ACTIVE`, `401` |

`PATCH /users/me`: `displayName` obligatorio (no vacío); `phone` opcional con formato; `avatarMediaAssetId` opcional, validado como `Approved` por `IMediaAssetValidator` (si no, `422`).

`/users/me/delete`: baja lógica; anonimiza campos personales y marca `Status = Deleted`; conserva historial (DEC-003 abierta).

`/sellers/activate`: autoactivación; crea `sellerProfiles` 1:1 y cambia `users.role = Seller` en una transacción lógica (orden: crear perfil → actualizar rol; idempotente).

### 4.4 Endpoints administrativos

| Método | Ruta | Auth | Request | Éxito | Errores |
|---|---|---|---|---|---|
| GET | `/api/v1/admin/users` | Admin | Query: `search?`, `role?`, `status?`, `page`, `pageSize` | `200 PagedResponse<UserSummaryResponse>` | `400`, `401`, `403` |
| PATCH | `/api/v1/admin/users/{id}/suspend` | Admin | — | `200 UserSummaryResponse` | `404`, `409 SELF_SUSPENSION`, `401`, `403` |
| PATCH | `/api/v1/admin/users/{id}/reactivate` | Admin | — | `200 UserSummaryResponse` | `404`, `409 INVALID_STATE_TRANSITION`, `401`, `403` |

`search` coincide por prefijo/contiene sobre `displayName` y `email` (case-insensitive, regex escapada). `pageSize` máximo configurable (default 20, máximo 100).

### 4.5 DTOs (forma)

```csharp
public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record RecoverRequest(string Email);
public sealed record UpdateProfileRequest(string DisplayName, string? Phone, string? AvatarMediaAssetId);
public sealed record ActivateSellerRequest(string BusinessName, string Description, string PickupPoint);
public sealed record AdminUserQuery(string? Search, string? Role, string? Status, int Page = 1, int PageSize = 20);

public sealed record AuthTokenResponse(string AccessToken, string RefreshToken, int ExpiresIn, string TokenType);
public sealed record UserProfileResponse(string Id, string Email, string DisplayName, string? Phone,
    string? AvatarMediaAssetId, string Role, string Status, string? SellerBusinessName, DateTime CreatedAt);
public sealed record SellerProfileResponse(string UserId, string BusinessName, string Description,
    string PickupPoint, string Status, DateTime ActivatedAt);
public sealed record UserSummaryResponse(string Id, string Email, string DisplayName, string Role,
    string Status, DateTime CreatedAt);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalItems);
```

### 4.6 Códigos de error del módulo

| Código HTTP | `code` | Caso |
|---|---|---|
| 400 | `VALIDATION_ERROR` | Formato inválido (FluentValidation). |
| 401 | `INVALID_CREDENTIALS` | Login fallido en Supabase. |
| 401 | `TOKEN_REVOKED` / `TOKEN_INVALID` | `jti` en blacklist o JWT inválido. |
| 403 | `ACCOUNT_SUSPENDED` | Perfil local `Suspended`. |
| 403 | `FORBIDDEN` | Rol insuficiente. |
| 404 | `USER_NOT_FOUND` | Recurso admin inexistente. |
| 409 | `EMAIL_ALREADY_REGISTERED` | Correo duplicado. |
| 409 | `SELLER_ALREADY_ACTIVE` | Segunda autoactivación. |
| 409 | `SELF_SUSPENSION` | Admin intenta suspenderse. |
| 409 | `INVALID_STATE_TRANSITION` | Reactivar cuenta no suspendida / cuenta ya eliminada. |
| 409 | `ALREADY_DELETED` | Baja repetida. |
| 422 | `EMAIL_DOMAIN_NOT_ALLOWED` | Dominio distinto de `@uttt.edu.mx`. |
| 422 | `MEDIA_ASSET_NOT_APPROVED` | Avatar no aprobado/inexistente. |
| 429 | `RATE_LIMITED` | Rate limit excedido. |
| 503 | `DEPENDENCY_UNAVAILABLE` | Redis/Mongo/Supabase caídos (fail-closed). |

Formato estándar de error (contrato único de `constitution.md` § 14):

```json
{
  "correlationId": "3f6c2b1e-9a4d-4c1e-8a77-0b2d5f9c1e42",
  "status": 422,
  "code": "EMAIL_DOMAIN_NOT_ALLOWED",
  "message": "El correo debe pertenecer al dominio @uttt.edu.mx.",
  "details": [
    { "field": "email", "reason": "dominio no permitido" }
  ]
}
```

Reglas: `correlationId` siempre presente; `code` estable y no traducido; `message` para desarrollador; `details` solo en validación; nunca se incluyen tokens, contraseñas, `jti` ni trazas internas.

**Trazabilidad:** RF-01, RF-02, RF-03, RF-04, RF-05, RF-06, RF-07, RF-08, RF-09, RF-10, RF-11; RNF-04, RNF-07, RNF-08, RNF-12, RNF-13; CL-01..CL-13.

---

## 5. Integración Supabase Auth + JwtBearer + Revocación Redis

### 5.1 Opciones tipadas

`SupabaseOptions`: `Url`, `JwtIssuer`, `JwtAudience`, `JwtSecret` (solo local/HS256) o `JwksUrl`, `ServiceRoleKey` (server-only, nunca al cliente). Valores desde variables de entorno/Key Vault (RNF-10).

### 5.2 Configuración de `AddJwtBearer`

- `Authority`/`MetadataAddress` = `${SUPABASE_URL}/auth/v1/.well-known/openid-configuration` cuando se use JWKS.
- `TokenValidationParameters`: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey = true`, `ClockSkew = 30s`.
- `Audience` por defecto `authenticated`.
- `MapInboundClaims = false`; `NameClaimType = "sub"`.
- En local se permite HS256 con `JwtSecret`; en Azure se prefiere JWKS/asimétrico.
- `JwtBearerEvents.OnAuthenticationFailed`: responder `401` con contrato de error y `correlationId`.

### 5.3 Revocación de JWT (`revoked:{jti}`)

- `ITokenBlacklist` con `IsRevokedAsync(jti)` y `RevokeAsync(jti, ttl)`.
- Implementación `RedisTokenBlacklist` con `StackExchange.Redis`.
- Clave `revoked:{jti}`; TTL = `exp - now`; si `exp` ya pasó, no-op.
- `JwtRevocationMiddleware` se ejecuta **después** de `UseAuthentication()` y **antes** de `UseAuthorization()`:
  1. Si no hay principal autenticado → continuar (endpoints públicos).
  2. Extraer `jti`; si falta → `401`.
  3. Consultar Redis; si `revoked` → `401 TOKEN_REVOKED`.
  4. Si Redis falla → **fail-closed** `503 DEPENDENCY_UNAVAILABLE` (RNF-02).
- Logout: `POST /auth/logout` revoca el `jti` del token actual y responde `204`; Redis caído → `503`.

### 5.4 Estado y rol desde MongoDB

- `LocalProfileMiddleware` (o `IClaimsTransformation`) carga el perfil por `sub`:
  - Sin perfil → `401` (identidad sin perfil local).
  - `Status = Suspended` → `403 ACCOUNT_SUSPENDED`.
  - `Status = Deleted` → `401`.
  - Añade el rol **desde MongoDB** como claim para las políticas `RequireRole`.
- El rol del JWT se ignora siempre (RNF-03).

### 5.5 Rate limiting distribuido

- Store Redis con contadores por ventana: clave `ratelimit:auth:{partición}` y `ratelimit:general:{partición}`.
- `AuthPolicy` más estricta en `/api/v1/auth/*`; `GeneralPolicy` para el resto.
- Fail-closed: error de Redis en `/auth/*` ⇒ `503`; en general ⇒ `503` con alerta.
- Respuesta `429 RATE_LIMITED` con contrato de error.

### 5.6 Orden de middleware en `Program.cs`

```
CorrelationId → ExceptionHandler → HttpsRedirection → CORS
→ RateLimiter → Authentication(JwtBearer) → JwtRevocation
→ LocalProfile → Authorization → Endpoints
```

No se altera este orden sin actualizar este plan y obtener aprobación de AGENT-BE-01/BE-04.

**Trazabilidad:** RF-02, RF-08, RF-10; RNF-01, RNF-02, RNF-03, RNF-04, RNF-10; CL-04, CL-05, CL-06, CL-08.

---

## 6. Seguridad y Reglas Duras Aplicadas

| Regla | Aplicación en el módulo |
|---|---|
| BR-009 / RNF-03 | `userId`/rol/estado se derivan del `sub` y MongoDB; nunca del body. |
| RNF-05 | Contraseñas solo viajan a Supabase; no se persisten ni loguean. |
| RNF-06 | `/auth/recover` responde `202` uniforme siempre. |
| RNF-10 | Secretos desde Key Vault/env; `ServiceRoleKey` solo backend. |
| RNF-09 | Auditoría de suspender/reactivar/eliminar con actor, acción, timestamp y `correlationId`, sin datos sensibles. |
| RNF-13 | Avatar solo `mediaAssetId` `Approved`; sin URLs arbitrarias. |
| Rate limit | Distribuido y fail-closed en `/auth/*`. |
| CORS | Orígenes explícitos; prohibido `*` en producción. |

---

## 7. Estrategia de Pruebas

Proyectos: `UtttGo.UnitTests`, `UtttGo.IntegrationTests`, `UtttGo.SecurityTests` (comandos de `AGENTS.md` § 1.3).

### 7.1 Unitarias (sin dependencias externas)

- `RegisterRequestValidator`: dominio `@uttt.edu.mx` exacto (RF-01/CL-01).
- `UpdateProfileRequestValidator`: nombre obligatorio; avatar opcional (RF-04).
- `ActivateSellerRequestValidator`: campos obligatorios (RF-05/CL-09).
- `UserProfileService`: anonimización en baja lógica (RF-11/CL-11).
- `AdminUserService`: transiciones suspender/reactivar y auto-suspensión (RF-08/RF-09/CL-07/CL-10).
- Mapeos POCO ↔ DTO.

### 7.2 Integración (`WebApplicationFactory` + Testcontainers)

- Contenedores: MongoDB 7 y Redis 7 reales por test run (Testcontainers).
- Supabase sustituido por `FakeSupabaseAuthProvider` (`IAuthProvider`) en CI; nunca se llama a Supabase real.
- Semilla de perfiles de prueba con `TestDataFactory`.
- Casos: registro dominio válido/ inválido, login ok/fallido, perfil propio, autoactivación, listado paginado, suspender/reactivar, baja lógica (T-AUTH-01..03, 07, 09, 10).

### 7.3 Seguridad (`UtttGo.SecurityTests`)

- JWT firmado con clave de prueba (`TokenHelper`): expirado, firma inválida, `jti` revocado, sin `jti` (T-AUTH-04).
- `403` en `/admin/*` para `Buyer`/`Seller`; usuario no edita perfil ajeno (T-AUTH-06/RNF-12).
- Suspensión bloquea operaciones; reactivación las restaura (RF-08/RF-09).
- Recuperación no revela existencia de correo (RF-06/RNF-06, T-AUTH-08).
- Fail-closed: detener Redis y verificar `503` en logout y `/auth/*` (T-AUTH-05/RNF-02).

### 7.4 Convenciones

- Nombres Given/When/Then derivados del spec.
- `--no-build` tras build Release; tests limpian claves `revoked:*` y `ratelimit:*`.
- Content Safety/Media y Supabase mockeados; sin servicios reales en CI.

**Trazabilidad:** T-AUTH-01..T-AUTH-10; RNF-01, RNF-02, RNF-04, RNF-06, RNF-12, RNF-13; CL-01..CL-13.

---

## 8. Fases de Implementación (orden sugerido)

| Fase | Contenido | Depende de |
|---|---|---|
| F1 | Esqueleto del módulo + `DependencyInjection` + registro en `Program.cs` | Fase 00 (esqueleto base) |
| F2 | POCOs, índices e `IUserRepository`/`ISellerProfileRepository` | F1 |
| F3 | Configuración `SupabaseOptions` + `AddJwtBearer` + `IAuthProvider` | F1 |
| F4 | `RedisTokenBlacklist`, `JwtRevocationMiddleware`, `LocalProfileMiddleware` | F3 |
| F5 | Validadores + `AuthService`/`UserProfileService` + endpoints `/auth/*` y `/users/me` | F2, F4 |
| F6 | `SellerActivationService` + `/sellers/activate` | F5 |
| F7 | `AdminUserService` + endpoints `/admin/users/*` + auditoría | F5 |
| F8 | Rate limiting distribuido `/auth/*` y general | F4 |
| F9 | Tests unitarios, integración y seguridad; trazabilidad | F5–F8 |

Sin código de producción hasta que esta fase se ejecute vía `task.md`.

---

## 9. Riesgos y Decisiones Diferidas

| Riesgo | Mitigación |
|---|---|
| Supabase HS256 vs JWKS según proyecto | Soportar ambos; JWKS por defecto en Azure, secreto solo local. |
| Redis caído bloquea login/logout | Fail-closed por diseño; health check y alertas; reintentos cortos. |
| Transacción usuario↔vendedor no atómica en MongoDB standalone | Orden idempotente + reintento; en Atlas usar transacción si el clúster lo permite. |
| Re-registro con correo de cuenta eliminada | Bloqueo por índice único y `Status = Deleted` hasta DEC-003. |
| Enumeración de correos | Respuesta uniforme en register/recover. |
| Asignación de `Admin` | Solo seed/migración server-side; jamás por API. |
| DEC-003 abierta | Anonimización reversible mínima y campos a conservar documentados; se ajusta al cerrar DEC-003. |

---

## 10. Fuera de Alcance del Plan

- Código de producción, migraciones EF, SQL, carrito, menús u horarios.
- Terraform, pipelines y despliegue.
- Verificación de email, OAuth, magic link, 2FA y cambio de contraseña/email en backend.
- Aprobación administrativa de vendedores (DEC-002 resuelta: autoactivación MVP).
- Definición final de retención/anonimización (DEC-003).
- Gestión de ofertas, productos, pedidos, reseñas, chat y notificaciones.

---

## 11. Matriz de Trazabilidad Consolidada

| Sección del plan | RF | RNF | CL | Tests |
|---|---|---|---|---|
| § 2 Estructura del módulo | RF-03..05, 07..09, 11 | RNF-03, 05, 12 | — | — |
| § 3 Modelo de datos | RF-03..05, 07..09, 11 | RNF-03, 08, 14 | CL-02, CL-03, CL-11 | T-AUTH-03, 07, 10 |
| § 4 Contratos HTTP | RF-01..11 | RNF-04, 07, 08, 12, 13 | CL-01..CL-13 | T-AUTH-01..10 |
| § 5 Supabase/JwtBearer/Redis | RF-02, 08, 10 | RNF-01..04, 10 | CL-04..CL-08 | T-AUTH-02, 04, 05, 06 |
| § 6 Seguridad | RF-01, 04, 06, 11 | RNF-03, 05, 06, 09, 10, 13 | CL-01, CL-12, CL-13 | T-AUTH-01, 06, 08 |
| § 7 Pruebas | RF-01..11 | RNF-01..14 | CL-01..CL-13 | T-AUTH-01..10 |

### 11.1 Trazabilidad RF → sección del plan → prueba

| RF | Requisito (resumen) | Secciones del plan | Pruebas |
|---|---|---|---|
| RF-01 | Registro solo con `@uttt.edu.mx` | §4.2, §4.6, §6 | T-AUTH-01 |
| RF-02 | Login email+password vía Supabase | §4.2, §5.2, §5.5 | T-AUTH-02 |
| RF-03 | Perfil local con rol `Buyer` y `sub` | §2, §3.1, §4.2, §5.4 | T-AUTH-03 |
| RF-04 | Perfil: nombre/email obligatorios, teléfono/avatar opcionales | §2, §3.1, §4.3, §6 | T-AUTH-03, T-AUTH-06 |
| RF-05 | Autoactivación de vendedor sin aprobación admin | §2, §3.2, §4.3, §7.1 | T-AUTH-09 |
| RF-06 | Recuperación por email sin enumeración | §4.2, §4.6, §6 | T-AUTH-08 |
| RF-07 | Admin lista/busca/pagina usuarios | §2, §3.3, §4.4 | T-AUTH-07 |
| RF-08 | Suspensión bloquea operaciones | §4.4, §5.4, §6 | T-AUTH-06, T-AUTH-07 |
| RF-09 | Reactivación restaura operaciones | §4.4, §5.4, §7.1 | T-AUTH-07 |
| RF-10 | Logout revoca `jti` y falla cerrado | §4.2, §5.3, §5.5 | T-AUTH-04, T-AUTH-05 |
| RF-11 | Baja lógica/anonimización sin borrado duro | §3.1, §4.3, §6, §9 | T-AUTH-10 |

Cada RF del spec queda cubierto al menos por una sección de implementación y una prueba.

---

## 12. DoD del Plan

- [ ] Estructura de § 2 creada y compilando.
- [ ] POCOs e índices de § 3 implementados y `EnsureIndexes` idempotente.
- [ ] Contratos de § 4 implementados con DTOs y contrato de error único.
- [ ] JwtBearer + revocación Redis de § 5 con fail-closed verificado.
- [ ] Rate limiting distribuido activo en `/auth/*`.
- [ ] T-AUTH-01..T-AUTH-10 en verde.
- [ ] Sin secretos en el diff; configuración por entorno/Key Vault.
- [ ] Trazabilidad actualizada en `docs/traceability.md`.
- [ ] `memoria.md` actualizado con el estado de la fase.

---

## 13. Aprobación y Control de Cambios

| Campo | Valor |
|---|---|
| Estado | **Aprobado** |
| Fecha | 2026-10-29 |
| Versión | 1.1 |
| Autor | AGENT-BE-01 (Architect) con AGENT-BE-02/BE-03 |
| Revisores | AGENT-BE-04 (Security Reviewer), AGENT-BE-06 (Memory Keeper) |
| Aprobado por | Equipo UTTT-GO |
| Próxima revisión | Al crear `task.md` o al cerrar DEC-003 |

### 13.1 Registro de versiones

| Versión | Fecha | Cambio | Aprobado por |
|---|---|---|---|
| 1.0 | 2026-10-29 | Plan de arquitectura, modelo de datos, contratos, integración Supabase/Redis, pruebas y trazabilidad | Equipo UTTT-GO |
| 1.1 | 2026-10-29 | Se añade ejemplo del formato estándar de error y matriz de trazabilidad RF-01..RF-11 → sección → prueba | Equipo UTTT-GO |

### 13.2 Reglas de control de cambios

1. Cambiar rutas, colecciones, índices o el orden de middleware exige nueva versión de este plan.
2. Toda desviación debe reflejarse primero en `spec.md` si altera requisitos.
3. Ningún agente implementa código de producción sin `task.md` derivado de este plan.
