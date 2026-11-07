# spec.md — Autenticación y Usuarios · Backend UTTT-GO
> Fase: `phase-02-auth-users` · Módulos: `Identity`, `Users`, `Sellers`, `Admin`
> Versión: 1.5 · Estado: **Aprobada individualmente para implementación local incremental de API-001/002/003/004/006/007/008; contratos definitivos, integraciones y despliegue pendientes** · Actualizado: 2026-11-04 (v1.0 aprobada por equipo; v1.5 sin aprobación integral del equipo)
> Documento subordinado a `docs/constitution.md` v2.3 y a `AGENTS.md` v2.2. En caso de conflicto, prevalece la constitución.

---

## 1. Contexto y Objetivo

Este spec define el módulo de **autenticación y gestión de usuarios** del backend UTTT-GO. Cubre el registro e inicio de sesión con **Supabase Auth**, la persistencia del perfil local en MongoDB, la activación de vendedores y las operaciones administrativas de usuarios.

Supabase Auth es la única fuente de identidad y emisión de JWT. El backend **recibe la contraseña por HTTPS en API-001/002 para delegarla a Supabase**, pero no la persiste, registra en logs ni la verifica con hashes locales; no implementa Argon2id/BCrypt, lockout propio ni 2FA local. El perfil, rol y estado de la cuenta viven en MongoDB y son la fuente de verdad para autorización.

Objetivo: que un miembro de la comunidad UTTT pueda registrarse con su correo institucional, operar como comprador, activarse como vendedor por autoservicio y ser administrado por un admin, con todas las operaciones sensibles resueltas server-side.

Referencias:

- `docs/constitution.md` v2.3 — reglas de negocio, seguridad y stack.
- `AGENTS.md` v2.2 — roles, sub-agentes y reglas operativas.
- `material/Requerimientos para el desarrollo del proyecto Móvil v2.pdf` §§ 3–5, 7–11, 13–16, 18 y tareas 2–6 de p. 4 — requisitos académicos; matriz en `docs/academic-requirements-phase-02.md`.
- `material/UTTT_GO_Punto_4_Especificacion_API_v1.xlsx` — fichas individuales API-001/002/003/004/006/007/008 **en estado «Propuesta a validar»**; método y ruta de referencia, payloads/respuestas/códigos sujetos a aprobación.
- `material/IC-Agile-User-Story-UTTT-GO-Figma-v2.xlsx` y `material/UI-Diana Mota Trejo.xlsx` — historias con numeraciones **distintas**; `material/PB_Diana Mota Trejo.pdf` — backlog PB/HU. No fusionar sus IDs.
- `memoria.md` — decisiones DEC-002 y DEC-003 (resueltas funcionalmente; diseño de baja pendiente) y glosario.
- Discovery de la fase y fichas API-001..010/039..040 del inventario — decisiones del equipo incorporadas; método/ruta del Excel prevalecen para el MVP.

**Extensión modular en revisión:** API-039 se desarrolla documentalmente en `api-039-list-users/spec.md` → `plan.md` → `task.md`; es una **propuesta sin aprobación de código** y no amplía el alcance de la aprobación local v1.5 de siete rutas.

---

## 2. Usuarios y Actores

| Actor | Descripción | Permisos relevantes |
|---|---|---|
| **Comprador (Buyer)** | Usuario UTTT registrado con rol por defecto `Buyer`. | Ver/editar su propio perfil; solicitar baja de cuenta. |
| **Vendedor (Seller)** | Usuario autoactivado que completó su perfil de negocio. | Todo lo de Buyer; gestionar su perfil y sus propios productos (fuera de este spec). |
| **Administrador (Admin)** | Usuario con rol `Admin`, asignado solo del lado servidor. | Listar, buscar, suspender y reactivar usuarios; nunca se autoasigna desde el cliente. |
| **Sistema** | Validación de JWT, blacklist, rate limit y sincronización de identidad. | Aplicar reglas fail-closed y registrar auditoría. |

Regla de roles: el rol efectivo se lee del perfil local en MongoDB, **nunca** del JWT. `Admin` no puede otorgarse a sí mismo ni asignarse desde un cliente.

---

## 3. Historias de Usuario

| ID | Como… | Quiero… | Para… |
|---|---|---|---|
| HU-AUTH-01 | Visitante UTTT | registrarme con mi correo `@uttt.edu.mx` | acceder como comprador |
| HU-AUTH-02 | Usuario | iniciar sesión con email y contraseña | operar en la plataforma |
| HU-AUTH-03 | Usuario | recuperar contraseña olvidada mediante enlace enviado a mi correo registrado | volver a acceder sin soporte manual; canal técnico pendiente de definir |
| HU-AUTH-04 | Usuario | cerrar sesión de forma segura | invalidar mi token actual |
| HU-AUTH-05 | Comprador | ver y editar mi perfil | mantener mis datos actualizados |
| HU-AUTH-06 | Comprador | activarme como vendedor | publicar productos propios |
| HU-AUTH-07 | Vendedor | completar mi perfil de negocio | que los compradores me identifiquen y recojan |
| HU-AUTH-08 | Admin | listar y buscar usuarios | gestionar la comunidad |
| HU-AUTH-09 | Admin | suspender y reactivar cuentas | moderar conductas indebidas |
| HU-AUTH-10 | Usuario | solicitar la baja de mi cuenta | ejercer mi derecho de eliminación |

---

## 4. Requisitos Funcionales (EARS)

| ID | Requisito (EARS) | Prioridad |
|---|---|---|
| **RF-01** | **Cuando** un visitante se registra, el sistema **debe** aceptar únicamente correos cuyo dominio sea exactamente `@uttt.edu.mx` y rechazar los demás; `422` es el código **propuesto por API-001**, pendiente de aprobación. | Dominio obligatorio; código provisional |
| **RF-02** | **Cuando** un usuario inicia sesión, el sistema **debe** delegar la autenticación con email y contraseña exclusivamente en Supabase Auth; **si** la cuenta está habilitada, el backend puede propagar el JWT de Supabase según el contrato aprobado. No habilitar operaciones privadas mientras falte verificar el correo por código (RF-15), aunque Supabase llegue a emitir un JWT. | Delegación obligatoria; respuesta de cuenta no verificada pendiente |
| **RF-03** | **Cuando** un usuario se registra por primera vez, el sistema **debe** vincular su perfil local al `sub` de Supabase con rol `Buyer`. **Propuesta técnica:** mantener la cuenta pendiente para operar hasta verificar el correo institucional por código (RF-15); estado interno, respuesta de API-001 y comportamiento del login requieren aprobación. No adoptar `status:Active` de la ficha como activación inmediata. | Perfil obligatorio; estados propuestos |
| **RF-04** | **Cuando** un comprador actualiza su perfil, el sistema **debe** validar nombre; el email proviene del perfil/Supabase y no se edita por esta ruta. Teléfono y avatar son opcionales. | Obligatorio |
| **RF-05** | **Cuando** un usuario se activa como vendedor, el sistema **debe** permitir la autoactivación sin aprobación administrativa (MVP) y **debe** exigir nombre de negocio, descripción y punto de recogida. | Obligatorio |
| **RF-06** | **Cuando** un usuario olvida su contraseña, la aplicación **debe** permitir solicitar un enlace de recuperación enviado al correo registrado para restablecerla mediante Supabase Auth. El canal técnico (SDK/flujo de Supabase o backend) se define sin aprobar automáticamente `/api/v1/auth/recover`. La respuesta no debe revelar existencia del correo. | Funcionalidad confirmada; contrato técnico pendiente |
| **RF-07** | **Cuando** un admin consulta usuarios, el sistema **debe** permitir listar, buscar y paginar; **debe** exponer estado y rol de cada usuario. | Obligatorio |
| **RF-08** | **Cuando** un admin suspende una cuenta, el sistema **debe** marcar el perfil como `Suspended` y **debe** bloquear sus operaciones autenticadas hasta su reactivación. | Obligatorio |
| **RF-09** | **Cuando** un admin reactiva una cuenta, el sistema **debe** restaurar el estado `Active` y **debe** permitir nuevamente sus operaciones autenticadas. | Obligatorio |
| **RF-10** | **Cuando** un usuario cierra sesión, el sistema **debe** revocar el JWT agregando su `jti` a Redis (`revoked:{jti}`) con TTL hasta `exp`; **si** Redis no responde, **debe** fallar cerrado. | Obligatorio |
| **RF-11** | **Cuando** un usuario elimina su cuenta, el sistema **debe** eliminar la información que le pertenece. Para un vendedor se incluyen productos, publicaciones, contenido e historial propios; el tratamiento de datos compartidos (como pedidos del otro participante), operaciones en curso y el borrado coordinado con Supabase/Media/otros módulos se especifica antes de implementar API-005. Baja lógica/anonimización por sí sola no satisface esta regla. | Confirmado funcionalmente; diseño pendiente |
| **RF-12** | **Cuando** un usuario autenticado consulta `GET /api/v1/auth/me`, el sistema **debe** validar JWT y revocación, resolver el perfil local por `sub` y devolver identidad/rol/estado locales sin tokens ni claims sensibles; `GET /api/v1/users/me` **debe** seguir exponiendo el perfil propio editable como contrato distinto. | Obligatorio |
| **RF-13** | **Cuando** se consulta `GET /api/v1/sellers/{sellerId}`, el sistema **debe** exponer únicamente datos públicos, sin email/teléfono privados. Según **RN-FIG-06** de la trazabilidad Figma, el perfil del vendedor inactivo **sigue visible**, pero no puede recibir nuevos pedidos hasta reactivarse; inactividad por sí sola no causa `404`. La ficha deja abierto acceso público o autenticado. **Cuando** un vendedor edita `PATCH /api/v1/sellers/me`, solo puede modificar su perfil propio y asociar media aprobada. | Visibilidad confirmada por maqueta; acceso/composición pendientes |
| **RF-14** | **Cuando** un Admin cambia el estado de cuenta mediante `PATCH /api/v1/admin/users/{userId}/status`, el sistema **debe** permitir suspender/reactivar conforme a las reglas de RF-08/09, auditar la acción y rechazar cambio de rol o de cuenta ajena a la política; desactivación definitiva queda condicionada por DEC-003. | Obligatorio |
| **RF-15** | **Cuando** se registra un miembro de la UTTT, el flujo de la app **debe** permitir verificar el correo institucional mediante un código antes de habilitar su cuenta; un código incorrecto o vencido no debe activarla. Se deriva de `PB-003/HU-03` y la historia 3 de `UI-Diana Mota Trejo.xlsx`, **no** del PDF académico. **Propuesta técnica:** OTP de confirmación de registro de Supabase (§ 4.3), sin endpoint backend nuevo; pendiente de aprobación y prueba end-to-end. | Funcional requerido; diseño propuesto no aprobado |

### 4.1 Validación de email institucional

- Dominio permitido: `@uttt.edu.mx` exacto (comparación sobre la parte posterior al último `@`, case-insensitive, sin subdominios).
- Verificación por código exigida por backlog/UI (RF-15); el registro no habilita acceso de inmediato. El flujo Supabase propuesto en § 4.3 **no** es una ficha adicional ni autoriza implementar API-001/002 antes de su aprobación.
- El correo no se reutiliza entre cuentas activas; un correo dado de baja no habilita un registro nuevo sin revisión (ver § 6).

---

## 4.2 Contratos individuales prioritarios: alcance y procedencia

Las siete fichas del Excel están marcadas **«Propuesta a validar»**. Se conserva su método/ruta; sus ejemplos de cuerpos, respuestas y códigos **no son DTOs, campos MongoDB, estados ni HTTP definitivos**. Autorización confirmada: identidad derivada del JWT validado, roles/estado locales, propiedad de recursos, blacklist Redis y protección de datos; tipo de acceso de API-008 por aprobar. Prevalecen BR-007 (avatar moderado), BR-011 (eliminación de cuenta) y la fuente de roles locales. Los escenarios siguientes son criterios documentales previstos, **ninguno ejecutado**. La columna HU-Figma no equivale a HU-PB del backlog.

| Ficha | Historia en Excel Figma / UI | Backlog del PDF `PB_Diana Mota Trejo.pdf` | Fuente académica |
|---|---|---|---|
| API-001 | `HU-004` / `UI-004 Registro` | `PB-002` / `HU-02`; verificación relacionada: `PB-003` / `HU-03` | PDF profesor §§ 5, 7, 14, 15; código solo en backlog/UI |
| API-002 | `HU-003` / `UI-003 Iniciar sesión` | `PB-004` / `HU-04` | PDF profesor §§ 7, 15 (JWT, Redis) |
| API-003 | Sin HU/UI de logout inequívoca en las fuentes leídas | Sin PB inequívoca | **PDF profesor § 10**: logout backend y móvil |
| API-004 | `HU-028` / `UI-024 Mi perfil` es relación indirecta; sesión ≠ perfil | `PB-021` / `HU-21` indirecta | PDF profesor § 15 (JWT); ficha API-004 da contrato de sesión |
| API-006 | `HU-028` / `UI-024 Mi perfil` | `PB-021` / `HU-21` | PDF profesor § 15 (JWT y seguridad) |
| API-007 | `HU-029` / `UI-025 Editar perfil` | `PB-022` / `HU-22` | PDF profesor §§ 7, 15, 16 (privacidad) |
| API-008 | `HU-015` / `UI-014 Perfil del vendedor` | `PB-012` / `HU-12` | PDF profesor §§ 7, 15 (rate limit/NoSQL) |

**API-001 — `POST /api/v1/auth/register` (pública).** Ficha original: `email`, `password`, `displayName`; proponía `201` con `userId`, `email`, `displayName`, `roles:["Buyer"]`, `status:"Active"`; `400`, `409`, `422`, `429`, `502/503`. **Aprobación individual de responsable técnica del punto 4:** éxito `202 Accepted` genérico e indistinguible para email nuevo o existente **solo con solicitud válida y procesable**; sin `userId`, roles, estado interno, JWT ni señales de existencia; **nunca `409` por duplicado**. El perfil nuevo permanece `PendingEmailVerification` hasta verificación OTP, sin exponer ese estado en `202`; `202` no oculta errores reales de dependencias ni asegura entrega del correo. Este cambio de éxito/antienumeración frente a la ficha **no ratifica entrada, cuerpo exacto, demás HTTP/errores ni sincronización**, y no modifica el Excel. Email normalizado y dominio institucional exacto; no inventar longitud de nombre ni umbral de contraseña. Rechazar roles del cliente y diseñar reconciliación si Supabase crea identidad y Mongo falla. Escenarios: registro crea perfil pendiente; dominio externo rechazado (`422` provisional); rol inyectado no asignado; fallo Supabase/Mongo no informa alta completa; código errado/vencido no habilita.

**API-002 — `POST /api/v1/auth/login` (pública).** Ficha: `email`, `password`; propone `200 {accessToken, expiresIn, tokenType, user:{id,roles}}`, `400`, `401`, `403`, `429`, `503`. **Propuesta § 4.3:** solo devolver `200` y JWT Supabase a cuenta confirmada y localmente habilitada; para cuenta pendiente, respuesta de error `403 EMAIL_VERIFICATION_REQUIRED` **sin token**, bajo revisión de antienumeración. Si Supabase emitiera sesión pese a no estar verificada/habilitada, no propagarla. Contraseña solo por HTTPS delegada a Supabase sin persistencia/logs; roles locales, nunca claims del cliente. `refreshToken`/renovación y semántica de sesión permanecen en TASK-AUTH-003. Escenarios: cuenta confirmada y activa recibe JWT y rol local; credenciales inválidas reciben error genérico; pendiente/suspendida no obtiene sesión backend; caída proveedor/Redis no abre acceso.

**API-003 — `POST /api/v1/auth/logout` (autenticado).** Ficha: body `{refreshToken: "<si aplica>"}`, éxito `204`, `401` o `503`; respuesta idempotente `204` al repetir es propuesta. **Confirmado:** añadir `revoked:{jti}` a Redis con TTL hasta `exp` y fallar cerrado si Redis falla (constitución); el PDF profesor § 10 exige logout en backend y móvil, y § 16 exige sesión móvil persistente salvo logout. **Pendiente:** si hay refresh token, cómo lo revoca Supabase, si va en el body, reintentos con JWT ya revocado y eliminación móvil de credenciales/caché. No prometer cierre global solo con blacklist del JWT; no guardar ni registrar refresh tokens. Escenarios: Given token activo/When logout/Then no se autoriza de nuevo; Given Redis caído/Then no se confirma revocación; Given repetición/Then comportamiento aprobado y probado entre instancias y app.

**API-004 — `GET /api/v1/auth/me` (autenticado).** Ficha: sin body, propone `200 {id,email,displayName,roles,accountStatus}`, `401/404/503`. Validar firma, issuer, audience, expiración, revocación y `sub`; devolver sesión/identidad del perfil local sin token ni claims sensibles. No sustituye API-006. Caché de perfil: la ficha lo considera opcional; propuesta segura es **no cachear inicialmente** hasta decidir invalidación tras editar rol/estado; no aprobado como obligación. Escenarios: token válido devuelve identidad propia; revocado `401`; perfil local inexistente `404` propuesto; Redis caído fail-closed.

**API-006 — `GET /api/v1/users/me` (autenticado).** Ficha: sin body, propone `200 {id,displayName,email,phone,avatarUrl,roles}`, `401/404/503`; devuelve perfil **privado propio**, no resumen de sesión. **Propuesta de lectura:** mantener `avatarUrl` como salida de lectura temporal/controlada por el backend solo para activo aprobado, o decidir otra representación con revisión formal; no URL pública arbitraria. Esto no implica que `avatarUrl` sea un campo seguro de escritura. Escenarios: token propio obtiene solo perfil propio; inválido/error/perfil faltante según códigos de ficha aún provisionales; no expone documento Mongo ni secretos.

**API-007 — `PATCH /api/v1/users/me` (autenticado).** Ficha: campos parciales propuestos `displayName`, `phone`, `avatarUrl`; éxito `200` perfil actualizado, errores `400/401/409/503`. Lista blanca de campos; rechazar `roles`, `emailVerified`, `accountStatus`, `sellerStatus`, `userId` y edición de otro recurso. **Propuesta de escritura, distinta de lectura:** asociar un `mediaAssetId` propio y `Approved` validado por Media, y devolver URL temporal/controlada al leer; **cambia el ejemplo `avatarUrl` del PATCH de la ficha y exige aprobación**, no cambio tácito. Alternativa: conservar campo `avatarUrl` de escritura solo si el backend puede vincularlo inequívocamente a un activo propio aprobado, sin aceptar URL arbitraria. Definir nulidad/eliminación del avatar, propiedad, DTO y código de error antes de implementar. Escenarios: actualización parcial propia, inyección de rol/estado denegada, avatar no aprobado rechazado según contrato a conciliar, persistencia fallida no informa éxito.

**API-008 — `GET /api/v1/sellers/{sellerId}` (pública o autenticada según decisión de producto, según ficha).** Ficha propone `200 {sellerId,displayName,ratingAverage,ratingCount,isActive}`, `400/404/503`; menciona reputación y alimentos disponibles, sin campos ni paginación cerrados. Email/teléfono privados nunca se exponen. **Confirmado por RN-FIG-06:** vendedor inactivo conserva perfil visible, pero no acepta pedidos nuevos; `404` de ficha solo podría corresponder a vendedor inexistente u otra causa aprobada, nunca solo a inactividad. **Abierto:** acceso público/autenticado; ratings ausentes; cómo la pantalla muestra productos: consultar API-011 de Catalog como alternativa de composición, o incluir lista paginada mediante contrato de Catalog; Reviews/Catalog no se leen por colecciones cruzadas. Escenarios: activo/inactivo muestran datos públicos y estado; inactivo no recibe pedidos nuevos; ID inválido/inexistente según códigos aprobados; si se incluyen productos, paginación; en todos los casos sin PII.

**Fuera de estos siete, sin eliminar funcionalidades:** API-005/009/010/039/040 y recuperación por enlace siguen en el SDD; RF-15 no tiene ficha de endpoint funcional entre API-001..042. Se propone operar confirmación/reenvío en Supabase sin ruta backend nueva (§ 4.3); si se requiere mediación backend, habrá que aprobar ficha adicional antes de implementarla.

### 4.3 Propuesta TASK-AUTH-002 — registro por contraseña y confirmación OTP (NO aprobada)

**Capacidades documentadas por Supabase, no probadas en este proyecto:** con **Confirm Email** habilitado, `signUp(email,password)` inicia confirmación; la plantilla **Confirm sign up** admite `{{ .Token }}` para mostrar el código, y `verifyOtp({email,token,type:'email'})` comprueba OTP de registro. `resend({type:'signup',email})` reenvía la confirmación iniciada. No sustituir esta plantilla por **Magic link or OTP**, ni usar `signInWithOtp`: este último puede iniciar autenticación sin contraseña/crear usuario y alteraría el flujo elegido. Referencias oficiales: [passwords](https://supabase.com/docs/guides/auth/passwords), [email templates](https://supabase.com/docs/guides/auth/auth-email-templates), [verifyOtp](https://supabase.com/docs/reference/javascript/auth-verifyotp), [resend](https://supabase.com/docs/reference/javascript/auth-resend).

**Configuración propuesta antes de probar:** habilitar proveedor email/password y **Confirm Email** en Supabase alojado; plantilla **Confirm sign up** con `{{ .Token }}` y sin `{{ .ConfirmationURL }}` ni enlaces equivalentes que confirmen automáticamente; ajustar caducidad **Email OTP expiration** considerando que también afecta otros correos (recuperación/cambio/invitación); comprobar rate limits efectivos y comportamiento de sesión del SDK móvil elegido. Configurar SMTP propio con envío probado a `@uttt.edu.mx` antes de producción: el SMTP incorporado solo entrega a direcciones autorizadas del equipo y limita a 2 correos/hora por proyecto; su encaje con **costo cero** es bloqueo, no supuesto resuelto. [SMTP](https://supabase.com/docs/guides/auth/auth-smtp) · [rate limits](https://supabase.com/docs/guides/auth/rate-limits) · [OTP expiration](https://supabase.com/docs/guides/auth/auth-email-passwordless).

**Secuencia propuesta, con responsabilidades separadas:**

1. **Backend API-001:** valida email institucional exacto y DTO; delega contraseña a Supabase `signUp`, exige identidad `sub` **inequívoca** del proveedor antes de crear idempotentemente `users` con rol local `Buyer` y estado propuesto `PendingEmailVerification`. Sin identidad confiable (incluidos duplicados con respuesta no reveladora) no inventa `userId` ni perfil: estrategia de respuesta y reparación por aprobar. No devuelve token ni declara email enviado/entregado; ante fallo de creación local no responde alta completa. Índices/normalización y reparación según `plan.md` § 4.2.
2. **Supabase Auth:** emite el correo de confirmación usando plantilla de **Confirm sign up** con OTP. **Móvil:** presenta entrada del código para el email usado; envía `verifyOtp({email,token,type:'email'})` directamente a Supabase (no al backend); ante necesidad de reenvío llama `resend({type:'signup',email})` con espera/errores. Clave pública de cliente según SDK; nunca service-role/secretos en móvil. No aceptar una afirmación de éxito del móvil como prueba para MongoDB.
3. **Confirmación:** Supabase valida el OTP y actualiza su estado; si `verifyOtp` devuelve sesión, el móvil **no** la utiliza para acceder a endpoints privados ni la toma como activación local. La cuenta se habilita solamente tras reconciliación backend; comportamiento exacto de almacenamiento/descarte de sesión en el SDK móvil exige prueba y decisión conjunta con TASK-AUTH-003.
4. **Backend API-002:** al login posterior con contraseña valida en Supabase identidad y confirmación actual (no solo JWT/claims), busca perfil por `sub` y reconcilia idempotentemente `PendingEmailVerification` → `Active` **solo si** la confirmación es positiva y la cuenta no está suspendida/eliminada; entonces responde sesión conforme a DTO aprobado. Si se confirma el correo pero el login falla por infraestructura, conserva estado seguro y permite reintento. Un JWT previo presentado a API-004 u otra ruta privada pasa por la misma política: solo después de verificación confiable y estado local permitido puede operar.

**Contrato JSON propuesto de API-001** (`POST /api/v1/auth/register`, no modifica ficha Excel):

```json
{"email":"alumna@uttt.edu.mx","password":"<solo HTTPS, nunca registrar>","displayName":"Alumna UTTT"}
```

**Éxito `202 Accepted` aprobado** (no afirma creación final ni entrega del correo). **Ejemplo de JSON público todavía propuesto, sujeto a ratificar texto/campos:**

```json
{"message":"Si el registro puede procesarse, recibirás instrucciones para verificar tu correo."}
```

Para correo nuevo o ya registrado, **mismo código y cuerpo públicos** si el request es válido y se puede procesar; no incluir `userId`, roles, estado interno, JWT ni indicios de existencia. La aprobación de `202`/antienumeración sustituye **solo esos aspectos** de `201`/`409` y de `status:"Active"` del Excel, que sigue intacto. Rechazar campos de rol/estado enviados por cliente mediante lista blanca; decidir `400` por payload malformado, `422` por dominio, `429` por rate limit y `502/503` por proveedor/Redis/Mongo antes de aprobar toda la ficha. **Nunca** devolver `409` por duplicado. Los errores reales no se disfrazan de `202`, ni se declara entrega efectiva del correo. Errores usan contrato constitucional, por ejemplo `422` **propuesto**:

```json
{"correlationId":"<id-correlacion>","status":422,"code":"INSTITUTIONAL_EMAIL_REQUIRED","message":"Correo institucional requerido.","details":{"email":["Debe pertenecer a @uttt.edu.mx."]}}
```

Ejemplo **propuesto** de validación `400` en API-001 (también aplicable a API-002 para campos ausentes):

```json
{"correlationId":"<id-correlacion>","status":400,"code":"VALIDATION_ERROR","message":"Solicitud inválida.","details":{"email":["Campo obligatorio."]}}
```

**Contrato JSON propuesto de API-002** (`POST /api/v1/auth/login`):

```json
{"email":"alumna@uttt.edu.mx","password":"<solo HTTPS, nunca registrar>"}
```

`200` propuesto **solo tras confirmación y perfil activo**; ejemplo omite refresh token deliberadamente, sujeto a TASK-AUTH-003. **Implementación local candidata API-002:** el `200` exige comprobación confiable de proveedor, perfil local `Active` y gate JWT/Redis; proveedores reales aún no configurados, por lo que producción falla cerrado con `503`. No promueve aún perfiles pendientes confirmados: esa conciliación sigue pendiente.

```json
{"accessToken":"<JWT-Supabase>","expiresIn":3600,"tokenType":"Bearer","user":{"id":"<id-local>","roles":["Buyer"],"accountStatus":"Active"}}
```

`expiresIn` es ilustrativo, **se obtiene de Supabase**, no fija duración de sesión. `403` **propuesto, NO habilitado en candidato local** para credenciales correctas pero correo pendiente (revisar divulgación de existencia de cuenta); hasta aprobar antienumeración, pendiente/suspendido/sin perfil y credenciales inválidas reciben `401` genérico sin JWT ni refresh token:

```json
{"correlationId":"<id-correlacion>","status":403,"code":"EMAIL_VERIFICATION_REQUIRED","message":"Verifica tu correo antes de continuar."}
```

Para `400` payload inválido, `401` credenciales incorrectas (**mensaje genérico, sin enumeración**), `429` límite y `503` proveedor/Redis/Mongo no disponibles, aprobar códigos y mensajes conforme a la ficha; ejemplos **propuestos**: `401 {"correlationId":"<id-correlacion>","status":401,"code":"INVALID_CREDENTIALS","message":"No se pudo iniciar sesión con esas credenciales."}`, `429 {"correlationId":"<id-correlacion>","status":429,"code":"RATE_LIMITED","message":"Intenta de nuevo más tarde."}` y `503 {"correlationId":"<id-correlacion>","status":503,"code":"AUTH_UNAVAILABLE","message":"Servicio temporalmente no disponible."}`. Estos dos últimos ejemplos también aplican a API-001; **`409` por duplicado no se usa en API-001**. En todos los errores, `details` es opcional; no devolver email, contraseña, OTP, JWT ni datos sensibles en logs. Cuenta suspendida o perfil inexistente se deniegan, no se convierten automáticamente en cuenta nueva.

**Límites y fallos (propuesta):** OTP erróneo/vencido deja perfil pendiente; el móvil muestra error de Supabase sin filtrar secretos y habilita reenvío según límites. La expiración de Email OTP es configurable y compartida por otros correos; no inventar TTL ni número de intentos. Supabase documenta `429`, límite de correo del proyecto y límites por usuario/IP; backend aplica además rate limit Redis distribuido `/auth/*`, fail-closed. Fallo de SMTP/entrega no autoriza cuenta; fallo Supabase tras crear identidad o fallo Mongo tras `signUp` exige registro/reconciliación seguros sin datos sensibles; confirmación Supabase exitosa con Mongo pendiente se reintenta en login; Mongo `Active` sin confirmación acreditada nunca autoriza. Aún falta probar semántica de respuesta de `signUp`, anti-enumeración, duplicados, verificación repetida, reenvío, caducidad y fallos parciales en un proyecto Supabase configurado.

**Decisión y alcance:** sin endpoint backend adicional **por ahora**. El contrato se revisa si no se puede garantizar reconciliación mediante API-002/rutas autenticadas existentes; ninguna llamada móvil crea roles ni habilita perfil. Decisiones humanas: aprobar flujo/SDK móvil y SMTP+costo cero; **resto** de DTO/HTTP de API-001 y contrato API-002 (el `202` genérico y no `409` de API-001 ya están aprobados individualmente); fuente/frecuencia de consulta de confirmación, estados e idempotencia; expiración/reenvío; política de sesiones/refresh (TASK-AUTH-003). TASK-AUTH-002 permanece abierta.

---

## 5. Requisitos No Funcionales

| ID | Requisito | Verificación |
|---|---|---|
| **RNF-01** | Todo endpoint autenticado **debe** validar el JWT de Supabase (firma, issuer, audience, expiración) y **debe** consultar la blacklist `revoked:{jti}`. | Suite de seguridad futura (aún inexistente) |
| **RNF-02** | La blacklist y el rate limit de `/auth/*` **deben** ser fail-closed si Redis no responde. | Prueba con Redis caído |
| **RNF-03** | Los roles y el estado de cuenta **deben** leerse del perfil local en MongoDB; **nunca** del JWT ni de un campo enviado por el cliente. | Revisión de código + tests de autorización |
| **RNF-04** | El rate limiting **debe** ser distribuido con Redis y **debe** responder `429` con el contrato de error único. | Tests de integración |

| **RNF-05** | API-001/002 pueden recibir contraseñas exclusivamente por HTTPS y delegarlas a Supabase; jamás persistirlas en MongoDB/Redis, cachearlas, incluirlas en respuestas/logs/trazas ni verificar hashes localmente. Supabase gestiona la credencial. | Revisión del flujo + pruebas de logs/secret scanning |
| **RNF-06** | Recuperación por enlace al correo registrado, sin enumeración de cuentas ni exposición de tokens en logs; las 42 fichas no incluyen ruta backend y cualquier ruta nueva requiere ficha aprobada. | Prueba de seguridad del flujo elegido |
| **RNF-07** | Toda respuesta de error **debe** incluir `correlationId` y seguir el contrato de `constitution.md`. | Tests de contrato |
| **RNF-08** | Los listados administrativos **deben** paginar con `page` + `pageSize` y un máximo configurable. | Tests de paginación |
| **RNF-09** | Las operaciones administrativas **deben** quedar auditadas (quién, qué, cuándo, `correlationId`) sin datos sensibles. | Revisión de logs/auditoría |
| **RNF-10** | Los secretos (JWT secret, connection strings) **deben** venir de variables de entorno de la instancia/contenedor; prohibido en Git/logs. | Secret scanning |
| **RNF-11** | Todos los endpoints **deben** exponerse bajo `/api/v1` y usar HTTPS. | Revisión de rutas |
| **RNF-12** | La autorización **debe** validar por recurso: un usuario solo edita su propio perfil; un admin accede a recursos administrativos. | Tests de autorización |
| **RNF-13** | Avatar editado solo con media propia `Approved` conforme a la constitución; **propuesta de escritura:** `mediaAssetId` verificado, distinta de **propuesta de lectura:** `avatarUrl` temporal de la ficha. El cambio de entrada `avatarUrl` requiere aprobación. Nunca aceptar URL arbitraria ni exponer Blob público. | Tests de media y conciliación de DTO de lectura/escritura |
| **RNF-14** | Las fechas **deben** persistirse y exponerse en UTC; dinero fuera de este módulo. | Revisión de modelo |
| **RNF-15** | Backend stateless con al menos dos instancias en nube: JWT/blacklist y rate limiting usan Redis compartido, no memoria local; TLS/HTTPS y observabilidad de errores sin datos sensibles. | Prueba distribuida propuesta y revisión académica PDF §§ 13–15 |
| **RNF-16** | SDD, DevSecOps, unitarias/integración/seguridad, CI/CD, versión de API/despliegues y comprobación de costo cero conforme al PDF oficial §§ 5–11 y 13–15. La viabilidad del stack constitucional no se presume sin evidencia de presupuesto/cuotas. | Matriz `docs/academic-requirements-phase-02.md` + revisión de costos y CI futuro |
| **RNF-17** | **Propuesta OTP:** antes de autorizar cualquier ruta privada, exigir confirmación vigente atribuible al mismo `sub` de Supabase y perfil local habilitado; en ausencia de prueba confiable o dependencia crítica, denegar. JWT válido por sí solo no es prueba suficiente. No cachear estado de verificación inicialmente; revisar costo/disponibilidad antes de aprobar consulta por petición. | T-AUTH-14 y pruebas de seguridad de acceso con JWT prematuro/Redis/Supabase/Mongo caídos |

**Políticas Redis de arranque autorizadas el 2026-11-06 para implementación local (configurables en `RateLimiting`):** `auth/register` 5 peticiones/15 min por IP; `auth/login` 10/15 min por IP **y** 5/15 min por correo normalizado; consulta pública de vendedor 60/min por IP; listado Admin 30/min por ID de usuario local. Respuesta `429` con `Retry-After` en segundos; Redis indisponible/configuración inválida ⇒ `503` fail-closed. La dirección IP se toma de la conexión; antes de operar detrás de un proxy se debe aprobar y configurar la cadena de proxies de confianza (sin confiar en `X-Forwarded-For` arbitrario). La validación JWT exige firma, issuer, audience y expiración antes de consultar `revoked:{jti}`; modo JWKS por `SUPABASE_JWKS_URL` **o** HS256 por `SUPABASE_JWT_SECRET`, nunca ambos; issuer/audience de configuración. Ausencia de configuración ⇒ fail-closed. Esta nota no acredita todavía prueba con JWT Supabase real ni cierre de TASK-AUTH-012.

**Persistencia MongoDB candidata local (2026-11-06, sin aprobación integral del esquema):** `users` es propiedad de Identity: `_id` ObjectId implícito único; índice único `supabaseUserId` para búsqueda de sesión/registro idempotente; índices `createdAt desc + _id asc`, `createdAt asc + _id asc`, `accountStatus + createdAt desc`, `roles + createdAt desc` para las ordenaciones/filtros del listado Admin. No se declara email único hasta aprobar borrado/re-registro. Sellers consulta `sellers` por `_id` (índice único implícito), con `displayName` y `status` para API-008; este nombre contradice `sellerProfiles` previsto en el plan, por lo que la relación con `users` y el modelo definitivo requieren conciliación antes de otras escrituras. Índices de `users` creados una vez al arrancar con Mongo configurado, no por petición. El filtro de búsqueda actual usa regex escapada e insensible a mayúsculas sobre `displayName`: su equivalencia Unicode NFKC con el selector de pruebas aún debe verificarse antes de considerar cerrado API-039. No hay autenticación Supabase ni auditoría Admin real verificada.

**Versionamiento previo al desarrollo (PDF § 11, § 15; TASK-AUTH-007):** la matriz canónica está en `docs/academic-requirements-phase-02.md` § «Matriz inicial y política de versiones». **Aprobación individual de responsable del punto 4, 2026-11-04:** contrato REST `/api/v1` `1.0.0-rc.1`; backend `0.1.0`; móvil `0.1.0`; Terraform `0.1.0`; configuración `dev`, `staging` y `prod` `0.1.0` **cada una**; SemVer MAJOR/MINOR/PATCH, DEP independiente, soporte de la versión móvil anterior **al menos 30 días tras publicar la nueva**, salvo medida extraordinaria documentada por vulnerabilidad de seguridad, registro de SHA/fecha/responsable/aprobación/evidencia y reversión solo a combinación probada (sin anterior, detener promoción y corregir). La responsable del punto 4 asume releases/coordinación de reversión del backend; **no** ejecuta reversión ni destruye infraestructura sin evidencia y autorización operativa. Son versiones de componentes previstos, **no desplegados** ni versiones del presente documento (`spec.md` v1.5); `DEP-0001` es solo ejemplo. **TASK-AUTH-001/006 `[x]`; 007 `[ ]`:** falta aprobación identificable del equipo o delegación formal; el aprobador operativo de cada operación requiere designación/autorización específica. No exponer rutas nuevas ni considerar aprobados DTO/HTTP de API-001/002 por numerar `/api/v1`.

---

## 6. Casos Límite

| ID | Caso | Comportamiento esperado |
|---|---|---|
| CL-01 | Registro con dominio distinto a `@uttt.edu.mx` | Rechazar sin crear cuenta; `422` de API-001 es propuesta, por aprobar. |
| CL-02 | Registro con correo ya existente activo | `409` de ficha como propuesta; respuesta pública no debe facilitar enumeración innecesaria (revisión de seguridad). |
| CL-03 | Registro con correo tras eliminar la cuenta previa | Política de reutilización e identidad Supabase por definir al diseñar DEC-003; no presumir rechazo permanente ni conservación del email. |
| CL-04 | Token expirado, inválido o con `jti` revocado | `401`; operación denegada. |
| CL-05 | Redis no disponible en logout o rate limit | Fail-closed: `503`/`401`; no se permite la operación. |
| CL-06 | Usuario suspendido intenta operar | `403`; se informa estado suspendido. |
| CL-07 | Admin intenta suspender su propia cuenta | `409`; operación no permitida. |
| CL-08 | No-admin accede a endpoints administrativos | `403`. |
| CL-09 | Autoactivación de vendedor con campos obligatorios incompletos | `422` con detalle de campos. |
| CL-10 | Reactivación de cuenta no suspendida | `409` (transición inválida). |
| CL-11 | Eliminación con órdenes/reseñas compartidas | Eliminar información propia sin borrar arbitrariamente derechos/historial del otro participante; política de datos compartidos y tratamiento de pedidos activos pendiente de diseño. |
| CL-12 | Avatar cuyo activo no está aprobado o no existe | No se asigna; `422` es propuesta histórica sujeta a conciliación con la ficha API-007. |
| CL-13 | Recuperación para correo inexistente | Respuesta del flujo elegido no revela si el correo existe; no se expone ruta backend sin ficha aprobada. |
| CL-14 | Solicitud de sesión con JWT revocado o perfil local inexistente | Rechazar token revocado; `401`/`404` de API-004 son códigos de ficha por aprobar; no devolver token ni claims sensibles. |
| CL-15 | Edición del perfil de un vendedor ajeno (API-010) | Solo el vendedor propietario puede editar mediante `/sellers/me`; `403` por rol incorrecto es propuesta de contrato, no permiso para modificar datos ajenos. La consulta de vendedor inactivo se verifica en CL-19. |
| CL-16 | Admin intenta cambiar rol o eliminar cuenta vía API-040 | Rechazar cambio de rol y no equiparar suspensión/reactivación con eliminación de cuenta API-005; cualquier eliminación administrativa requiere política y contrato aprobados. |
| CL-17 | OTP incorrecto, vencido, reutilizado o reenvío limitado | Supabase rechaza verificación no válida; perfil local no se habilita, móvil permite reintentar/reenvío respetando rate limits y no promete email entregado. Comportamiento preciso/reutilización se prueban; sin endpoint backend de OTP propuesto. |
| CL-18 | Logout con refresh token ausente o JWT ya revocado | Conservar fail-closed de JWT; definir revocación Supabase, presencia del body e idempotencia antes de cerrar API-003. |
| CL-19 | Perfil de vendedor inactivo o ratings no disponibles | Mantener perfil visible con estado inactivo y sin aceptar pedidos nuevos (RN-FIG-06); no exponer PII. Decidir ratings ausentes y composición de alimentos, **no** visibilidad por inactividad. |
| CL-20 | Identidad Supabase creada sin perfil Mongo o confirmación Supabase sin sincronización | No informar registro completo si falla Mongo; en login reconciliar solo perfil pendiente existente asociado al `sub` confiable. Si falta perfil, denegar y reparar por procedimiento aprobado, nunca crear `Buyer` a partir de JWT arbitrario. |
| CL-21 | JWT válido sin confirmación, Mongo `Active` desactualizado o proveedor no disponible | Denegar ruta privada salvo confirmación confiable y estado local permitido; caída de Supabase/Mongo ⇒ no autorizar. No promover suspendido/eliminado ni confiar en claims o respuesta del móvil. |

---

## 7. Contratos (resumen)

Endpoints previstos (detalle de DTOs en `plan.md`/`task.md` de la fase):

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| POST | `/api/v1/auth/register` | Público | Registro con email `@uttt.edu.mx` + password (vía Supabase). |
| POST | `/api/v1/auth/login` | Público | Login email + password. |
| POST | `/api/v1/auth/logout` | Autenticado | Revoca `jti` en Redis. |
| GET | `/api/v1/auth/me` | Autenticado | Sesión: identidad y rol/estado del perfil local, sin tokens (API-004). |
| GET | `/api/v1/users/me` | Autenticado | Perfil propio. |
| PATCH | `/api/v1/users/me` | Autenticado | Actualiza perfil propio. |
| DELETE | `/api/v1/users/me` | Autenticado | Solicitud de eliminación de cuenta e información propia; datos compartidos y semántica de respuesta de API-005 pendientes de diseño. |
| GET | `/api/v1/sellers/{sellerId}` | Público o autenticado (pendiente) | Perfil sin datos sensibles (API-008). |
| POST | `/api/v1/sellers/activation-requests` | Autenticado | Autoactivación inmediata al completar perfil; no hay aprobación admin (API-009). |
| PATCH | `/api/v1/sellers/me` | Seller | Edición de perfil de negocio propio (API-010). |
| GET | `/api/v1/admin/users` | Admin | Lista/busca usuarios (paginado). |
| PATCH | `/api/v1/admin/users/{userId}/status` | Admin | Suspende/reactiva; no sustituye el flujo de eliminación API-005. |

Contrato de error: único de la constitución, con `code`, `message`, `correlationId` y `details` opcionales.

---

## 8. Fuera de Alcance

- Implementar unilateralmente un endpoint para verificar código: RF-15 es funcional de backlog/UI, sin ruta aprobada; **la funcionalidad no queda descartada**.
- Registro de vendedores con aprobación administrativa (DEC-002 resuelta a favor de autoactivación para el MVP).
- OAuth, magic link, SSO y proveedores sociales.
- 2FA/MFA, lockout local, Argon2id/BCrypt local.
- Cambio de email y gestión local de contraseñas; restablecimiento por enlace **sí** es requerido mediante Supabase Auth.
- Endpoint backend de recuperación `/api/v1/auth/recover` sin ficha API-001..042 (requiere aprobación si se elige esa vía); esto **no** difiere la funcionalidad de recuperación.
- Retener como solución final la cuenta y toda su información propia mediante simple baja lógica/anonimización (contradice BR-011). Detalles de datos compartidos y ejecución de eliminación siguen pendientes.
- Asignación de roles fuera de las operaciones administrativas definidas.
- Gestión de productos propios, pedidos y notificaciones (otros módulos).

---

## 9. Tests y Criterios de Aceptación

| ID | Nivel | Verifica |
|---|---|---|
| T-AUTH-01 | Integración | Registro acepta `@uttt.edu.mx` y rechaza otros dominios; comprobar `422` solo si se aprueba el código de ficha. |
| T-AUTH-02 | Integración | Login válido/inválido vía Supabase (mockeado en CI). |
| T-AUTH-03 | Integración | Creación de perfil local con rol `Buyer` y `sub` de Supabase. |
| T-AUTH-04 | Seguridad | JWT inválido/expirado/revocado rechazado (`401`). |
| T-AUTH-05 | Resiliencia | Redis caído ⇒ blacklist/rate limit fail-closed. |
| T-AUTH-06 | Seguridad | No-admin recibe `403` en admin; usuario no edita perfil ajeno. |
| T-AUTH-07 | Integración | Suspensión bloquea operaciones; reactivación las restaura. |
| T-AUTH-08 | Seguridad/integración | Recuperación por enlace al correo registrado vía Supabase; antienumeración y no exposición de credenciales. Si requiere endpoint backend, bloquearlo hasta ficha aprobada. |
| T-AUTH-09 | Integración | Autoactivación de vendedor exige campos obligatorios. |
| T-AUTH-10 | Integración/seguridad | Eliminación de identidad y datos propios (incluidos productos/contenido/historial vendedor), coordinación de datos compartidos e idempotencia, conforme a diseño aprobado. |
| T-AUTH-11 | Integración/seguridad | API-004 consulta identidad/rol local, token revocado rechazado y API-006 conserva perfil distinto. |
| T-AUTH-12 | Integración/seguridad | API-008 no revela datos privados; API-010 solo modifica perfil propio con avatar aprobado. |
| T-AUTH-13 | Integración/seguridad | API-040 suspende/reactiva mediante ruta única y no permite cambiar roles ni cuenta propia. |
| T-AUTH-14 | Funcional/seguridad | RF-15: Supabase OTP signup correcto confirmado → reconciliación local idempotente al login; incorrecto/vencido/reutilizado/reenvío limitado no habilitan; JWT previo, fallo Supabase/Mongo y estado suspendido se deniegan; probar con SDK/proyecto de prueba y mocks, sin declarar evidencia ejecutada. |
| T-AUTH-15 | Seguridad | API-001/002 reciben contraseña por HTTPS pero no la guardan, cachean ni registran; inyección de rol y enumeración revisadas. |
| T-AUTH-16 | Integración | API-003 revocación efectiva entre instancias; Redis caído fail-closed, refresh token e idempotencia según decisión aprobada. |
| T-AUTH-17 | Contrato/seguridad | API-006/007 escritura de activo aprobado y lectura controlada según DTO aprobado; API-008 inactivo visible sin recibir pedidos, ratings y productos según composición aprobada sin PII. |
| T-AUTH-18 | Contrato/integración | API-001/002: registrar pendiente sin token, login activo con JWT/rol local, pendiente/credenciales inválidas/dominio inválido/duplicado con errores aprobados y `correlationId`; comprobar no enumeración y ausencia de contraseña, OTP y tokens en logs. |
| T-AUTH-19 | Resiliencia/integración | Supabase crea identidad pero Mongo falla; Supabase confirma y Mongo sigue pendiente; reintento concurrente de reconciliación; perfil ausente o suspendido; caída de SMTP/Redis/Supabase/Mongo. Sin alta/operación privada indebida ni duplicados; evidencia SMTP real y costo por separado. |

Criterios de aceptación:

- [ ] **AC-001** Solo se aceptan correos `@uttt.edu.mx` exactos.
- [ ] **AC-002** Auth exclusivamente vía Supabase; sin contraseñas locales.
- [ ] **AC-003** Perfil local con rol/estado en MongoDB como fuente de autorización.
- [ ] **AC-004** Vendedor se autoactiva sin aprobación admin (MVP).
- [ ] **AC-005** Admin puede listar/buscar/suspender/reactivar.
- [ ] **AC-006** Logout revoca `jti` en Redis y falla cerrado si Redis cae.
- [ ] **AC-007** Eliminar información propia al eliminar la cuenta; cerrar diseño de datos compartidos/operaciones en curso antes de implementar API-005.
- [ ] **AC-008** Tests T-AUTH-01..19 aplicables en verde tras aprobar los contratos y diseños técnicos; trazabilidad actualizada.
- [ ] **AC-009** Las rutas funcionales expuestas son únicamente las fichas API-001..010/039..040 pertinentes; API-004/008/010 y el contrato único de API-040 tienen pruebas específicas.
- [ ] **AC-010** API-001..004/006..008 registran por separado RF, escenario, evidencia prevista y vínculos Figma/UI/PB cuando existan; API-003 explicita la ausencia de HU/UI/PB inequívocos y la referencia al PDF § 10. Ninguna ficha «Propuesta a validar» se presenta como aprobada íntegramente.
- [ ] **AC-011** Se documenta RF-15 con OTP Supabase **propuesto** sin inventar endpoint; `status:Active` de ficha API-001, acceso de API-002, política JWT y entrega SMTP se concilian y aprueban antes de implementar.
- [ ] **AC-012** Requisitos académicos y viabilidad de costo cero se cotejan con el PDF oficial; línea base SemVer/DEP documentada como **propuesta**, matriz y política formalmente aprobadas **antes de implementar** (TASK-AUTH-007 → 008 → 010); los comandos documentales usan solución/proyecto reales sin cambiar archivos de ejecución.
- [ ] **AC-013** RN-FIG-06: perfil inactivo visible y nuevos pedidos denegados; no se usa `404` por inactividad; HTTP y DTO de API-008 se aprueban antes de programar.

---

## 10. Definición de Hecho (DoD)

- [ ] Versión 1.5 de este `spec.md` revisada y aprobada; v1.0 fue aprobada el 2026-10-29.
- [ ] `plan.md` y `task.md` de la fase derivados de este spec.
- [ ] Contratos/DTOs y validaciones implementados conforme a § 7.
- [ ] Build Release y `dotnet format` en verde.
- [ ] Pruebas T-AUTH-01..19 aplicables en verde una vez definidos los contratos técnicos; ninguna prueba declarada ejecutada actualmente.
- [ ] Sin secretos en el diff; configuración vía variables de entorno.
- [ ] Trazabilidad `spec ↔ endpoint ↔ test` actualizada.
- [ ] DEC-002 y DEC-003 resueltas funcionalmente; diseño técnico de DEC-003 y canal de recuperación documentados.

---

## 11. Aprobación y Control de Cambios

| Campo | Valor |
|---|---|
| Estado | **Aprobada como base SDD para implementación local incremental de siete endpoints**; contratos finales/integraciones/despliegue pendientes |
| Fecha | 2026-11-04 (aprobación individual de alcance local) |
| Versión | 1.5 |
| Autor | AGENT-BE-02 (API Designer) |
| Revisores | AGENT-BE-04 (Security Reviewer), AGENT-BE-06 (Memory Keeper) |
| Aprobado por | Responsable técnica del punto 4 (v1.5 **solo para implementación local incremental**, declaración expresa en esta conversación, 2026-11-04); equipo UTTT-GO (v1.0); aprobación integral de v1.5 por equipo pendiente |
| Próxima revisión | RF-15/SMTP/contratos API-001/002 y resto de fichas; diseño de DEC-003 y canal de recuperación |

### 11.1 Registro de versiones

| Versión | Fecha | Cambio | Aprobado por |
|---|---|---|---|
| 1.0 | 2026-10-29 | Formalización del módulo de autenticación y usuarios (discovery de 5 preguntas) | Equipo UTTT-GO |
| 1.1 | 2026-11-03 | Conciliación de API-004/005/008/009/010/040 al inventario; RF-06 y ruta recover diferidos; DEC-003 pendiente. | Pendiente aprobación integral; decisiones funcionales confirmadas por el equipo |
| 1.2 | 2026-11-03 | Decisión confirmada de eliminar datos propios y recuperar contraseña por enlace; política de compartidos y canal técnico pendientes. | Pendiente aprobación integral; decisiones funcionales confirmadas por el equipo |
| 1.3 | 2026-11-03 | Cotejo del PDF académico y siete fichas individuales con HU/UI/PB; RF-15 verificación por código; RNF-05 corregido; contratos ambiguos y costo cero pendientes. | Pendiente aprobación integral |
| 1.4 | 2026-11-03 | RN-FIG-06 mantiene visible al vendedor inactivo; propuesta de cuenta pendiente por código y avatar de lectura/escritura; JWT/logout móvil y códigos/DTO sujetos a aprobación; versionamiento previo obligatorio. | Pendiente aprobación integral |
| 1.5 | 2026-11-04 | TASK-AUTH-002: propuesta OTP Supabase de registro sin nueva ruta; JSON API-001/002, autorización fail-closed, sincronización y SMTP/costo cero pendientes. | Pendiente aprobación integral |

**Alcance de aprobación local de v1.5 (2026-11-04):** la responsable técnica del punto 4, autora/revisora de esta especificación, autorizó expresamente usarla como base aprobada para código y tests locales de las siete rutas prioritarias con contratos candidatos. No aprobó integraciones externas, contrato definitivo, despliegue ni en nombre de todo el equipo. Los gates TASK-AUTH-007/008 y las tareas de integración/seguridad siguen abiertos; ninguna prueba simulada cuenta como prueba real de Supabase/MongoDB/Redis/OTP/SMTP.

### 11.2 Reglas de control de cambios

1. Cambiar dominio de correo, flujo de vendedor o permisos admin exige nueva versión de este spec y registro en `memoria.md`.
2. Ningún agente implementa código que contradiga la constitución o este spec.
3. El diseño de datos compartidos de DEC-003 y el canal técnico de recuperación deben aprobarse antes de cerrar sus pruebas y contratos.
