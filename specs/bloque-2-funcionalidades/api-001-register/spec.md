# API-001 — Registro · spec

> Lote 2a · 2026-11-12 · **Pendiente de aprobación**. La autorización individual histórica para desarrollo local y el `202` genérico no aprueban el contrato completo ni esta ficha.

Autoridad: [constitución vigente](../../../docs/constitution.md) §§ 3, 6, 14 → esta ficha → [plan](plan.md) → [tareas](task.md). Origen conservado: [fase 02](../../phase-02-auth-users/spec.md) §§ 4–5, 9 y [tareas de fase 02](../../phase-02-auth-users/task.md); la ficha API-001 del Excel sigue «Propuesta a validar».

## Alcance y contrato vigente (código, no ratificación)

| Contrato vigente | Comportamiento y fuente real |
|---|---|
| Ruta/método | `POST /api/v1/auth/register` ([`AuthController`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs)). |
| Autenticación | Público ([`AuthController`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs)). |
| Entrada (campos y validaciones) | JSON camelCase `{email, password, displayName}` ([DTO](../../../src/UTTTGO.Api/Modules/Identity/Contracts/RegisterRequest.cs)); el [validador](../../../src/UTTTGO.Api/Modules/Identity/Validators/RegisterRequestValidator.cs) exige los tres campos, email bien formado y ningún campo extra (incluidos `roles`/`accountStatus`); no fija longitud ni fortaleza de contraseña. El [controlador](../../../src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs) normaliza email con `Trim`/minúsculas y nombre con `Trim`; solo admite dominio **exacto** `@uttt.edu.mx` (sin subdominios, ignorando mayúsculas). |
| Límites | [Filtro](../../../src/UTTTGO.Api/Modules/Identity/Controllers/RegistrationRequestSizeFilter.cs) y [`RequestSizeLimit`](../../../src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs): **8 KiB, candidato local, no RNF aprobado**; exceso → `413 REQUEST_TOO_LARGE`. [Gate Redis](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisIdentityGates.cs) `auth-register` por IP; [`RateLimiting.AuthRegister` en `appsettings.json`](../../../src/UTTTGO.Api/appsettings.json): 5 solicitudes/900 s por IP, **provisionalmente, por decisión del responsable, no aprobado**. |
| Salidas | [Controlador](../../../src/UTTTGO.Api/Modules/Identity/Controllers/AuthController.cs): tabla de respuestas vigente a continuación; códigos distintos del `202`, texto exacto del DTO y tamaño siguen sujetos a ratificación. |
| Dependencias | [Servicio](../../../src/UTTTGO.Api/Modules/Identity/Application/RegistrationService.cs): primero [gate Redis](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisIdentityGates.cs), comprueba disponibilidad del [repositorio Mongo `users`](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/MongoUserRepositories.cs), delega `signUp` a [Supabase](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/SupabaseAuthClient.cs) y, para identidad nueva con `sub`/email coincidentes, guarda perfil local `Buyer` pendiente de verificación ([contrato de perfil](../../../src/UTTTGO.Api/Modules/Identity/Application/IRegistrationDependencies.cs)); un duplicado procesable recibe el mismo `202` sin crear otro perfil. No persiste contraseña ni acepta rol del cliente; la sincronización sigue sujeta a ratificación. |

| Resultado actual | Condición / contenido público |
|---|---|
| `202 Accepted` | Solicitud válida y procesable, sea identidad nueva o ya existente: `{"message":"Si el registro puede procesarse, recibirás instrucciones para verificar tu correo."}`; sin identificador, rol, estado ni JWT. **Único éxito aprobado individualmente**, sin `409` por duplicado; no garantiza entrega de correo. |
| `400 VALIDATION_ERROR` | Body inválido, malformado o con campos extra; error con `correlationId`. |
| `422 INSTITUTIONAL_EMAIL_REQUIRED` | Correo bien formado de dominio no institucional. |
| `429 RATE_LIMITED` | Redis deniega cuota; `Retry-After` en segundos cuando el limitador calcula el reintento. |
| `503 AUTH_UNAVAILABLE` | Dependencia crítica/identidad inconsistente o Redis no disponible: fail-closed, sin confirmar registro. |

## Trazabilidad y escenarios de aceptación

Registro: **RF-01/03/15**; **RNF-02/03/04/05/07/10/11/15/17** (RNF-17 es propuesta OTP); **AC-001/002/003/008/010/011/012**. [Fase 02 § 4.2](../../phase-02-auth-users/spec.md#42-contratos-individuales-prioritarios-alcance-y-procedencia): Figma `HU-004/UI-004`, backlog `PB-002/HU-02` y verificación `PB-003/HU-03` (fuentes con numeraciones distintas). TASK-AUTH-001/006 `[x]` documentales; **002/007/008/010/011/012/020/030 `[ ]`**: el traslado no las cierra. Pruebas correspondientes: **T-AUTH-01/03/05/14/15/18/19**.

| Given / When / Then | Prueba requerida / límite actual |
|---|---|
| **Dado** un JSON válido con correo institucional, **cuando** se registra, **entonces** responde `202` genérico y, si es nuevo, guarda perfil `Buyer` pendiente ligado al `sub` de Supabase, sin JWT. | Dobles verifican respuesta y perfil simulado; confirmar identidad/persistencia e idempotencia reales por separado. |
| **Dado** correo de otro dominio, campo de rol inyectado o body > 8 KiB, **cuando** se envía, **entonces** devuelve respectivamente `422`, `400` o `413`, sin registrar. | Tests de dominio, validación y filtro; Kestrel/proxy real aún por probar. |
| **Dado** duplicado procesable, cuota agotada o dependencia caída, **cuando** se registra, **entonces** devuelve respectivamente el mismo `202`, `429` con `Retry-After` o `503` sin éxito ficticio. | Dobles; probar Redis distribuido, duplicados Supabase y fallos parciales Mongo/Supabase reales. |
| **Dado** correo sin verificar o código OTP incorrecto/vencido, **cuando** se intenta habilitar acceso, **entonces** no se autoriza operación privada. | Flujo integral de RF-15 pendiente: no se acredita con un `202` de registro. |

## Evidencia separada y pendientes

- **Con dobles** ([QA API-001](../../../docs/qa-api-001.md)): 53 pruebas informadas (16 unitarias, 28 integración, 9 seguridad): `RegistrationServiceTests`, `RegisterEndpointTests`, `RegisterSecurityTests`; validación, antienumeración simulada, `413`, `429` y `503`. No prueban proveedores reales ni resistencia temporal.
- **Real local (2026-11-06; Atlas + Redis + Supabase reales)**: registro de prueba con correo institucional → `202`; correo fuera del dominio (gmail) → `422 INSTITUTIONAL_EMAIL_REQUIRED`. Evidencia comunicada para estos dos casos, **no reejecutada aquí**; no demuestra OTP, entrega de correo, duplicados reales ni recuperación.
- **AWS (2026-11-12)**: `POST /api/v1/auth/register` figura en el OpenAPI del despliegue. **Ruta presente ≠ «Verificado»**: no hay curl de registro en AWS en esta evidencia.

**Pendiente:** RF-15, verificación por código/OTP y reconciliación de perfil; RF-06/BR-013, recuperación por enlace al correo registrado (sin inventar ruta backend). Aprobar contrato restante, entrega SMTP/flujo móvil y pruebas reales antes de cerrar tareas o AC; conservar fase 02 sin archivar.
