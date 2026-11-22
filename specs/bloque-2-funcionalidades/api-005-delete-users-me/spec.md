# API-005 - Eliminar cuenta propia - spec

Estado de aprobacion: APROBADA por el propietario, 2026-11-22.
Estado operativo: BLOCKED por falta de evidencia de contenedores.

## Identificacion y autoridad

`DELETE /api/v1/users/me`, conforme a RF-11, BR-011 y `docs/traceability.md`. La autoridad es `docs/constitution.md` -> esta ficha -> `plan.md` -> `task.md`.

## Responsabilidad

Identity coordina la baja de la cuenta autenticada y solo modifica `users`. Orders se consulta mediante `IActiveOrderReader`; Catalog recibe la solicitud mediante `IAccountDeletionCatalog`. Reviews, Chat, Media y otros datos compartidos no se modifican desde esta ficha.

## Politica aprobada DEC-003

1. La cuenta local pasa a `accountStatus=Deleted`.
2. Se anonimizan `displayName`, `email`, `phone` y `avatarMediaAssetId`; el correo es unico, no entregable y no reversible.
3. Se genera un `deletedUserRef` UUID aleatorio estable. Los datos compartidos conservan solo esa referencia cuando sus fichas dueñas lo soporten.
4. Los productos del vendedor se pausan mediante Catalog y no reciben nuevos pedidos.
5. Pedidos activos `Pending`, `Accepted`, `InPreparation` o `Ready` bloquean con `409`; estados terminales no bloquean.
6. La sesion actual se revoca en Redis con la politica de API-003: `revoked:{id}` hasta `exp`.
7. El logout global de Supabase es best-effort; no se usa `service_role` ni Admin API y su fallo no convierte una baja local completada en una respuesta falsa de error.

## Contrato HTTP

| Resultado | Contrato |
|---|---|
| `204` | Baja local completada, sin body. |
| `401 UNAUTHORIZED` | Bearer ausente, invalido, expirado, revocado, sujeto invalido o perfil ausente. |
| `409 ACTIVE_ORDER_CONFLICT` | Pedido activo; no se muta la cuenta. |
| `503 ACCOUNT_DELETION_UNAVAILABLE` | Mongo, Orders, Catalog o Redis no disponible; fail-closed. |

No hay body, query ni `userId` de cliente. El sujeto proviene del JWT validado.

## Comportamiento

`DeleteOwnAccountService` valida la sesion, consulta pedidos activos, aplica una actualizacion CAS en `users`, pausa productos, revoca el JWT y solicita logout global best-effort. Los pasos locales son idempotentes; un reintento con perfil `Deleted` continua los pasos pendientes.

## Seguridad y datos

- El cliente no decide identidad, estado, `deletedUserRef` ni productos.
- No se registran PII, tokens, `jti`, secretos ni respuestas completas de Supabase.
- Redis es fail-closed para la revocacion.
- Identity no accede a colecciones de otros modulos.
- Pedidos, reseñas y chat no se eliminan desde esta ficha; sus referencias futuras deben usar `deletedUserRef` mediante contratos dueños.

## Escenarios

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| DEL-01 | Sesion valida, sin pedidos activos y dependencias disponibles | DELETE | Anonimiza, pausa si aplica, revoca y responde `204`. |
| DEL-02 | Pedido activo como comprador o vendedor | DELETE | `409`, sin mutaciones. |
| DEL-03 | Perfil ya `Deleted` tras fallo posterior | Repetir DELETE | Continua pasos pendientes de forma idempotente. |
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
| DEL-01/02/05 | SIN_EVIDENCIA | Integracion con Mongo/Redis/Testcontainers. |
| DEL-03/04/06 | IMPLEMENTADO_PROBADO | Unitarias y gates existentes; seguridad completa pendiente. |

Build y pruebas locales con dobles no acreditan integracion real. No se considera `VERIFIED` hasta ejecutar las suites con Docker en un entorno no productivo.
