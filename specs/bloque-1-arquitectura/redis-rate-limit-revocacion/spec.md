# Redis: rate limit y revocación — spec

> Lote 1 · 2026-11-12 · **Pendiente de aprobación**; API-003/logout aún no existe.

Autoridad: [constitución v2.6](../../../docs/constitution.md) § 6 → esta ficha → [plan](plan.md) → [task](task.md). [Trazas ID](../base-modular/plan.md#correspondencia-exhaustiva-de-fase-00). [Diseño API](../../../skills/be-api-design.md) · [arquitectura](../../../skills/be-architecture.md) · [memoria](../../../skills/be-memory.md).

## Alcance y aceptación

REQ-007/008, AC-005/006: contrato compartido del gate JWT/revocación y rate limit Redis; caché degradable separada. La política exacta se remite a la constitución; esta ficha no implementa logout ni fija umbrales nuevos.

| Dado / cuando | Entonces / prueba necesaria |
|---|---|
| Token expirado/revocado o Redis caído al comprobar revocación | Rechazo; firma/issuer/audience/exp y perfil local deben verificarse independientemente. Test real Redis/JWT aún pendiente. |
| Se excede cuota `/auth/*` entre dos réplicas | `429` con error único y partición distribuida; sin Redis no se permite autenticar. Prueba entre réplicas pendiente. |
| Redis falla en rate limit general o caché | General `503` con alerta; caché consulta origen Mongo y registra degradación. Caso de falla real pendiente. |
| Se solicita logout (API-003) | Deberá escribir `revoked:{jti}` o, si falta `jti`, `revoked:{session_id}` con TTL hasta `exp`; aún no se implementa la escritura ni se marca completo. |

## Contrato vigente (código, no aprobación de políticas)

| Gate Redis | Archivo real y alcance |
|---|---|
| `RedisJwtGate` | [`src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisJwtGate.cs`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisJwtGate.cs): valida firma, issuer, audience, expiración y `sub`; consulta `revoked:{jti}` o `revoked:{session_id}` si falta `jti` (tokens Supabase sin `jti`); sin ambos, rechaza. Configura **JWKS (RS256/ES256) o secreto HS256, nunca ambos**; JWKS inaccesible no hace fallback. No escribe revocaciones. |
| `RedisAuthRateLimitGate` | [`src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisIdentityGates.cs`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisIdentityGates.cs): registro por IP; login por IP y correo normalizado. |
| `RedisPublicSellerRateLimitGate` | [`src/UTTTGO.Api/Modules/Sellers/Infrastructure/RedisPublicSellerRateLimitGate.cs`](../../../src/UTTTGO.Api/Modules/Sellers/Infrastructure/RedisPublicSellerRateLimitGate.cs): lectura pública de vendedor por IP. |
| `RedisAdminUsersRateLimitGate` | [`src/UTTTGO.Api/Modules/Admin/Infrastructure/RedisAdminUsersRateLimitGate.cs`](../../../src/UTTTGO.Api/Modules/Admin/Infrastructure/RedisAdminUsersRateLimitGate.cs): listado admin por ID local del actor, tras autorización. |

[`RateLimiting` en `src/UTTTGO.Api/appsettings.json`](../../../src/UTTTGO.Api/appsettings.json): **provisional, decidida por el responsable, no aprobada**. Políticas actuales (límite/ventana en segundos): `AuthRegister` 5/900; `AuthLoginIp` 10/900; `AuthLoginEmail` 5/900; `PublicSeller` 60/60; `AdminUsers` 30/60; `Health` 120/60; `SellerActivation` 5/900; `SellerWrite` 30/60; `SellerMe` 60/60. [`RedisRateLimiter`](../../../src/UTTTGO.Api/Shared/Infrastructure/RedisOptions.cs) guarda `ratelimit:{policy}:{SHA256(partición)}`; al agotar cuota produce `429` con `Retry-After` (segundos hasta reintento). Error de Redis en gates críticos → `503` **fail-closed**; esto describe código, no una prueba de caída en AWS.

## Evidencia

- [**Real en AWS: tres curl, fecha y límites**](../base-modular/spec.md#evidencia-en-aws-2026-11-12): sonda Redis `Healthy` y lecturas públicas que pasan por `RedisPublicSellerRateLimitGate`; no prueban `429`, blacklist ni Redis caído.
- `docs/qa-api-002.md` § Contrato candidato (2026-11-04): `LoginSecurityTests`, `LoginEndpointTests` prueban `429` y `503` **con dobles**, no rate limit distribuido/JWT criptográfico. `docs/qa-api-001.md`: `RegisterEndpointTests` comprueba fallo cerrado simulado.
- `memoria.md` entrada 2026-11-06 «Adaptadores Redis locales»: prueba manual de seis login con Redis local, cinco `503` por Supabase sin configurar y sexto `429` con `Retry-After`; no prueba blacklist, Redis caído ni dos réplicas.
- **Pendiente:** escritura/reutilización de `revoked:{jti}` o `revoked:{session_id}` (API-003), Redis caído con endpoint (fail-closed), `429` entre las dos réplicas, login y `/auth/me` en AWS. AC-005/006 siguen abiertos; no etiquetar estos escenarios como «Verificado».
