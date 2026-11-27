# API-005 - Eliminar cuenta propia - spec

Estado de aprobacion: APROBADA por el propietario, 2026-11-22.
Estado operativo: HARDENING IMPLEMENTADO EN RAMA; pendiente validacion con Docker activo y prueba dedicada del dispatcher S3.

## Identificacion y autoridad

`DELETE /api/v1/users/me` y `GET /api/v1/users/me/deletion/{operationId}`, conforme a RF-11, BR-011 y `docs/traceability.md`. La autoridad es `docs/constitution.md` -> esta ficha -> `plan.md` -> `task.md`.

## Responsabilidad

Identity coordina la baja de la cuenta autenticada y solo modifica `users` y `accountDeletionOperations`. Orders se consulta mediante `IActiveOrderReader`; Catalog recibe la solicitud mediante `IAccountDeletionCatalog`; Media recibe una orden idempotente mediante `IAccountDeletionMedia` y es el unico modulo que elimina sus assets. Reviews, Chat y otros datos compartidos no se modifican desde esta ficha.

## Politica aprobada DEC-003

1. La cuenta local pasa a `accountStatus=Deleted`.
2. Se anonimizan `displayName`, `email`, `phone` y `avatarMediaAssetId`; el correo es unico, no entregable y no reversible.
3. Se genera un `deletedUserRef` UUID aleatorio estable. Los datos compartidos conservan solo esa referencia cuando sus fichas dueñas lo soporten.
4. Los productos del vendedor se pausan mediante Catalog y no reciben nuevos pedidos.
5. Pedidos activos `Pending`, `Accepted`, `InPreparation` o `Ready` bloquean con `409`; estados terminales no bloquean.
6. Antes de cualquier mutacion destructiva se crea una operacion en `accountDeletionOperations` y se revoca la sesion actual en Redis con la politica de API-003: `revoked:{id}` hasta `exp`.
7. El logout global de Supabase es best-effort; no se usa `service_role` ni Admin API y su fallo no convierte una baja local completada en una respuesta falsa de error.
8. Tras la anonimización y pausa de publicaciones, Media registra un outbox idempotente por `ownerUserId` + `deletedUserRef`; un dispatcher elimina objetos staging/approved de S3 y marca los `mediaAssets` como `Deleted` con reintentos y lease.
9. Un worker de Identity retoma operaciones `Pending`, `RequiresIntervention` y `Processing` obsoletas (lease stale de un minuto) tras un fallo o reinicio. Nunca se persiste el access token ni se depende de su TTL para continuar.
10. Identity y Orders comparten un fence Redis por usuario/vendedor. DELETE conserva el fence hasta marcar la cuenta y pausar publicaciones; Orders vuelve a validar comprador y producto bajo el mismo fence antes de reservar stock y persistir.

## Contrato HTTP

| Resultado | Contrato |
|---|---|
| `204` | Baja local y encolado de purga completados, sin body. La purga fisica de S3 continua asincronicamente. |
| `401 UNAUTHORIZED` | Bearer ausente, invalido, expirado, revocado, sujeto invalido o perfil ausente. |
| `409 ACTIVE_ORDER_CONFLICT` | Pedido activo; no se muta la cuenta. |
| `400 ACCOUNT_DELETION_HEADERS_REQUIRED` | Faltan `X-Account-Deletion-Id` (UUID) o `X-Account-Deletion-Proof` (secreto aleatorio de al menos 32 caracteres). |
| `503 ACCOUNT_DELETION_UNAVAILABLE` | Dependencia no disponible. Incluye `operationId`, `state` y `stage` cuando la operacion pudo persistirse. Reintento seguro con los mismos headers. |
| `200` en consulta | Estado sin PII: `Pending`, `Processing`, `Completed` o `RequiresIntervention`, incluyendo `mediaPurge.status` (`NotQueued`, `Pending`, `Processing`, `Failed`, `Completed`). |
| `404 ACCOUNT_DELETION_NOT_FOUND` | La pareja `operationId` + prueba no existe. |

DELETE no recibe body ni `userId`. Requiere los headers de idempotencia y prueba. El sujeto proviene del JWT validado. La consulta de estado no usa JWT: exige la prueba opaca asociada al identificador, almacenada solo como hash.

## Comportamiento

`DeleteOwnAccountService` exige `auth_time` del JWT validado con antigüedad maxima de 15 minutos, adquiere el fence compartido, consulta pedidos activos, persiste la operacion, revoca el JWT antes de mutar, aplica la actualizacion CAS en `users`, pausa productos y encola la purga de Media. Un worker reanuda las etapas persistidas tras un fallo; si el JWT expiro antes de recuperar una operacion en `Created`, la revocacion ya no es necesaria porque el token no es reutilizable. El logout global de Supabase es best-effort. La confirmacion `204` requiere que la orden de purga de Media haya quedado persistida; `Completed` significa que la purga fue encolada, mientras `mediaPurge.status=Completed` confirma la purga fisica observada por Media.

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
- La reautenticacion reciente se valida en backend mediante `auth_time`; una confirmacion visual en la app no es suficiente.
- La prueba de operacion nunca se devuelve completa, no se registra en logs y no permite consultar otra cuenta.
- Orders y Identity deben tomar el fence en orden canonico; una carrera gana el fence, y el perdedor revalida estado antes de crear pedido o mutar la cuenta.

## Escenarios

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| DEL-01 | Sesion valida, sin pedidos activos y dependencias disponibles | DELETE | Anonimiza, pausa si aplica, persiste outbox de Media, revoca y responde `204`. |
| DEL-02 | Pedido activo como comprador o vendedor | DELETE | `409`, sin mutaciones. |
| DEL-03 | Operacion `Pending`, `RequiresIntervention` o `Processing` stale tras fallo posterior | Worker | Continua las etapas pendientes de forma idempotente. |
| DEL-04 | JWT ausente, invalido, expirado o revocado | DELETE | `401`, sin mutaciones. |
| DEL-05 | Mongo, Orders, Catalog, Redis, Supabase o Media falla | DELETE, worker o consulta | `503` con operacion persistida cuando aplica; Supabase no bloquea la baja local y el worker no depende del TTL JWT. |
| DEL-07 | Pedido intenta entrar durante DELETE | DELETE y Orders concurrentes | El fence compartido fuerza `409`, `ACCOUNT_NOT_ACTIVE` o `PRODUCT_NOT_AVAILABLE`; no se reserva stock ni se crea pedido después de la baja. |
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
| DEL-01/02/05/07 | IMPLEMENTADO_LOCAL | Build, unitarias e integración de endpoint; suites completas de cierre pendientes. |
| DEL-03/04/06 | IMPLEMENTADO_LOCAL | Fence, recuperación stale, recuperación post-TTL y estado Media implementados; falta evidencia controlada de staging y dispatcher S3. |

Las suites con Docker acreditan el gate local, pero no la integración externa. No se considera `VERIFIED` la purga hasta comprobar el dispatcher con un emulador S3/doble controlado en entorno no productivo.
