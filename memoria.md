# memoria.md - Backend UTTT-GO

Registro vivo de decisiones, contexto y evidencia. No contiene secretos ni
credenciales.

## Decisiones vigentes

| ID | Decisión | Estado |
|---|---|---|
| DEC-001 | Compra directa individual; no existe carrito. | Resuelta |
| DEC-002 | El vendedor se autoactiva al completar su perfil de negocio. | Resuelta |
| DEC-003 | API-005: baja lógica `Deleted`, anonimización de perfil, `deletedUserRef` UUID aleatorio, bloqueo por pedidos activos, productos de vendedor no disponibles, outbox de purga Media/S3, revocación Redis y logout global de Supabase best-effort; no se usa service_role/Admin API. | Aprobada por el propietario 2026-11-22; suites locales verdes; evidencia S3 y consistencia compartida pendientes |
| DEC-005 | El chat se conserva durante el pedido y se purga dentro de tres días. | Diseño técnico pendiente |
| DEC-008 | El único pago es efectivo presencial. | Resuelta |
| DEC-009 | No existen menús ni horarios. | Resuelta |
| DEC-010 | La moderación previa de imágenes usa Amazon Rekognition. Las etiquetas con confianza igual o superior a `MODERATION_MIN_CONFIDENCE` rechazan; errores producen `ProcessingFailed`. | Resuelta |
| DEC-016 | Cada vendedor administra sus propios productos/publicaciones. | Resuelta funcionalmente |
| DEC-018 | La infraestructura objetivo es AWS `us-east-2`: una EC2 t3.micro, dos réplicas Compose detrás de Nginx, S3 privado, Redis local y Terraform. | Resuelta; costo final pendiente |
| DEC-019 | API-016 no se implementa; se aprueba `GET /api/v1/sellers/me`; Media pertenece a fase 05. | Resuelta |
| DEC-020 | El puerto 22 permanece cerrado; el despliegue usa SSM y el presupuesto usa variable externa. | Resuelta |
| DEC-021 | Los logs de la EC2 se publican con `awslogs` en CloudWatch. | Resuelta |

## Estado de fase 05

- `mediaAssets` vive en MongoDB; S3 solo almacena objetos privados.
- El cliente recibe únicamente URLs presignadas generadas por el backend.
- La intención valida MIME, extensión y máximo `4_000_000` bytes.
- La confirmación verifica HEAD, obtiene los bytes desde staging y llama a
  Rekognition con la identidad AWS del entorno.
- Solo `Approved` puede asociarse a un producto; no existe fallback aprobador.
- `Rejected` elimina el objeto; fallos del proveedor dejan `ProcessingFailed`.
- El E2E local usa Mongo, Redis, LocalStack y un doble de Rekognition. No
  acredita permisos reales de AWS, privacidad del bucket ni llamadas externas.
- La rama actual es `feature/media-rekognition` en el worktree
  `C:\UTTT-Go\media-rekognition`; todavía no hay commit de esta migración.

## Evidencia previa

- `origin/main` está en `2049fbe` al iniciar esta migración.
- CD #175/run `44968803181` quedó verde antes de abrir esta rama.
- La sonda productiva verificada previamente fue `GET /health` HTTP 200 con
  MongoDB y Redis saludables.
- Las pruebas locales anteriores no son evidencia de IAM, S3 privado ni
  servicio de moderación real.

## Auditoría Orders 2026-11-23

- `origin/main` se actualizó a `9894adc`; la auditoría se realizó en la rama
  aislada `feature/orders-flow-verification`, sin cambios de producción.
- Orders ya cubre creación, listado del comprador, detalle, cancelación,
  transiciones del vendedor, token de entrega y confirmación de entrega.
- La persistencia usa `MongoOrderRepository`, índices de `orders` y
  `orderAuditLogs`, CAS por `status`/`version`, y DI resuelta en `Program.cs`.
- Redis aplica los límites `order-*` mediante `RedisOrdersRateLimitGate` con
  comportamiento fail-closed.
- Validación con Docker activo: build Release correcto con 0 errores; `format`
  correcto; unitarias 386/386, integración 292/292 y seguridad 159/159.
- El build emitió cuatro advertencias `CS8034` del analizador de
  `AWSSDK.Core`, bloqueado por la política de control de aplicaciones local;
  no es un error del código de Orders.

## Pendientes

## Aprobación API-040 2026-11-25

- API-040 queda aprobada explícitamente en `specs/bloque-2-funcionalidades/api-040-update-user-status/spec.md`.
- Contrato resuelto: cuerpo cerrado `Active`/`Suspended`; self-target `409 ADMIN_SELF_STATUS_CHANGE_FORBIDDEN`; suspensión idempotente `200`; objetivo inexistente `404 USER_NOT_FOUND`; inválido `400`; dependencias `503`.
- La suspensión actualiza el perfil local, revoca sesiones con `revoked:{session_id|jti|sha256}` y TTL hasta `exp`, elimina `deviceTokens` y pausa productos del vendedor. Reactivar solo activa el perfil y no reactiva productos.
- Login y sesión de cuenta suspendida quedan en `403 ACCOUNT_SUSPENDED` y `403 SESSION_ACCOUNT_SUSPENDED`. Auditoría Mongo sin PII en `adminUserStatusAuditLogs`; rate limit Redis `30/60` por administrador y fail-closed. No se usa `service_role`.
- La implementación real está presente en `src/UTTTGO.Api/Modules/Admin/` y `src/UTTTGO.Api/Modules/Identity/Infrastructure/RedisAccountSessionRevoker.cs`; el estado es implementado pendiente de verificación completa, sin afirmar evidencia de curl o tests aún no ejecutados.

## Implementación Chat API-037/API-038 2026-11-25

- Rama aislada `feature/chat-api-037-038`, creada desde `origin/main` `66fccdc`.
- Contrato ratificado para esta entrega: historial `page/pageSize` máximo 50; envío `{content}` sanitizado a 1..1000 caracteres; `Idempotency-Key` obligatorio; `404 ORDER_NOT_FOUND` para no participantes; `409 ORDER_CHAT_CLOSED` para pedidos terminales.
- Chat posee la colección Mongo `orderMessages`, índices de orden/idempotencia y consume `IOrderChatParticipantReader` de Orders; no lee colecciones ajenas. La autorización usa `IAuthenticatedLocalProfileReader`.
- SignalR, FCM de mensajes, purga programada dentro de tres días y coordinación DEC-003 quedan como trabajo posterior. Build Release, format y suites Unit/Integration/Security se ejecutaron con Docker activo el 2026-11-25: 409/308/172 pruebas superadas.

## Implementación Reviews 2026-11-23

- Autorización explícita del propietario para implementar API-030 y API-031 en
  `feature/reviews-api-030-031`, sin Favoritos ni API-041.
- Reviews posee `reviews`, su índice único por `orderId`, índices de lectura y
  métricas agregadas. API-030 consume `IDeliveredOrderReader` de Orders; no lee
  `orders` directamente.
- API-031 es público y limita por IP en Redis; sus respuestas no exponen
  `buyerUserId`, `orderId` ni PII. Sellers/Catalog reciben métricas mediante
  interfaces de lectura de Reviews.

## Spec FCM 2026-11-23

- Rama documental `docs/spec-fcm`: solo spec/plan/task; no código, IAM/SCP,
  Terraform ni credenciales.
- La propuesta cubre registro/baja de `deviceTokens`, eventos de Orders,
  `INotificationSender`, Firebase Admin SDK, outbox no bloqueante, reintentos
  acotados, limpieza `UNREGISTERED` y SSM Parameter Store.
- Implementación FCM fusionada en `main` por PR #58: `FirebaseAdmin 3.4.0`, tokens
  cifrados con Data Protection y hash SHA-256, endpoints de registro/baja,
  outbox Mongo y dispatcher con reintentos acotados. Orders publica después de
  persistir su cambio y no espera a FCM; fallos de push no revierten el pedido.
- La credencial se lee solo desde `FIREBASE_CREDENTIALS_JSON` o
  `FIREBASE_CREDENTIALS_PATH`; si falta, el sender registra un warning y queda
  desactivado. La propagación de la ruta y el montaje de solo lectura se revisan
  en la rama operativa independiente `ops/fcm-credentials`; no se registran
  valores de credencial.
- Evidencia local: build Release correcto con cuatro warnings `CS8034` del
  analizador AWS bloqueado por la política local; unitarias `391/391`, FCM
  unitarias `2/2`, integración FCM aislada `1/1` y seguridad FCM aislada `2/2`.
  Las suites completas de integración y seguridad quedaron pendientes en el
  corte anterior porque Docker Desktop no estaba disponible.
- API-035/036 y Chat permanecen fuera de alcance; las preguntas abiertas están
  en `specs/phase-06-notifications-push/spec.md`.

- DEC-003/API-005 fue aprobada el 2026-11-22. Los contratos mínimos `IActiveOrderReader`
  e `IAccountDeletionCatalog` ya están publicados; Reviews y mensajes quedan para
  sus fichas dueñas. API-005 persiste `mediaDeletionOutbox` mediante
  `IAccountDeletionMedia`; Media elimina el `ObjectKey` registrado y las rutas
  conocidas de staging/approved en S3, marca assets `Deleted` y reintenta también
  registros `Failed`. Las suites locales ya se ejecutaron con Docker disponible;
  falta una prueba dedicada del dispatcher contra doble/emulador S3 y evidencia
  externa no productiva.

## Robustecimiento API-005 2026-11-26

- Rama aislada `feat/account-deletion-hardening`, creada desde `origin/main` actualizado.
- Se añadió `accountDeletionOperations` en Identity con prueba hasheada, estado y etapa persistentes; el worker recupera `Pending` y `RequiresIntervention` después de reinicios.
- `DELETE /api/v1/users/me` exige `X-Account-Deletion-Id`, `X-Account-Deletion-Proof` y `auth_time` reciente (máximo 15 minutos). La consulta segura es `GET /api/v1/users/me/deletion/{operationId}` y no requiere reutilizar el JWT revocado.
- `DELETE /api/v1/users/me` conserva el bloqueo previo de pedidos `Pending`, `Accepted`, `InPreparation` y `Ready`; después de la anonimización CAS pausa publicaciones del vendedor y persiste una orden idempotente `ownerUserId + deletedUserRef` en `mediaDeletionOutbox`.
- Media es dueño de la purga: el dispatcher elimina objetos staging/approved de S3 con lease/reintentos y marca `mediaAssets` como `Deleted`. No se guardan access tokens ni credenciales.
- Supabase global logout continúa best-effort con el token actual; Redis revoca localmente de forma fail-closed. No se usa `service_role` ni Admin API.
- Build Release y Unit 415 pasan. Docker Engine quedó disponible después de la primera ejecución; Integration 308 y Security 172 pasan con Docker activo. Falta una prueba dedicada del dispatcher S3.
- El dispatcher conserva lease y reintentos sin límite fijo para evitar abandonar purgas tras ocho intentos; esto no sustituye una transacción compartida entre Identity, Orders y Catalog.

- Rama `feature/auth-logout` aprobada el 2026-11-21: implementa API-003 con `204`, `401` para token ausente/inválido/expirado/revocado, `503` si Redis falla, `jti` obligatorio, `SET NX` y TTL hasta `exp`; no implementa refresh token.

- Ejecutar build Release y las suites unitarias, integración y seguridad con
  Docker encendido antes de cualquier commit.
- Actualizar el snapshot OpenAPI y los ejemplos móviles del PATCH de producto.
- Preparar la rama independiente `infra/rekognition-lifecycle` desde `main`;
  solo ejecutar `fmt`, `validate` y `plan` sin aplicar infraestructura.
- Preparar `scripts/test-media-flow.ps1` sin ejecutarlo hasta autorización
  explícita.

## Conciliación de inventario 2026-11-24

- Rama documental: `docs/reconcile-status`, creada desde `origin/main`
  `e391223` (PR #56 ya fusionado en la referencia remota).
- El propietario confirmó que API-003 (logout), API-005 (eliminación de cuenta)
  y API-039 (listado admin) están fusionadas y desplegadas (PR #39, fix de
  Supabase y PR #46). La matriz corregida separa esa declaración de la
  evidencia reproducible disponible: API-003 y API-005 no tienen en este
  checkout SHA/run y sonda productiva individual; API-039 tiene sonda `401`
  productiva del 2026-11-08, pero falta `200` autenticado y cierre de AC-005.
- La matriz de cumplimiento del documento del proyecto queda en
  `PROJECT_STATUS.md` §0: FCM es la prioridad; logout, eliminación, CloudWatch,
  caché, rate limiting, versionamiento, script de BD e IaC tienen estado
  separado por local/producción/spec. Compresión de respuestas es un faltante
  explícito; paginación está cumplida en los contratos implementados.
- La implementación FCM y su paquete están fusionados en `main` por PR #58.
  API-035/036 permanecen fuera de alcance para esta entrega.
- Reviews, Favorites y Chat no son requisitos prioritarios del documento del
  proyecto; quedan después de la entrega salvo aprobación explícita. No se
  inicia trabajo de esas áreas por esta conciliación.

## Conciliación de producción 2026-11-25

- `origin/main` contiene el merge de PR #58 (`8d48ae9`); CD #241 lo dejó
  desplegado en la EC2.
- `GET /health` respondió `200` con MongoDB y Redis saludables.
- El OpenAPI productivo expone las rutas de registro/baja de `device-tokens` y
  las rutas de Reviews API-030/API-031.
- Esta evidencia confirma despliegue, salud y exposición contractual. No cierra
  pruebas productivas de login, logout, pedidos, device tokens, FCM ni Reviews,
  y ninguna de esas áreas se marca como 100% funcional por este corte.

## Hardening FCM 2026-11-25

- Rama `fix/fcm-hardening`, creada desde `origin/main` `041ee96`; no se usaron
  credenciales, IAM/SCP, Terraform ni despliegues.
- Orders resuelve `OwnerUserId` mediante `ISellerOrderProfiles`; si falta,
  registra warning sin PII y el pedido permanece creado.
- Se conserva `order.status.changed` para Ready: es el evento general de
  transición. La emisión `order.ready` y la emisión al regenerar delivery-token
  fueron eliminadas. Las claves son `eventType:orderId:status`.
- El outbox usa reclamo atómico Mongo con worker/lease, recuperación de leases,
  estados `Pending`, `Processing`, `Sent`/`Failed`, backoff y restricción única
  de idempotencia.
- Un token FCM activo se desactiva para cualquier propietario anterior antes de
  registrarse para el usuario actual; la prueba usa Mongo local de Docker.
- Commits de la rama: `ee99de4`, `d883929`, `4df32d7`, `ea0f14f`, `64c637d`,
  `6c6650a`, `f139848`, `f8c5e08`, `ef4743f`, `981e823`, `00b3a7f`.
- Evidencia: unitarias `397/397`, seguridad `164/164`, integración `300/300`
  excluyendo el snapshot OpenAPI. El snapshot completo aún difiere por un
  contrato previo de `PATCH /api/v1/users/me` (`200`/`204`), no por FCM.

## Cierre operativo y smoke 2026-11-26

- `origin/main` contiene PR #69 (Chat API-037/API-038) y PR #70 (API-005).
  El estado de producción se refrenda como **Activo**: `GET /health` `200 OK`
  con MongoDB y Redis `Healthy`.
- El inventario de entrega integrado comprende Auth, Perfil/Vendedor,
  Productos/Catálogo, Media S3/Rekognition, Pedidos, Reviews API-030/031,
  Suspensión API-040, Eliminación resiliente API-005 y Chat API-037/API-038.
- `scripts/smoke-prod.ps1` quedó limitado a login, `me`, catálogo, health y GET
  no mutante de `device-tokens`. Solo imprime nombre, código HTTP y tiempo; sus
  credenciales se leen de variables de entorno y no se ejecuta sin autorización
  y cuentas de prueba.
- Auditoría de secretos: no hubo referencias a `JWT_SECRET` ni
  `SUPABASE_JWT_SECRET` en `infra/ssm/` o `scripts/`. Los placeholders dummy de
  DAST/fuzz fueron retirados de `.github/workflows/ci-1.yml`; no se imprimieron
  valores sensibles.
