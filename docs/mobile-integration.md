# Integración móvil — UTTT-GO Backend

> Guía práctica para el equipo de desarrollo móvil. Los contratos funcionales
> oficiales siguen siendo las 42 fichas de
> `material/UTTT_GO_Punto_4_Especificacion_API_v1.xlsx` y los `spec.md` de cada
> fase; este documento describe el **entorno desplegado** y el **flujo real de
> autenticación** (corte: 2026-11-11).

## 1. URL base (HTTPS)

| Uso | URL |
|---|---|
| **Base de la API (HTTPS)** | `https://3.129.142.54.nip.io` |
| Documento OpenAPI (JSON) | `https://3.129.142.54.nip.io/openapi/v1.json` |
| Swagger UI (navegador) | `https://3.129.142.54.nip.io/openapi` |
| Sonda de salud | `https://3.129.142.54.nip.io/health` |

- El hostname `3.129.142.54.nip.io` lo resuelve **nip.io** (servicio gratuito)
  a la Elastic IP `3.129.142.54`: no hay que registrar ni pagar un dominio ni
  configurar DNS (costo AWS $0, DEC-018).
- El certificado TLS es de **Let's Encrypt**, emitido y renovado
  automáticamente en la instancia con Certbot (desafío `webroot`).
- El hostname por HTTP redirige a HTTPS (`301`). La IP desnuda
  (`http://3.129.142.54`) sigue sirviendo **solo HTTP**: la app móvil debe usar
  siempre la base HTTPS de la tabla (RNF-05: HTTPS en todos los entornos).
- `GET /openapi/v1.json` y la UI en `GET /openapi` son endpoints
  **operacionales de documentación**, igual que `GET /health`; no son fichas
  funcionales del inventario.

## 2. Contrato de error único

Todo error responde con el mismo cuerpo (camelCase):

```json
{
  "correlationId": "0HNP8GK2J9V8M:00000001",
  "status": 401,
  "code": "INVALID_CREDENTIALS",
  "message": "No se pudo iniciar sesión con esas credenciales."
}
```

- `correlationId` debe incluirse en cualquier reporte de bug.
- Nunca se devuelven detalles internos (stack trace, mensajes de Supabase/Mongo).

## 3. Registro — `POST /api/v1/auth/register`

Solo correos institucionales con dominio **exacto** `@uttt.edu.mx`.

```json
{
  "email": "estudiante@uttt.edu.mx",
  "password": "********",
  "displayName": "Nombre"
}
```

| Código | `code` | Cuándo |
|---|---|---|
| `202` | — | Solicitud válida y procesable. El cuerpo es siempre genérico: `{"message":"Si el registro puede procesarse, recibirás instrucciones para verificar tu correo."}` — **no** distingue correo nuevo ni existente (antienumeración). |
| `400` | `VALIDATION_ERROR` | Cuerpo inválido o ausente. |
| `413` | — | Cuerpo mayor a 8 KiB. |
| `422` | `INSTITUTIONAL_EMAIL_REQUIRED` | Dominio distinto de `@uttt.edu.mx`. |
| `429` | `RATE_LIMITED` | Rate limit de `/auth/*` en Redis. |
| `503` | `AUTH_UNAVAILABLE` | Supabase o Redis no responden (fail-closed). |

El `202` **no** garantiza que el correo haya sido entregado ni crea la cuenta
por sí solo: la identidad se crea en Supabase y el perfil local queda
`PendingEmailVerification` hasta el login posterior (ver § 5).

## 4. Login — `POST /api/v1/auth/login`

Petición:

```json
{
  "email": "estudiante@uttt.edu.mx",
  "password": "********"
}
```

Respuesta `200` (JSON real del backend):

```json
{
  "accessToken": "<jwt-de-supabase>",
  "expiresIn": 3600,
  "tokenType": "Bearer",
  "user": {
    "id": "690f1c2e8b3f2a0012ab34cd",
    "roles": ["Buyer"],
    "accountStatus": "Active"
  }
}
```

| Campo | Significado |
|---|---|
| `accessToken` | JWT de Supabase validado por el backend (firma, issuer, audience, expiración y blacklist `revoked:{jti}` en Redis). |
| `expiresIn` | Segundos de validez del access token (típicamente `3600`). No hay refresh token: al expirar, la app vuelve a autenticarse (API-003 pendiente). |
| `tokenType` | Siempre `"Bearer"`. |
| `user.id` | Id del **perfil local** en MongoDB (no es el `sub` de Supabase). |
| `user.roles` | Roles del perfil local (`Buyer`; `Seller` si se autoactivó). **Nunca** se confían roles del cliente ni del JWT. |
| `user.accountStatus` | `Active` en el login exitoso (el backend promueve `PendingEmailVerification` → `Active` en este primer login). |

Errores:

| Código | `code` | Cuándo |
|---|---|---|
| `400` | `VALIDATION_ERROR` | Cuerpo inválido. |
| `401` | `INVALID_CREDENTIALS` | Contraseña incorrecta, correo no confirmado en Supabase, dominio no institucional, perfil ausente/suspendido. **Mismo código genérico para todos** (antienumeración): la app no debe intentar distinguir el motivo. |
| `429` | `RATE_LIMITED` | Rate limit de `/auth/*`; respeta `Retry-After`. |
| `503` | `AUTH_UNAVAILABLE` | Supabase, Redis o Mongo no responden. |

Cabecera de las rutas autenticadas:

```http
Authorization: Bearer <accessToken>
```

## 5. Verificación de correo en 3 pasos (antes del login)

Supabase exige confirmar el correo antes de poder iniciar sesión. La app móvil
debe manejar el flujo así:

**Paso 1 — Registro.** Llamar `POST /api/v1/auth/register` con el correo
institucional. Con `202`, mostrar «revisa tu correo» y **no** intentar el
login todavía: Supabase envía el correo «Confirm sign up» con un enlace de
confirmación a la dirección registrada.

**Paso 2 — Confirmar en el enlace de Supabase.** El usuario abre el correo en
su dispositivo y toca el enlace: Supabase marca el correo como confirmado
(página de confirmación de Supabase en el navegador). La app espera la señal
del usuario (p. ej. botón «ya confirmé») o reintenta el paso 3. Si el correo
no llega, la app puede reenviarlo con el SDK de Supabase del lado cliente
(`auth.resend({ type: 'signup', email })`): **no existe ni se necesita una
ruta backend nueva** para reenvío ni verificación (decisión AF02).

**Paso 3 — Login.** Recién entonces llamar `POST /api/v1/auth/login`. Con el
correo ya confirmado, Supabase valida credenciales + confirmación y el backend
responde `200` con `accessToken` (activando el perfil local pendiente). Si el
correo **aún no está confirmado**, la respuesta es `401 INVALID_CREDENTIALS`
(idéntica a una contraseña incorrecta, por antienumeración): la app debe
sugerir revisar el correo o reenviar la confirmación, sin prometer que el
motivo sea ese.

```text
App ── register ──▶ Backend ── signUp ──▶ Supabase ──▶ correo con enlace
App ◀── 202 genérico ── Backend
Usuario abre el correo y confirma el enlace de Supabase
App ── login ──▶ Backend ── password grant + confirmación ──▶ Supabase
App ◀── 200 con accessToken ── Backend
```

## 6. Sonda de salud — `GET /health`

Pública, con su propia política de rate limit en Redis (si se excede responde
`429` con `Retry-After`). `200` con
`{"status":"Healthy","checks":{"mongodb":"Healthy","redis":"Healthy"}}` o `503`
con el contrato de error único si MongoDB o Redis no responden (readiness del
stack). Útil para sondas de la app o del monitor.

## 7. Rate limiting

- Políticas distribuidas en Redis para todas las rutas públicas; más estrictas
  en `/auth/*` (login, registro, sesión).
- Respuesta `429` con el contrato de error único y cabecera `Retry-After`.
- Si Redis no responde, `/auth/*` falla **cerrado** (`401`/`503`), nunca
  abierto.

## 8. Notas para la app

- **Sin carrito, menús ni pasarelas**: compra directa, pago en efectivo
  presencial (BR-001..003).
- **CORS es irrelevante** para un cliente nativo; si más adelante hay un
  cliente web, se configurará una política explícita (nunca `*` en producción).
- Los endpoints existentes hoy están inventariados en `PROJECT_STATUS.md`
  § 2; las rutas del Excel aún no implementadas responden `404`.
- La UI Swagger (`GET /openapi`) está pensada solo para lectura de contratos
  (`tryItOut` deshabilitado).
