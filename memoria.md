# memoria.md - Backend UTTT-GO

Registro vivo de decisiones, contexto y evidencia. No contiene secretos ni
credenciales.

## Decisiones vigentes

| ID | Decisión | Estado |
|---|---|---|
| DEC-001 | Compra directa individual; no existe carrito. | Resuelta |
| DEC-002 | El vendedor se autoactiva al completar su perfil de negocio. | Resuelta |
| DEC-003 | API-005: baja lógica `Deleted`, anonimización de perfil, `deletedUserRef` UUID aleatorio, bloqueo por pedidos activos, productos de vendedor no disponibles, revocación Redis y logout global de Supabase best-effort; no se usa service_role/Admin API. | Aprobada por el propietario 2026-11-22; evidencia de contenedores pendiente |
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
- Implementación FCM en `feature/fcm-push-v2`: `FirebaseAdmin 3.4.0`, tokens
  cifrados con Data Protection y hash SHA-256, endpoints de registro/baja,
  outbox Mongo y dispatcher con reintentos acotados. Orders publica después de
  persistir su cambio y no espera a FCM; fallos de push no revierten el pedido.
- La credencial se lee solo desde `FIREBASE_CREDENTIALS_JSON` o
  `FIREBASE_CREDENTIALS_PATH`; si falta, el sender registra un warning y queda
  desactivado. `docker-compose.yml` propaga ambas variables sin valores en Git.
  La integración de SSM/IAM permanece fuera de esta tarea.
- Evidencia local: build Release correcto con cuatro warnings `CS8034` del
  analizador AWS bloqueado por la política local; unitarias `391/391`, FCM
  unitarias `2/2`, integración FCM aislada `1/1` y seguridad FCM aislada `2/2`.
  Las suites completas de integración y seguridad no pudieron ejecutarse porque
  Docker Desktop no estaba disponible (`npipe://./pipe/docker_engine`).
- API-035/036 y Chat permanecen fuera de alcance; las preguntas abiertas están
  en `specs/phase-06-notifications-push/spec.md`.

- DEC-003/API-005 fue aprobada el 2026-11-22. Los contratos mínimos `IActiveOrderReader`
  e `IAccountDeletionCatalog` ya están publicados; Reviews y mensajes quedan para
  sus fichas dueñas. Falta ejecutar integración/seguridad con Mongo y Redis aislados
  cuando Docker esté disponible.

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
- La spec FCM está aprobada en la rama `feature/fcm-push`; el adaptador Firebase
  y su paquete local siguen bloqueados hasta disponer del Firebase Admin SDK.
  No se implementan API-035/036 en esta prioridad.
- Reviews, Favorites y Chat no son requisitos prioritarios del documento del
  proyecto; quedan después de la entrega salvo aprobación explícita. No se
  inicia trabajo de esas áreas por esta conciliación.
