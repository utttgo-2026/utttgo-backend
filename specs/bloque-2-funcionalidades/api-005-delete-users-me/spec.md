# API-005 - Eliminar cuenta propia - spec

Estado de aprobacion: APROBADA por el propietario, 2026-11-22.
Estado operativo: IMPLEMENTADA; suites locales con Docker verdes; pendiente prueba dedicada del dispatcher S3 y verificacion externa.

## Identificacion y autoridad

`DELETE /api/v1/users/me`, conforme a RF-11, BR-011 y `docs/traceability.md`. La autoridad es `docs/constitution.md` -> esta ficha -> `plan.md` -> `task.md`.

## Responsabilidad

Identity coordina la baja de la cuenta autenticada y solo modifica `users`. Orders se consulta mediante `IActiveOrderReader`; Catalog recibe la solicitud mediante `IAccountDeletionCatalog`; Media recibe una orden idempotente mediante `IAccountDeletionMedia` y es el unico modulo que elimina sus assets. Reviews, Chat y otros datos compartidos no se modifican desde esta ficha.

## Politica aprobada DEC-003

1. La cuenta local pasa a `accountStatus=Deleted`.
2. Se anonimizan `displayName`, `email`, `phone` y `avatarMediaAssetId`; el correo es unico, no entregable y no reversible.
3. Se genera un `deletedUserRef` UUID aleatorio estable. Los datos compartidos conservan solo esa referencia cuando sus fichas dueñas lo soporten.
4. Los productos del vendedor se pausan mediante Catalog y no reciben nuevos pedidos.
5. Pedidos activos `Pending`, `Accepted`, `InPreparation` o `Ready` bloquean con `409`; estados terminales no bloquean.
6. La sesion actual se revoca en Redis con la politica de API-003: `revoked:{id}` hasta `exp`.
7. El logout global de Supabase es best-effort; no se usa `service_role` ni Admin API y su fallo no convierte una baja local completada en una respuesta falsa de error.
8. Tras la anonimización y pausa de publicaciones, Media registra un outbox idempotente por `ownerUserId` + `deletedUserRef`; un dispatcher elimina objetos staging/approved de S3 y marca los `mediaAssets` como `Deleted` con reintentos y lease.

## Contrato HTTP

| Resultado | Contrato |
|---|---|
| `204` | Baja local completada, sin body. |
| `401 UNAUTHORIZED` | Bearer ausente, invalido, expirado, revocado, sujeto invalido o perfil ausente. |
| `409 ACTIVE_ORDER_CONFLICT` | Pedido activo; no se muta la cuenta. |
| `503 ACCOUNT_DELETION_UNAVAILABLE` | Mongo, Orders, Catalog o Redis no disponible; fail-closed. |

No hay body, query ni `userId` de cliente. El sujeto proviene del JWT validado.

## Comportamiento

`DeleteOwnAccountService` valida la sesion, consulta pedidos activos, aplica una actualizacion CAS en `users`, pausa productos, encola la purga de Media, revoca el JWT y solicita logout global best-effort. La confirmacion `204` requiere que la orden de purga de Media haya quedado persistida. Los pasos locales son idempotentes; la continuidad posterior a la revocacion se realiza mediante el outbox, no mediante un nuevo DELETE con el mismo token.

## Seguridad y datos

- El cliente no decide identidad, estado, `deletedUserRef` ni productos.
- No se registran PII, tokens, `jti`, secretos ni respuestas completas de Supabase.
- Redis es fail-closed para la revocacion.
- Identity no accede a colecciones de otros modulos.
- Pedidos, reseñas y chat no se eliminan desde esta ficha; sus referencias futuras deben usar `deletedUserRef` mediante contratos dueños.
- Los pedidos compartidos se conservan temporalmente por su naturaleza transaccional; Orders debe anonimizar PII del participante usando `deletedUserRef` en su propia ficha, sin borrar la evidencia del pedido.
- Los productos no se borran: se pausan inmediatamente para impedir nuevas compras. Su eliminacion definitiva requiere una ficha de Catalog separada.
- Los assets de Media se eliminan de S3 mediante `mediaDeletionOutbox`; mientras exista un fallo de S3, el item queda pendiente/fallido para reintento.
- No se persiste ningun access token para reintentar Supabase. La revocacion local Redis es obligatoria; el logout global de Supabase se intenta con el token actual y queda explicitamente best-effort.

## Escenarios

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| DEL-01 | Sesion valida, sin pedidos activos y dependencias disponibles | DELETE | Anonimiza, pausa si aplica, persiste outbox de Media, revoca y responde `204`. |
| DEL-02 | Pedido activo como comprador o vendedor | DELETE | `409`, sin mutaciones. |
| DEL-03 | Perfil ya `Deleted` tras fallo posterior | Dispatcher | Continua la purga de Media con lease y reintentos de forma idempotente. |
| DEL-04 | JWT ausente, invalido, expirado o revocado | DELETE | `401`, sin mutaciones. |
| DEL-05 | Mongo, Orders, Catalog o Redis falla | DELETE o reintento | `503`, sin confirmar la baja. |
| DEL-06 | Perfil local `Deleted` | Login posterior | Se rechaza la operacion. |

## Trazabilidad

| Fuente | Cobertura |
|---|---|
| RF-11, BR-011, AC-007, T-AUTH-10 | DEL-01..06 |
| API-003 | Revocacion Redis y sesion actual |
| Orders | `IActiveOrderReader` |
| Catalog | `IAccountDeletionCatalog` |

## Brechas y evidencia

| Escenario | Estado | Evidencia pendiente |
|---|---|---|
| DEL-01/02/05 | LOCAL_DOCKER | Build Release, formato y suites Unit 409 / Integration 308 / Security 172 ejecutadas con Docker; falta prueba dedicada del dispatcher con doble/emulador S3. |
| DEL-03/04/06 | IMPLEMENTADO_PROBADO | Unitarias y gates existentes; falta prueba dedicada del dispatcher y evidencia externa. |

Las suites con Docker acreditan el gate local, pero no la integración externa. No se considera `VERIFIED` la purga hasta comprobar el dispatcher con un emulador S3/doble controlado en entorno no productivo.
