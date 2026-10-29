# spec.md — Autenticación y Usuarios · Backend UTTT-GO
> Fase: `phase-02-auth-users` · Módulos: `Identity`, `Users`, `Sellers`, `Admin`
> Versión: 1.0 · Estado: **Aprobado** · Fecha: 2026-10-29
> Documento subordinado a `constitution.md` v2.1 y a `AGENTS.md` v2.0. En caso de conflicto, prevalece la constitución.

---

## 1. Contexto y Objetivo

Este spec define el módulo de **autenticación y gestión de usuarios** del backend UTTT-GO. Cubre el registro e inicio de sesión con **Supabase Auth**, la persistencia del perfil local en MongoDB, la activación de vendedores y las operaciones administrativas de usuarios.

Supabase Auth es la única fuente de identidad y emisión de JWT. El backend **no** gestiona contraseñas, no implementa Argon2id/BCrypt local, no usa lockout propio ni 2FA local. El perfil, el rol y el estado de la cuenta viven en MongoDB y son la fuente de verdad para autorización.

Objetivo: que un miembro de la comunidad UTTT pueda registrarse con su correo institucional, operar como comprador, activarse como vendedor por autoservicio y ser administrado por un admin, con todas las operaciones sensibles resueltas server-side.

Referencias:

- `constitution.md` v2.1 — reglas de negocio, seguridad y stack.
- `AGENTS.md` v2.0 — roles, sub-agentes y reglas operativas.
- `memoria.md` — decisiones DEC-002 (resuelta), DEC-003 (abierta) y glosario.
- Discovery de la fase (5 preguntas) — respuestas incorporadas en RF-01 a RF-11.

---

## 2. Usuarios y Actores

| Actor | Descripción | Permisos relevantes |
|---|---|---|
| **Comprador (Buyer)** | Usuario UTTT registrado con rol por defecto `Buyer`. | Ver/editar su propio perfil; solicitar baja de cuenta. |
| **Vendedor (Seller)** | Usuario que se autoservicio-activó como vendedor y completó su perfil de negocio. | Todo lo de Buyer; gestionar su perfil de vendedor y sus ofertas (fuera de este spec). |
| **Administrador (Admin)** | Usuario con rol `Admin`, asignado solo del lado servidor. | Listar, buscar, suspender y reactivar usuarios; nunca se autoasigna desde el cliente. |
| **Sistema** | Validación de JWT, blacklist, rate limit y sincronización de identidad. | Aplicar reglas fail-closed y registrar auditoría. |

Regla de roles: el rol efectivo se lee del perfil local en MongoDB, **nunca** del JWT. `Admin` no puede otorgarse a sí mismo ni asignarse desde un cliente.

---

## 3. Historias de Usuario

| ID | Como… | Quiero… | Para… |
|---|---|---|---|
| HU-AUTH-01 | Visitante UTTT | registrarme con mi correo `@uttt.edu.mx` | acceder como comprador |
| HU-AUTH-02 | Usuario | iniciar sesión con email y contraseña | operar en la plataforma |
| HU-AUTH-03 | Usuario | recuperar mi contraseña por correo | volver a acceder sin soporte manual |
| HU-AUTH-04 | Usuario | cerrar sesión de forma segura | invalidar mi token actual |
| HU-AUTH-05 | Comprador | ver y editar mi perfil | mantener mis datos actualizados |
| HU-AUTH-06 | Comprador | activarme como vendedor | publicar ofertas |
| HU-AUTH-07 | Vendedor | completar mi perfil de negocio | que los compradores me identifiquen y recojan |
| HU-AUTH-08 | Admin | listar y buscar usuarios | gestionar la comunidad |
| HU-AUTH-09 | Admin | suspender y reactivar cuentas | moderar conductas indebidas |
| HU-AUTH-10 | Usuario | solicitar la baja de mi cuenta | ejercer mi derecho de eliminación |

---

## 4. Requisitos Funcionales (EARS)

| ID | Requisito (EARS) | Prioridad |
|---|---|---|
| **RF-01** | **Cuando** un visitante se registra, el sistema **debe** aceptar únicamente correos cuyo dominio sea exactamente `@uttt.edu.mx`; cualquier otro dominio **debe** rechazarse con `422`. | Obligatorio |
| **RF-02** | **Cuando** un usuario inicia sesión, el sistema **debe** autenticar con email y contraseña exclusivamente vía Supabase Auth y **debe** emitir/propagar el JWT de Supabase. | Obligatorio |
| **RF-03** | **Cuando** un usuario se registra por primera vez, el sistema **debe** crear su perfil local con rol `Buyer` y estado `Active`, usando el `sub` de Supabase como identificador de identidad. | Obligatorio |
| **RF-04** | **Cuando** un comprador actualiza su perfil, el sistema **debe** exigir nombre y email, y **debe** aceptar teléfono y avatar como opcionales. | Obligatorio |
| **RF-05** | **Cuando** un usuario se activa como vendedor, el sistema **debe** permitir la autoactivación sin aprobación administrativa (MVP) y **debe** exigir nombre de negocio, descripción y punto de recogida. | Obligatorio |
| **RF-06** | **Cuando** un usuario solicita recuperar su contraseña, el sistema **debe** disparar el flujo de recuperación por email de Supabase; **no debe** exponer si el correo existe. | Obligatorio |
| **RF-07** | **Cuando** un admin consulta usuarios, el sistema **debe** permitir listar, buscar y paginar; **debe** exponer estado y rol de cada usuario. | Obligatorio |
| **RF-08** | **Cuando** un admin suspende una cuenta, el sistema **debe** marcar el perfil como `Suspended` y **debe** bloquear sus operaciones autenticadas hasta su reactivación. | Obligatorio |
| **RF-09** | **Cuando** un admin reactiva una cuenta, el sistema **debe** restaurar el estado `Active` y **debe** permitir nuevamente sus operaciones autenticadas. | Obligatorio |
| **RF-10** | **Cuando** un usuario cierra sesión, el sistema **debe** revocar el JWT agregando su `jti` a Redis (`revoked:{jti}`) con TTL hasta `exp`; **si** Redis no responde, **debe** fallar cerrado. | Obligatorio |
| **RF-11** | **Cuando** un usuario solicita la baja de su cuenta, el sistema **debe** aplicar baja lógica/anonimización (sin borrado duro); la política de retención queda sujeta a DEC-003. | Obligatorio |

### 4.1 Validación de email institucional

- Dominio permitido: `@uttt.edu.mx` exacto (comparación sobre la parte posterior al último `@`, case-insensitive, sin subdominios).
- No se exige verificación de email en el MVP (Supabase puede emitir el token sin `email_confirmed`).
- El correo no se reutiliza entre cuentas activas; un correo dado de baja no habilita un registro nuevo sin revisión (ver § 6).

---

## 5. Requisitos No Funcionales

| ID | Requisito | Verificación |
|---|---|---|
| **RNF-01** | Todo endpoint autenticado **debe** validar el JWT de Supabase (firma, issuer, audience, expiración) y **debe** consultar la blacklist `revoked:{jti}`. | `UtttGo.SecurityTests` |
| **RNF-02** | La blacklist y el rate limit de `/auth/*` **deben** ser fail-closed si Redis no responde. | Prueba con Redis caído |
| **RNF-03** | Los roles y el estado de cuenta **deben** leerse del perfil local en MongoDB; **nunca** del JWT ni de un campo enviado por el cliente. | Revisión de código + tests de autorización |
| **RNF-04** | El rate limiting **debe** ser distribuido con Redis y **debe** responder `429` con el contrato de error único. | Tests de integración |
| **RNF-05** | Las contraseñas **nunca** se almacenan ni procesan en el backend; solo Supabase Auth las gestiona. | Revisión de código |
| **RNF-06** | El endpoint de recuperación **no debe** filtrar la existencia del correo (respuesta uniforme). | Test de enumeración |
| **RNF-07** | Toda respuesta de error **debe** incluir `correlationId` y seguir el contrato de `constitution.md`. | Tests de contrato |
| **RNF-08** | Los listados administrativos **deben** paginar con `page` + `pageSize` y un máximo configurable. | Tests de paginación |
| **RNF-09** | Las operaciones administrativas **deben** quedar auditadas (quién, qué, cuándo, `correlationId`) sin datos sensibles. | Revisión de logs/auditoría |
| **RNF-10** | Los secretos (JWT secret, connection strings) **deben** venir de Key Vault o variables de entorno; prohibido en Git/logs. | Secret scanning |
| **RNF-11** | Todos los endpoints **deben** exponerse bajo `/api/v1` y usar HTTPS. | Revisión de rutas |
| **RNF-12** | La autorización **debe** validar por recurso: un usuario solo edita su propio perfil; un admin accede a recursos administrativos. | Tests de autorización |
| **RNF-13** | El avatar, si se envía, **debe** referenciar un `mediaAssetId` en estado `Approved` conforme a la constitución; nunca una URL arbitraria. | Tests de media |
| **RNF-14** | Las fechas **deben** persistirse y exponerse en UTC; dinero fuera de este módulo. | Revisión de modelo |

---

## 6. Casos Límite

| ID | Caso | Comportamiento esperado |
|---|---|---|
| CL-01 | Registro con dominio distinto a `@uttt.edu.mx` | `422` con mensaje de dominio no permitido; no se crea cuenta. |
| CL-02 | Registro con correo ya existente activo | `409 Conflict`; no se duplica el perfil. |
| CL-03 | Registro con correo de una cuenta dada de baja | Rechazo controlado (`409`); requiere revisión (DEC-003). |
| CL-04 | Token expirado, inválido o con `jti` revocado | `401`; operación denegada. |
| CL-05 | Redis no disponible en logout o rate limit | Fail-closed: `503`/`401`; no se permite la operación. |
| CL-06 | Usuario suspendido intenta operar | `403`; se informa estado suspendido. |
| CL-07 | Admin intenta suspender su propia cuenta | `409`; operación no permitida. |
| CL-08 | No-admin accede a endpoints administrativos | `403`. |
| CL-09 | Autoactivación de vendedor con campos obligatorios incompletos | `422` con detalle de campos. |
| CL-10 | Reactivación de cuenta no suspendida | `409` (transición inválida). |
| CL-11 | Baja de cuenta con órdenes/reseñas históricas | Baja lógica/anonimización; historial se conserva según DEC-003 (abierta). |
| CL-12 | Avatar con `mediaAssetId` no aprobado o inexistente | `422`; no se asigna. |
| CL-13 | Solicitud de recuperación para correo inexistente | Respuesta uniforme `202`; no se revela existencia. |

---

## 7. Contratos (resumen)

Endpoints previstos (detalle de DTOs en `plan.md`/`task.md` de la fase):

| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| POST | `/api/v1/auth/register` | Público | Registro con email `@uttt.edu.mx` + password (vía Supabase). |
| POST | `/api/v1/auth/login` | Público | Login email + password. |
| POST | `/api/v1/auth/recover` | Público | Recuperación por email (respuesta uniforme). |
| POST | `/api/v1/auth/logout` | Autenticado | Revoca `jti` en Redis. |
| GET | `/api/v1/users/me` | Autenticado | Perfil propio. |
| PATCH | `/api/v1/users/me` | Autenticado | Actualiza perfil propio. |
| POST | `/api/v1/users/me/delete` | Autenticado | Baja lógica/anonimización. |
| POST | `/api/v1/sellers/activate` | Autenticado | Autoactivación de vendedor + perfil de negocio. |
| GET | `/api/v1/admin/users` | Admin | Lista/busca usuarios (paginado). |
| PATCH | `/api/v1/admin/users/{id}/suspend` | Admin | Suspende cuenta. |
| PATCH | `/api/v1/admin/users/{id}/reactivate` | Admin | Reactiva cuenta. |

Contrato de error: único de la constitución, con `code`, `message`, `correlationId` y `details` opcionales.

---

## 8. Fuera de Alcance

- Verificación de email obligatoria (no se exige en MVP).
- Registro de vendedores con aprobación administrativa (DEC-002 resuelta a favor de autoactivación para el MVP).
- OAuth, magic link, SSO y proveedores sociales.
- 2FA/MFA, lockout local, Argon2id/BCrypt local.
- Cambio de email y de contraseña desde el backend (se delegan a Supabase Auth).
- Borrado duro de cuentas y definición de retención/anonimización final (DEC-003 abierta).
- Asignación de roles fuera de las operaciones administrativas definidas.
- Gestión de ofertas, productos, pedidos y notificaciones (otros módulos).

---

## 9. Tests y Criterios de Aceptación

| ID | Nivel | Verifica |
|---|---|---|
| T-AUTH-01 | Integración | Registro acepta `@uttt.edu.mx` y rechaza otros dominios (`422`). |
| T-AUTH-02 | Integración | Login válido/inválido vía Supabase (mockeado en CI). |
| T-AUTH-03 | Integración | Creación de perfil local con rol `Buyer` y `sub` de Supabase. |
| T-AUTH-04 | Seguridad | JWT inválido/expirado/revocado rechazado (`401`). |
| T-AUTH-05 | Resiliencia | Redis caído ⇒ blacklist/rate limit fail-closed. |
| T-AUTH-06 | Seguridad | No-admin recibe `403` en admin; usuario no edita perfil ajeno. |
| T-AUTH-07 | Integración | Suspensión bloquea operaciones; reactivación las restaura. |
| T-AUTH-08 | Seguridad | Recuperación no revela existencia de correo. |
| T-AUTH-09 | Integración | Autoactivación de vendedor exige campos obligatorios. |
| T-AUTH-10 | Integración | Baja de cuenta es lógica/anonimización, sin borrado duro. |

Criterios de aceptación:

- [ ] **AC-001** Solo se aceptan correos `@uttt.edu.mx` exactos.
- [ ] **AC-002** Auth exclusivamente vía Supabase; sin contraseñas locales.
- [ ] **AC-003** Perfil local con rol/estado en MongoDB como fuente de autorización.
- [ ] **AC-004** Vendedor se autoactiva sin aprobación admin (MVP).
- [ ] **AC-005** Admin puede listar/buscar/suspender/reactivar.
- [ ] **AC-006** Logout revoca `jti` en Redis y falla cerrado si Redis cae.
- [ ] **AC-007** Baja de cuenta sin borrado duro, sujeta a DEC-003.
- [ ] **AC-008** Tests T-AUTH-01 a T-AUTH-10 en verde y trazabilidad actualizada.

---

## 10. Definición de Hecho (DoD)

- [ ] Este `spec.md` aprobado (2026-10-29) y registrado en `memoria.md`.
- [ ] `plan.md` y `task.md` de la fase derivados de este spec.
- [ ] Contratos/DTOs y validaciones implementados conforme a § 7.
- [ ] Build Release y `dotnet format` en verde.
- [ ] T-AUTH-01 a T-AUTH-10 en verde.
- [ ] Sin secretos en el diff; configuración vía Key Vault/entorno.
- [ ] Trazabilidad `spec ↔ endpoint ↔ test` actualizada.
- [ ] DEC-002 marcada como resuelta; DEC-003 documentada como pendiente.

---

## 11. Aprobación y Control de Cambios

| Campo | Valor |
|---|---|
| Estado | **Aprobado** |
| Fecha | 2026-10-29 |
| Versión | 1.0 |
| Autor | AGENT-BE-02 (API Designer) |
| Revisores | AGENT-BE-04 (Security Reviewer), AGENT-BE-06 (Memory Keeper) |
| Aprobado por | Equipo UTTT-GO |
| Próxima revisión | Al cerrar DEC-003 o al cambiar la constitución |

### 11.1 Registro de versiones

| Versión | Fecha | Cambio | Aprobado por |
|---|---|---|---|
| 1.0 | 2026-10-29 | Formalización del módulo de autenticación y usuarios (discovery de 5 preguntas) | Equipo UTTT-GO |

### 11.2 Reglas de control de cambios

1. Cambiar dominio de correo, flujo de vendedor o permisos admin exige nueva versión de este spec y registro en `memoria.md`.
2. Ningún agente implementa código que contradiga la constitución o este spec.
3. La resolución de DEC-003 actualizará RF-11, CL-11 y § 8.
