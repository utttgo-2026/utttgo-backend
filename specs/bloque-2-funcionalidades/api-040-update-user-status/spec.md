# API-040 — Cambiar estado de cuenta de usuario · spec

**Estado de aprobación de la ficha:** APROBADA explícitamente para API-040
**Estado operativo:** IMPLEMENTADO_PENDIENTE_DE_VERIFICACION_COMPLETA

## Identificación y autoridad

API-040 `PATCH /api/v1/admin/users/{userId}/status`, módulo `Admin`, según [trazabilidad](../../../docs/traceability.md), [phase-02](../../phase-02-auth-users/spec.md) (RF-08/09/14, T-AUTH-13, AC-005/AC-009) y la matriz de migración. [Constitución](../../../docs/constitution.md) → esta ficha → [plan](plan.md) → [task](task.md). La aprobación registrada en esta ficha ratifica el contrato y resuelve las decisiones abiertas; no cambia la constitución ni autoriza rutas adicionales.

Diseño consultado: [be-api-design](../../../skills/be-api-design.md), [be-architecture](../../../skills/be-architecture.md) y [be-memory](../../../skills/be-memory.md). DEC-018..021 permanecen vigentes.

## Aprobación y alcance

API-040 queda aprobada explícitamente para suspender y reactivar cuentas existentes mediante una sola ruta administrativa. El actor debe ser `Admin` local y `Active`; no puede cambiar su propia cuenta. La ruta no cambia roles, no elimina cuentas, no lista usuarios y no usa `service_role` ni Admin API de Supabase.

La implementación existente se documenta como pendiente de verificación completa. Referencias naturales: [controlador](../../../src/UTTTGO.Api/Modules/Admin/Controllers/AdminUsersStatusController.cs), [servicio](../../../src/UTTTGO.Api/Modules/Admin/Application/AdminUserStatusService.cs), [contratos](../../../src/UTTTGO.Api/Modules/Admin/Contracts/AdminUserStatusContracts.cs), [validador](../../../src/UTTTGO.Api/Modules/Admin/Validators/AdminUserStatusRequestValidator.cs), [adaptadores](../../../src/UTTTGO.Api/Modules/Admin/Infrastructure/AdminUserStatusAdapters.cs) y [revocador de sesiones](../../../src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisAccountSessionRevoker.cs).

## Contrato aprobado

### Entrada y éxito

- `PATCH /api/v1/admin/users/{userId}/status` requiere Bearer JWT validado y perfil local `Admin`/`Active`.
- El cuerpo es cerrado y solo admite `{ "accountStatus": "Active" | "Suspended" }`; campos extra, ausentes o valores distintos producen `400 VALIDATION_ERROR`.
- El objetivo inexistente produce `404 USER_NOT_FOUND`.
- El objetivo igual al actor produce `409 ADMIN_SELF_STATUS_CHANGE_FORBIDDEN`.
- Cambiar a `Suspended` o `Active` responde `200` con `{ "userId": "string", "accountStatus": "Active" | "Suspended" }`.
- Suspender un usuario ya `Suspended` es idempotente y responde `200`; no repite revocación, eliminación de tokens, pausa de productos ni auditoría de mutación.
- Reactivar solo acepta `Active`. No reactiva productos pausados anteriormente.

Todos los errores usan el contrato constitucional con `correlationId`, `status`, `code`, `message` y `details`.

| Situación | HTTP/código |
|---|---|
| JWT ausente, inválido, expirado o revocado | `401 UNAUTHORIZED` |
| Actor sin rol `Admin` | `403 FORBIDDEN` |
| Actor no `Active` | `401 UNAUTHORIZED` |
| Self-target | `409 ADMIN_SELF_STATUS_CHANGE_FORBIDDEN` |
| Cuerpo inválido o cerrado violado | `400 VALIDATION_ERROR` |
| Usuario objetivo inexistente | `404 USER_NOT_FOUND` |
| Límite excedido | `429 RATE_LIMIT_EXCEEDED` |
| MongoDB, Redis, Identity, catálogo, tokens o auditoría no disponibles | `503 DEPENDENCY_UNAVAILABLE` |

## Efectos y seguridad

1. El backend valida identidad, blacklist Redis y perfil local; nunca toma roles o estado del JWT ni del cuerpo.
2. La suspensión actualiza el perfil local a `Suspended`, revoca todas las sesiones activas en Redis mediante `revoked:{session_id|jti|sha256}` con TTL hasta `exp`, elimina los `deviceTokens` del usuario y pausa los productos del vendedor, si aplica.
3. La reactivación actualiza el perfil local a `Active`, no revoca ni recrea sesiones y no reactiva productos pausados.
4. Login de una cuenta suspendida responde `403 ACCOUNT_SUSPENDED` y no emite sesión. Una sesión autenticada de cuenta suspendida responde `403 SESSION_ACCOUNT_SUSPENDED`; la consulta del perfil local ocurre antes de autorizar operaciones.
5. La auditoría se guarda en MongoDB, colección `adminUserStatusAuditLogs`, sin PII: `actorId`, `targetId`, `action`, estado previo/nuevo, `occurredAtUtc` UTC y `correlationId`. No se guardan tokens, credenciales ni payloads sensibles.
6. El rate limit es distribuido, `30` solicitudes por `60` segundos por administrador, con Redis. Redis no disponible implica fail-closed: `503` y ninguna mutación parcial.
7. La operación no usa `service_role`; las fronteras se mantienen mediante interfaces de Identity, Catalog y Notifications. No se cambia el orden del middleware.

La respuesta `200` solo se permite después de completar los efectos requeridos para la transición. Un fallo de dependencia produce `503`; el diseño no confirma una transición parcial como éxito.

## Escenarios Given/When/Then

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| ADMS-01 | Admin `Active` y objetivo existente distinto | Solicita `Suspended` | `200`; perfil local suspendido, sesiones revocadas, tokens eliminados, productos pausados y auditoría sin PII |
| ADMS-02 | Admin `Active` y objetivo suspendido | Repite `Suspended` | `200` idempotente, sin efectos secundarios repetidos |
| ADMS-03 | Admin `Active` y objetivo suspendido | Solicita `Active` | `200`; perfil activo, sin reactivación de productos |
| ADMS-04 | Actor apunta a sí mismo | Envía cualquier estado | `409 ADMIN_SELF_STATUS_CHANGE_FORBIDDEN`, sin escritura |
| ADMS-05 | Cuerpo con estado distinto de `Active`/`Suspended` o campos extra | Envía PATCH | `400 VALIDATION_ERROR`, sin escritura |
| ADMS-06 | Objetivo inexistente | Envía PATCH | `404 USER_NOT_FOUND` |
| ADMS-07 | Cuenta suspendida | Intenta login o usa sesión previa | `403 ACCOUNT_SUSPENDED` o `403 SESSION_ACCOUNT_SUSPENDED`; no se autoriza sesión/operación |
| ADMS-08 | Redis no disponible o 31.ª solicitud en 60 s | Envía PATCH | `503 DEPENDENCY_UNAVAILABLE` o `429 RATE_LIMIT_EXCEEDED`, sin mutación parcial |

## Trazabilidad y preparación

| Fuente | Escenarios |
|---|---|
| RF-08, RF-09, RF-14, AC-005, AC-009 | ADMS-01/02/03/07 |
| T-AUTH-13, CL-16 | ADMS-04/07 |
| Constitución §§ 4, 6, 14 y 15 | ADMS-01/04/05/08 |

La ficha es apta para mantener la implementación existente y verificarla; no declara verificación completa ni cierra gates de CI o pruebas externas.

## Evidencia

No hay registro `Verificado` todavía. La implementación se encuentra documentada como pendiente de verificación completa: falta ejecutar el curl autenticado y las pruebas identificadas para ADMS-01..08 en entorno no productivo, con MongoDB/Redis y dobles de Supabase/servicios externos. La inspección de archivos reales anterior solo establece presencia de rutas, contratos y adaptadores; no acredita comportamiento extremo a extremo.
