# Integración móvil Expo Go — Autenticación y verificación de correo (@uttt.edu.mx)

> Estado: documento de referencia para el equipo móvil. No contiene secretos, claves, tokens ni valores de entorno reales. Toda credencial vive solo en variables de entorno de la instancia EC2 (`.env` del servidor, fuera de Git) y nunca se comitea.

## 1. Variables de entorno requeridas (sin valores)

El backend lee las siguientes variables de entorno. **Ningún valor se comitea.** El desarrollador móvil no necesita conocerlas; solo debe saber que el backend las inyecta desde la instancia EC2.

| Variable | Origen / quién la provee | Nota |
|---|---|---|
| `SUPABASE_URL` | Supabase project dashboard (no es Terraform de este repo) | Configurada por el propietario en la EC2 |
| `SUPABASE_ANON_KEY` | Supabase project dashboard | Clave pública de cliente; segura para exponer en el móvil |
| `SUPABASE_JWKS_URL` | Derivada de `SUPABASE_URL` | Para validar firma del JWT |
| `SUPABASE_JWT_SECRET` | Supabase project dashboard | Solo backend; nunca al móvil |
| `SUPABASE_JWT_ISSUER` | Supabase project dashboard | Para validar issuer del JWT |
| `SUPABASE_JWT_AUDIENCE` | Supabase project dashboard | Para validar audience del JWT |

**Regla de seguridad:** ninguna de estas variables se guarda en Git, `.env` comiteado, Terraform ni logs. Si falta alguna, el backend falla cerrado (`503`).

## 2. Correo institucional (@uttt.edu.mx) y verificación

### ¿El endpoint `/api/v1/auth/register` crea el usuario en Supabase o solo en MongoDB?

**En ambos.** El `RegistrationService` hace:
1. Llama a Supabase Auth (`POST /auth/v1/signup`) para crear la identidad con email + contraseña.
2. Si Supabase devuelve un `sub` (identidad) inequívoca, crea un **perfil local pendiente** en MongoDB (`users`, rol `Buyer`, estado `PendingEmailVerification`) vinculado al `sub`.

Si falla la escritura en MongoDB tras crear en Supabase, el registro **no** se considera completo y no se responde como éxito. Nunca se devuelve `userId`, rol, estado interno ni JWT en el registro (respuesta `202 Accepted` genérica, indistinguible para email nuevo o existente, para evitar enumeración).

### ¿Quién envía el correo de verificación?

**Supabase, no el backend AWS.** Al llamar `signUp(email, password)`, Supabase envía el correo de confirmación usando la plantilla **"Confirm sign up"**. El backend **no envía correos** ni expone endpoint de reenvío propio.

### ¿Código OTP o enlace de confirmación?

**Código OTP**, no enlace. La plantilla de Supabase usa `{{ .Token }}` (código numérico), sin `{{ .ConfirmationURL }}`. Se descartó `signInWithOtp` / "magic link" porque crearía sesión sin contraseña y alteraría el flujo elegido (contraseña + verificación por código).

La app móvil presenta la entrada del código y llama directamente a Supabase:
- Verificar: `verifyOtp({ email, token, type: 'email' })` directo a Supabase (no al backend).
- Reenviar: `resend({ type: 'signup', email })` directo a Supabase.

### ¿Está ya configurada la confirmación de correo y el SMTP para @uttt.edu.mx?

**No, aún no.** Esta es la tarea **TASK-AUTH-002 (abierta)**:

- El SMTP **incorporado de Supabase** solo entrega a direcciones autorizadas del equipo y limita a **2 correos/hora por proyecto**. No cubre envíos a estudiantes @uttt.edu.mx.
- Se debe configurar **SMTP propio** en Supabase (panel de Supabase, **no** en el Terraform de este repo) y probar envío real a `@uttt.edu.mx` antes de considerar la función lista.
- La plantilla **"Confirm sign up"** debe usar `{{ .Token }}` y **no** debe usar `{{ .ConfirmationURL }}` ni enlaces que confirmen automáticamente.
- Nunca usar la plantilla "Magic link or OTP", ni `signInWithOtp`.

**Conclusión para el móvil:** hasta que se verifique entrega real de correo a `@uttt.edu.mx`, la verificación de correo **no está operativa en producción**. Pueden probar el flujo en un proyecto Supabase de prueba, pero no asumir que el entorno AWS ya lo soporta.

## 3. Sincronización MongoDB ← Supabase (lazy, no webhook)

**No hay webhook de Supabase hacia el backend.** La sincronización es **perezosa ("lazy")** y ocurre en el primer intento de login (`POST /api/v1/auth/login`, API-002):

1. El móvil se loguea con email + contraseña. El backend delega a Supabase Auth (`token?grant_type=password`).
2. Supabase valida identidad **y** confirma que el correo está verificado (`email_confirmed_at` / `confirmed_at` presente).
3. El backend busca el perfil local por `sub` (Supabase user ID) en MongoDB.
4. Si el perfil existe con estado `PendingEmailVerification` y la confirmación en Supabase es positiva, el backend lo pasa a `Active` **idempotentemente** y luego devuelve el JWT.
5. Si el perfil no existe o la cuenta está suspendida/eliminada, se deniega el acceso.

**Flujo resumido:**
```
Registro (API-001)
  └─ Supabase signUp → identidad creada + correo de verificación enviado
  └─ MongoDB: perfil PendingEmailVerification (rol Buyer, sub de Supabase)
         │
         └─ Usuario recibe código OTP → verifyOtp en Supabase → correo verificado
                │
                └─ Primer login (API-002): Supabase valida confirmación
                      └─ Backend reconcilia PendingEmailVerification → Active
                            └─ Devuelve JWT Supabase + rol local + accountStatus Active
```

**Nunca** se permite acceso con solo el JWT de Supabase si el perfil local no está `Active`. Un JWT válido sin confirmación / sin perfil local `Active` se rechaza (`401` / `403` según política aprobada).

## 6. Cómo comprobar el usuario de prueba

### En Supabase Auth
- Dashboard de Supabase → **Authentication → Users**, buscar por email `@uttt.edu.mx`.
- O llamar `GET /auth/v1/user` con `Authorization: Bearer <accessToken>` (el código backend ya lo usa en `SupabaseAuthClient.FindBySubjectAsync`).
- Verificar que `email_confirmed_at` / `confirmed_at` tenga valor (verificado) o esté vacío (pendiente).

### En MongoDB (colección `users`)
- Consultar por `supabaseUserId` (índice único) = el `sub` de Supabase.
- Campos a revisar: `email`, `displayName`, `accountStatus` (debe ser `PendingEmailVerification` antes de verificar, `Active` tras login exitoso post-verificación), `roles: ["Buyer"]`, `createdAt`.
- Nota: no hay endpoint público para listar usuarios; solo el admin (`GET /api/v1/admin/users`) y el propio usuario (`GET /api/v1/users/me`) pueden consultar perfiles.

## 7. Ejemplo JSON de respuesta de `POST /api/v1/auth/login` (sanitizado)

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600,
  "tokenType": "Bearer",
  "user": {
    "id": "<id-local-de-mongodb>",
    "roles": ["Buyer"],
    "accountStatus": "Active"
  }
}
```

Notas:
- `expiresIn` es el valor devuelto por Supabase, no una duración fija del backend.
- `user.id` es el identificador **local de MongoDB**, no el `sub` de Supabase (ese no se expone).
- `accountStatus` será `Active` solo si el correo está verificado y el perfil no está suspendido/eliminado. Si el correo no está verificado, el login se deniega (`401` genérico o `403 EMAIL_VERIFICATION_REQUIRED`, pendiente de aprobación de antienumeración) y **no** se devuelve JWT.

## 7. Qué falta por hacer (pendiente del equipo)

1. Configurar SMTP propio en Supabase (panel de Supabase, no nuestro Terraform) y probar envío a `@uttt.edu.mx`. → **TASK-AUTH-002 abierta**.
2. Aprobar el contrato definitivo de API-001/002 (códigos 202/403/429/503, DTOs) y el flujo de reconciliación Supabase ↔ Mongo. → **TASK-AUTH-007/008 abiertos**.
3. No desplegar en producción hasta tener evidencia de costo $0 (alerta AWS Budget) y prueba de entrega de correo real.

---
*Este documento es de referencia interna. No contiene secretos ni valores de entorno reales. Todas las credenciales viven solo en variables de entorno de la instancia EC2.*