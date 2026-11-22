# Estado real del backend UTTT-GO

> Corte: 2026-11-16. El backend está **desplegado y operativo en Amazon AWS** (EC2 `3.129.142.54`, 2 réplicas Docker Compose detrás de Nginx, Redis contenedor, MongoDB Atlas y Supabase Auth configurados por variables de entorno en la instancia). El **cierre de infraestructura** (DEC‑020/021) está aplicado y verificado en vivo: puerto 22 cerrado, despliegue CD‑1 por AWS Systems Manager, Budget con alerta institucional, CORS de S3 estricto y logs de contenedores en CloudWatch Logs. Este documento separa lo **verificado externamente** (sondas HTTP contra la instancia, despliegue por SSM y logs publicados) de lo **verificado con dobles** (suites locales) y de lo **verificado contra dependencias reales** (la suite E2E con MongoDB 7 y Redis 7 en Testcontainers). Un `200` con dobles no prueba una integración externa.

## 1. Stack, estructura y despliegue

| Elemento | Estado real |
|---|---|
| Runtime | .NET 10 con SDK **10.0.400 (estable, no preview)**, C# 14, ASP.NET Core 10, monolito modular en un único proyecto API. |
| Paquetes de la API | `FluentValidation 12.0.0`, `MongoDB.Driver 3.11.0`, `StackExchange.Redis 2.7.27`, `System.IdentityModel.Tokens.Jwt 8.3.1`, `Microsoft.AspNetCore.OpenApi 10.0.9`, `Microsoft.OpenApi 2.7.5`. **Sin SDK de AWS** (S3 presigned), sin cliente de Content Safety, sin FCM y sin SignalR. |
| Pruebas | `xUnit 2.9.3`, `FluentAssertions 6.12.2`, `Microsoft.AspNetCore.Mvc.Testing 10.0.9`, `Testcontainers 4.16.0` (`Testcontainers.MongoDb`/`Testcontainers.Redis`). La suite E2E (`tests/UtttGo.IntegrationTests/EndToEnd`) levanta **MongoDB 7 y Redis 7 reales** en contenedores y dobla solo las fronteras externas de autenticación (Supabase/JWT/blacklist). |
| Nube | AWS `us-east-2`: 1 × EC2 t3.micro (Ubuntu 24.04) + EIP `3.129.142.54`, Security Group **solo 80/443** (22 cerrado), S3 privado con CORS estricto (sin reglas por defecto), IAM Instance Profile, CloudWatch Logs, Systems Manager, Budget con alerta institucional. Terraform **aplicado por el propietario** (DEC‑018/020/021). |
| Contenedores | `docker-compose.yml`: `api` con `deploy.replicas: 2`, `redis:7-alpine` sin puertos publicados en red interna (`noeviction` + AOF), `nginx:alpine` con 80/443, `mongo:7` solo en perfil `local`. `docker-compose.prod.yml` (solo EC2) añade el driver `awslogs` para publicar los logs en `/utttgo/api/dev`. |
| Estado en la instancia | **Caído desde el 2026-11-18**: el run de CD 44255395753 se canceló por `timeout-minutes` durante el build y la sonda pública (`https://3.129.142.54.nip.io/health` y `http://3.129.142.54/health`) devuelve **000** (conexión fallida). Último estado verificado en verde: CD‑1 del 2026-11-08 y despliegue por SSM del 2026-11-11 (SHA `355db03`, 2/2 réplicas sanas, `/health` 200). Recuperación prevista con **CD‑2 (DEC-022)**: imagen publicada en GHCR y despliegue sin compilar. |
| CI/CD | `ci-1.yml` (gate en `pull_request` a `main` y `push` a `feature/**` y `main`: restore, NuGetAudit, formato, build Release, tres suites, Gitleaks bloqueante, Semgrep informativo) y `cd-deploy.yml` (**CD‑2/DEC‑022**: job `build_image` que publica `ghcr.io/utttgo-2026/utttgo-api:<sha>` y `:latest` con `packages: write` acotado, y job `deploy` que **no compila en la instancia**: envía `infra/ssm/deploy.sh` por `aws ssm send-command` (`AWS-RunShellScript`, `--timeout-seconds 1500`) y ejecuta `flock` + swap de 2 GB + `docker compose pull api` + `up -d --no-build --scale api=2` + `docker image prune`, espera de réplicas sanas, smoke de **`GET /health`**, SHA real (`git rev-parse HEAD`) y evidencia con los eventos de CloudWatch Logs). `ops-diagnose.yml` (`workflow_dispatch`) recoge el estado de la instancia **sin modificarla**. |
| Logs | La salida de `api`, `nginx` y `redis` llega a CloudWatch Logs (`/utttgo/api/dev`, streams `api`/`nginx`/`redis`) por el driver `awslogs` de Docker con el Instance Profile (DEC‑021). En la EC2 `docker compose logs` **no** está disponible: `aws logs tail /utttgo/api/dev`. |
| Configuración | `.env.example` documenta `REDIS_URL`, `MONGODB_URI`, `MONGODB_DATABASE`, `SUPABASE_URL`, `SUPABASE_ANON_KEY`, `SUPABASE_JWKS_URL`, `SUPABASE_JWT_SECRET`, `SUPABASE_JWT_ISSUER`, `SUPABASE_JWT_AUDIENCE`, `S3_BUCKET_IMAGES`, `AWS_REGION` y los opcionales `TLS_DOMAIN`/`TLS_EMAIL` (si faltan, el TLS usa `3.129.142.54.nip.io`). Los valores reales viven **solo** en el entorno de la instancia y en secretos locales; nunca en Git. `appsettings.json` contiene únicamente quotas de rate limiting y registro. |

Estructura del árbol (sin `.git`, `.vs`, `bin`, `obj` ni `node_modules`):

```text
utttgo-backend/
├── .github/workflows/        # ci-1.yml, cd-deploy.yml
├── certbot/www/
├── ci-evidence/
├── docs/                     # constitution, traceability, academic-requirements, qa-*
├── infra/terraform/          # IaC AWS (aplicada)
├── material/                 # fuentes PDF/DOCX/XLSX (no son specs)
├── nginx/conf.d/
├── scripts/{ci,dev,tls}/     # auditoría NuGet, resumen de tests, siembra local de vendedor, aprovisionamiento TLS
├── skills/
├── specs/
│   ├── cross-cutting/        # api-health-probe, ci-cd-devsecops, infra-aws-free-tier
│   ├── phase-00-constitution/
│   ├── phase-02-auth-users/
│   ├── phase-03-catalog-offers/
│   └── phase-07-chat-realtime/
├── src/UTTTGO.Api/
│   ├── Modules/{Identity,Sellers,Admin}/
│   └── Shared/{Contracts,Health,Infrastructure,OpenApi}/
├── tests/{UtttGo.UnitTests,UtttGo.IntegrationTests,UtttGo.SecurityTests}/
├── AGENTS.md, Dockerfile, docker-compose.yml, memoria.md, nginx/, setup.sh
└── UTTTGO.Backend.slnx
```

## 2. Endpoints realmente presentes

Las rutas se derivan de los atributos de los controladores y de `Program.cs`. La ruta de plantilla `/WeatherForecast` **ya fue eliminada**: no existe el directorio `Controllers/` de plantilla. Los errores usan el contrato único `{ correlationId, status, code, message }`.

| Método | Ruta | Acceso | Salida | Servicios reales (AWS) |
|---|---|---|---|---|
| POST | `/api/v1/auth/register` | Público | `202` genérico; `400`/`413`/`422`/`429`/`503` | Supabase signup + perfil local en Atlas + rate limit en Redis. |
| POST | `/api/v1/auth/login` | Público | `200` (`accessToken`, `expiresIn`, `tokenType`, `user`); `400`/`401`/`429`/`503` | Supabase password grant + confirmación + perfil en Atlas + blacklist/rate limit en Redis. |
| GET | `/api/v1/auth/me` | Bearer | `200` (`id`, `email`, `displayName`, `roles`, `accountStatus`); `401`/`404`/`503` | JWT validado (firma/issuer/audience/expiración) + `revoked:{jti}` en Redis + perfil en Atlas. |
| GET | `/api/v1/users/me` | Bearer | `200` (`id`, `displayName`, `email`, `phone`, `avatarUrl`, `roles`); `401`/`503` | Sesión + Atlas. `avatarUrl` siempre `null` (Media no existe). |
| PATCH | `/api/v1/users/me` | Bearer | `200` perfil propio; `400`/`401`/`409`/`503` | Sesión + CAS por `version` en Atlas. Solo `displayName`/`phone`. |
| GET | `/api/v1/sellers/{sellerId}` | Público | `200` (`sellerId`, `displayName`, `ratingAverage`, `ratingCount`, `isActive`); `400`/`404`/`429`/`503` | Atlas (`sellers`) + rate limit en Redis. Ratings `null` mientras Reviews no exista. |
| GET | `/api/v1/admin/users` | Bearer + rol `Admin` | `200` (`items`, `page`, `pageSize`, `total`); `400`/`401`/`403`/`429`/`503` | Sesión + listado paginado en Atlas. Roles del perfil local, nunca del JWT. |
| GET | `/health` | Público | `200` `{"status":"Healthy","checks":{...}}`; `429`/`503` | Pings acotados a Atlas y Redis; falla cerrada sin revelar la dependencia. Sonda operativa, no ficha funcional. |
| POST | `/api/v1/sellers/activation-requests` | Bearer + cuenta `Active` | `201`/`200` (perfil de negocio con `sellerId`, `storeName`, `description`, `phone`, `status: "Active"`); `400`/`401`/`403`/`404`/`409`/`429`/`503` | **Sprint 1 de la fase 03**: autoactivación sin aprobación administrativa (DEC‑002); asigna el rol `Seller` en el perfil local. **Alineado con el móvil (2026-11-16)**: el cuerpo acepta solo `storeName`, `description` y `phone`. |
| GET | `/api/v1/sellers/me` | Bearer | `200` perfil de negocio propio (`storeName`, `description`, `phone`, `status`, `isActive`, `roles`); `401`/`404` `SELLER_PROFILE_NOT_FOUND`/`429`/`503` | Ficha nueva aprobada (DEC‑019.d). No expone `email`, `supabaseUserId` ni `version` (el `phone` es el contacto del negocio). |
| PATCH | `/api/v1/sellers/me` | Bearer | `200`; `400`/`401`/`404`/`409`/`429`/`503` | Edición del propio perfil con control de concurrencia por `version`; acepta solo `storeName`, `description` y `phone`. |
| GET | `/openapi/v1.json` | Público | `200` documento OpenAPI (todos los entornos, antes solo Development) | Sin servicios externos. No es ficha funcional. |
| GET | `/openapi` | Público | `200` HTML con Swagger UI de solo lectura (`tryItOut` deshabilitado) | CDN de Swagger UI; documentación para el equipo móvil (`docs/mobile-integration.md`). |
| GET | `/api/v1/categories` | Público | `200` (`items`, `page`, `pageSize`, `totalCount`); `400`/`429`/`503` | Atlas (`categories`, dueño Catalog) + rate limit en Redis. **Sembradas en la instancia el 2026-11-16** (`CATALOG_SEED_ENABLED=true`, upsert idempotente por `normalizedName`): `Comida Preparada`, `Bebidas`, `Snacks`. |
| GET | `/api/v1/products` | Público | `200` (`items`, `page`, `pageSize`, `totalCount`); `400`/`429`/`503` | Atlas (`products`, dueño Catalog) + filtros `search`/`categoryId`/`minPrice`/`maxPrice`/`rating` + rate limit en Redis. |
| GET | `/api/v1/products/{productId}` | Público | `200`; `400`/`404`/`429`/`503` | Atlas (`products`); solo productos del vendedor en estado `Active` con categoría activa. |
| POST | `/api/v1/products` | Bearer + rol `Seller` | `201`; `400`/`401`/`403`/`404`/`409`/`422` `IMAGE_NOT_AVAILABLE`/`429`/`503` | Atlas (`products` + `categories`) + rate limit en Redis. El precio y el stock los fija el vendedor; el cliente no puede elegir vendedor ni estado. `imageUrl` es opcional y **no se persiste** (fase 05: Media). |
| PATCH | `/api/v1/products/{productId}` | Bearer + dueño | `200`; `400`/`401`/`403`/`404`/`409`/`429`/`503` | Atlas (`products`) con CAS por `version`; solo su propio producto. |
| PATCH | `/api/v1/products/{productId}/availability` | Bearer + dueño | `200`; `400`/`401`/`403`/`404`/`409`/`429`/`503` | Atlas (`products`) con CAS por `version`; pausa/reanudación del producto. El cuerpo usa `status` (`Active`/`Paused`). |
| POST | `/api/v1/orders` | Bearer + cuenta `Active` | `201`; `200` (replay de `Idempotency-Key`); `400` `VALIDATION_ERROR`/`INSUFFICIENT_STOCK`; `401`/`403` `ACCOUNT_NOT_ACTIVE`/`404` `PRODUCT_NOT_FOUND`/`422` `PRODUCT_NOT_AVAILABLE`/`429`/`503` `ORDER_UNAVAILABLE` | **Fase 1 (API-023)**: Atlas (`orders`, dueño Orders) + reserva atómica de stock en `products` vía el contrato publicado por Catalog + `pickupPoint` de `sellers` + rate limit en Redis. Total server-side (`unitPrice` × `quantity`). Acepta `notes` opcional (≤250 caracteres). |
| GET | `/api/v1/orders/mine` | Bearer | `200` (`items`, `page`, `pageSize`, `totalCount`); `400`/`401`/`429`/`503` | **Fase 1 (API-024)**: Atlas (`orders`) filtrado por el comprador autenticado; filtro opcional `status`. Cada ítem publica `sellerId`, `buyerId`, `productName`, `unitPrice`, `quantity` y `totalAmount`. |
| GET | `/api/v1/orders/{orderId}` | Bearer + participante | `200`; `401`/`404` `ORDER_NOT_FOUND`/`429`/`503` | **Fase 1 (API-025)**: Atlas (`orders`); solo el comprador o el vendedor del pedido (un tercero recibe `404`). Publica `buyerId`, `sellerId`, `productName`, `unitPrice`, `quantity`, `totalAmount`, `status`, `notes` y `createdAt`. |
| POST | `/api/v1/orders/{orderId}/cancel` | Bearer + comprador | `200`; `401`/`403` `ORDER_FORBIDDEN`/`404` `ORDER_NOT_FOUND`/`409` `ORDER_STATE_CONFLICT`/`429`/`503` `ORDER_UNAVAILABLE` | **Fase 2 (API-026)**: Atlas (`orders` + `orderAuditLogs`) con CAS; solo el comprador y solo desde `Pending`; libera la reserva de stock de forma idempotente, fija `completedAt` y es idempotente al repetirse. |
| PATCH | `/api/v1/orders/{orderId}/status` | Bearer + vendedor | `200`; `400` `VALIDATION_ERROR`/`401`/`403` `ORDER_FORBIDDEN`/`404` `ORDER_NOT_FOUND`/`409` `ORDER_STATE_CONFLICT`/`ORDER_VERSION_CONFLICT`/`429`/`503` | **Fase 2 (API-027)**: Atlas (`orders`) con CAS por `version`; solo el vendedor del pedido; `Pending→Accepted`, `Pending→Rejected`, `Accepted→InPreparation`, `InPreparation→Ready`. `Delivered` y `CancelledByBuyer` se rechazan. |
| POST | `/api/v1/orders/{orderId}/delivery-token` | Bearer + comprador | `201`; `401`/`403` `ORDER_FORBIDDEN`/`404` `ORDER_NOT_FOUND`/`409` `ORDER_STATE_CONFLICT`/`429`/`503` | **Fase 2 (API-028)**: solo el comprador y solo con el pedido en `Ready`; devuelve `{orderId, token, expiresAt}`; en Atlas solo se guarda el hash SHA-256 del token con su expiración (TTL 900 s). El QR lo genera el móvil. |
| POST | `/api/v1/orders/{orderId}/delivery-confirmations` | Bearer + vendedor | `200`; `401`/`403` `ORDER_FORBIDDEN`/`404` `ORDER_NOT_FOUND`/`409` `ORDER_STATE_CONFLICT`/`ORDER_VERSION_CONFLICT`/`DELIVERY_TOKEN_ALREADY_USED`/`422` `DELIVERY_TOKEN_INVALID`/`DELIVERY_TOKEN_EXPIRED`/`429`/`503` | **Fase 2 (API-029)**: `Ready→Delivered` con CAS (estado, versión y hash del token); fija `deliveredAt`/`completedAt`, marca el token como usado y confirma la reserva de stock. El token es de un solo uso. |
| GET | `/api/v1/sellers/me/orders` | Bearer + rol `Seller` + cuenta `Active` | `200` (`items`, `page`, `pageSize`, `totalCount`); `400`/`401`/`403` `SELLER_ROLE_REQUIRED`/`SELLER_ACCOUNT_NOT_ACTIVE`/`429`/`503` | **Fase 4, ficha nueva aprobada (D-ORD-05, 2026-11-18)**: Atlas (`orders`, dueño Orders) filtrado por el `sellerId` del perfil autenticado; cada ítem publica `role: "seller"` y los campos de `OrderSummaryResponse`; filtro opcional `status`; rate limit `order-read` fail-closed. `/orders/mine` no cambia. |

No existen todavía: media/imágenes, reseñas, favoritos, notificaciones push, chat, recuperación de contraseña, logout y refresh token. La Fase 2 de pedidos (API‑026..029) está **fusionada en `main`** (PR #25, merge `cac164f`) pero **aún no desplegada**: el CI‑1 sobre el merge commit (run `43979116661`) falló por un flake del contenedor de MongoDB en la suite E2E (`exit code 48`) y el CD‑1 quedó *skipped*; el arreglo (contenedor MongoDB sin fase de inicialización, espera explícita de disponibilidad y reintentos) está en el **PR #26 con CI‑1 en verde** (run `43986405691`), pendiente de fusión. La notificación push y el hecho de Reviews quedan diferidos (D‑ORD‑15).

## 3. Servicios externos y configuración

| Servicio | Cliente en código | Variables leídas | Estado |
|---|---|---|---|
| MongoDB Atlas | `MongoClient`/`MongoStore` (`Shared/Infrastructure/MongoOptions.cs`), colecciones `users` y `sellers` | `MONGODB_URI`, `MONGODB_DATABASE` | **Real y operativo**; índices de `users` y `sellers` al arrancar; un fallo de índice no impide el arranque. |
| Redis | `IConnectionMultiplexer` + `RedisRateLimiter` + `RedisJwtGate` | `REDIS_URL` | **Real y operativo**: rate limiting distribuido, blacklist JWT (`revoked:{jti}`) y healthcheck. |
| Supabase Auth | `SupabaseAuthClient` (HTTP a `/auth/v1/signup`, `/token`, `/user`) | `SUPABASE_URL`, `SUPABASE_ANON_KEY`, `SUPABASE_JWKS_URL`, `SUPABASE_JWT_SECRET`, `SUPABASE_JWT_ISSUER`, `SUPABASE_JWT_AUDIENCE` | **Real y operativo**; el JWT se valida por JWKS o HMAC. |
| Amazon S3 | **No existe cliente** | `S3_BUCKET_IMAGES`, `AWS_REGION` (declarados) | Bucket privado aprovisionado; el flujo presigned + moderación pertenece a la fase 05. |
| Amazon Rekognition | Cliente integrado en Media | `MODERATION_MIN_CONFIDENCE` | Migración en validación local (fase 05, Media). |
| Firebase Cloud Messaging | **No existe cliente** | — | Pendiente (fase de notificaciones). |
| SignalR | **No implementado** | — | Pendiente (fase de chat); con 2 réplicas requiere backplane Redis. |

## 4. Verificación: sondas externas y suites locales

### 4.1 Sondas contra la instancia (2026-11-08 y 2026-11-11, desde Internet)

| Sonda | Resultado |
|---|---|
| `GET /health` | `200` `{"status":"Healthy","checks":{"mongodb":"Healthy","redis":"Healthy"}}` |
| `GET /api/v1/auth/me` | `401` `UNAUTHORIZED` (contrato de error único con `correlationId`) |
| `GET /api/v1/users/me` | `401` `UNAUTHORIZED` |
| `GET /api/v1/sellers/000000000000000000000000` | `404` `SELLER_NOT_FOUND` → **Atlas responde** (antes `503`) |
| `GET /api/v1/admin/users?page=1&pageSize=20` | `401` `UNAUTHORIZED` |
| CD‑1 (push a `main`, 2026-11-08) | Run **verde**: clonado en la EC2, despliegue del SHA, 2 réplicas sanas, smoke y evidencia con la sonda `/health` |
| Despliegue por SSM (2026-11-11, SHA `355db03`) | **Verde**: `aws ssm send-command` → documento `utttgo-dev-deploy` → fetch del SHA exacto → `docker compose` con el override de producción → **2/2 réplicas sanas**, `GET /api/v1/auth/me` → `401`, `GET /health` → `200` |
| Security Group (2026-11-11) | Ingress real: **solo 80 y 443** (`0.0.0.0/0`); el 22 está cerrado y el acceso se hace por SSM |
| CloudWatch Logs (2026-11-11) | Streams `api`, `nginx` y `redis` recibiendo eventos del stack en producción (`aws logs tail /utttgo/api/dev`) |
| Budget (2026-11-11) | `utttgo-dev-zero-cost-guard`: COST, USD, MONTHLY, límite 1 USD, **notificación `ACTUAL`/`GREATER_THAN`/umbral 0 % en estado `OK`**; `ActualSpend` 0.00 USD y Cost Explorer del mes en 0.00 USD |

Estas sondas prueban **disponibilidad, enrutamiento y operación de la infraestructura**, no el flujo autenticado completo: la validación con una cuenta real `@uttt.edu.mx` confirmada en Supabase queda como evidencia del Sprint 1 (TASK‑SELLER‑041).

### 4.2 Suites locales

`dotnet build -c Release` sin errores (0 advertencias) · `dotnet format --verify-no-changes` sin diferencias · **289 unitarias + 231 de integración + 128 de seguridad en verde** (648 pruebas; el Sprint 2 del catálogo y la Fase 1 de pedidos añadieron 183). **Las suites de endpoint usan dobles**; la suite E2E (`tests/UtttGo.IntegrationTests/EndToEnd`) sí usa **MongoDB 7 y Redis 7 reales en Testcontainers** y dobla solo la autenticación externa. El histórico por endpoint está en `docs/qa-api-*.md`.

**Fase 2 de pedidos (2026-11-16, rama `feature/orders-phase2-status-qr`)**: `dotnet build -c Release` sin errores ni advertencias · `dotnet format --verify-no-changes` sin diferencias · **331 unitarias + 260 de integración + 138 de seguridad en verde** (729 pruebas; la Fase 2 añadió 81).

**Alineación del contrato con el equipo móvil (2026-11-16)**: `dotnet build -c Release` sin errores ni advertencias · `dotnet format --verify-no-changes` sin diferencias · **331 unitarias + 268 de integración + 140 de seguridad en verde** (739 pruebas).

Además, contra **MongoDB 7 real** (contenedor local) se validó el diseño del Sprint 1: el índice único **parcial** `ownerUserId_unique` convive con documentos heredados sin `ownerUserId`, rechaza el duplicado con error `11000` y la activación por `version` se aplica de forma condicional. Smoke con el stack local completo (2 réplicas + Nginx + Redis + Mongo): `/health` → `200` con `mongodb` y `redis` en `Healthy`; las tres rutas de vendedor responden `401` sin token válido.

Evidencia de la Fase 1 de pedidos (2026-11-16, rama `feature/orders-phase1-p1`): compra completa con Mongo real (alta de vendedor, producto, compra, reserva de stock, `/orders/mine`, detalle del comprador y del vendedor, `404` para un tercero y auditoría `Created`), **carrera de `Idempotency-Key`** (dos compras simultáneas con la misma clave → un `201`, un `200`, un solo pedido y el stock reservado una vez) y **carrera de stock** (dos compradores simultáneos sobre una unidad → un `201` y un `400 INSUFFICIENT_STOCK`, stock final `0`, sin sobreventa).

Evidencia de la Fase 2 de pedidos (2026-11-16, rama `feature/orders-phase2-status-qr`): ciclo completo con Mongo y Redis reales `Pending → Accepted → InPreparation → Ready → delivery-token → delivery-confirmations` (`Delivered` con `deliveredAt`, `completedAt`, `version = 5`, hash SHA-256 del token en el documento y stock comprometido), cancelación con devolución del stock y **carrera de `version`** (dos `PATCH /status` simultáneos con la misma versión → un `200` y un `409 ORDER_VERSION_CONFLICT`).

## 5. Documentación SDD presente

| Archivo(s) | Contenido y estado |
|---|---|
| `docs/constitution.md` | **v2.6**: nube AWS (DEC‑018), DEC‑019 (retirada de API‑016, identidad del vendedor, ficha `GET /api/v1/sellers/me`, alcance del Sprint 1) y DEC‑020/021 (cierre de exposición de la EC2, CD por SSM, Budget con alerta, CORS estricto y logs en CloudWatch Logs). |
| `AGENTS.md` | Manual operativo SDD **v2.6**, alineado con la constitución y con la sonda `/health`. |
| `memoria.md` | Registro vivo de decisiones, dependencias, mapa de colecciones y estado de fases. |
| `docs/traceability.md` | Matriz de las 42 fichas + fichas nuevas, con el estado real de cada ruta y la evidencia de despliegue. Incluye el bloque «Contrato publicado para el equipo móvil» (guía, OpenAPI con `servers` HTTPS y esquemas explícitos, seeding de categorías en la EC2). |
| `docs/academic-requirements-phase-02.md`, `docs/decisiones-pendientes-fase-02.md` | Matriz académica y decisiones técnicas pendientes de la fase 02 (OTP, refresh, SemVer). |
| `docs/qa-api-*.md` | Evidencia de QA local con dobles de API‑001/002/004/006/007/008/039. |
| `docs/mobile-integration.md` | Guía para el equipo móvil: base HTTPS (`3.129.142.54.nip.io`), OpenAPI/Swagger, JSON real de login y flujo de 3 pasos de verificación de correo antes del login. |
| `docs/guia-integracion-movil.md` | **Contrato único para el equipo móvil (API-005 actualizado 2026-11-22)**: JSON reales de autenticación, vendedores, catálogo (API-011..015/019) y pedidos (API-023..029), eliminación de cuenta `DELETE /api/v1/users/me`, contrato de error, rate limits por ruta, flujo mínimo de integración y límites conocidos (sin imágenes, sin DELETE de producto). |
| `specs/phase-02-auth-users/*` | Identidad, usuarios y vendedores: contratos y tareas con gates documentales abiertos (TASK‑AUTH‑002/003/007/008). |
| `specs/phase-03-catalog-offers/*` | **v2.1 aprobada para el Sprint 1 (vendedor)** por DEC‑019; el Sprint 2 (catálogo y categorías) está implementado (API‑011..015/019) y sus fichas atómicas siguen PENDIENTE de conciliación. |
| `specs/phase-04-orders-cash/*` | **v0.4**: Fase 1 (API‑023/024/025) y Fase 2 (API‑026/027/028/029) aprobadas por el propietario e implementadas el 2026-11-16; las decisiones D‑ORD‑15..19 registran el diferimiento de notificaciones/Reviews, el QR en el móvil, el alcance de `Rejected` y el tratamiento de los fallos de stock. |
| `specs/cross-cutting/api-health-probe/*` | v1.1: ficha de `GET /health` (HP‑01..HP‑11) con el fix del registro de la sonda en CD‑1. |
| `specs/cross-cutting/ci-cd-devsecops/*` | Requisitos, secuencia y tareas de CI‑1/CD‑1 con la evidencia del run verde y el nuevo camino de despliegue por SSM. |
| `specs/cross-cutting/infra-aws-free-tier/*` | INFRA‑01..15: IaC aplicada y cierre verificado (SG sin 22, Budget con alerta, CORS estricto, logs en CloudWatch Logs); pendientes el `description` del SG, el backend remoto de estado y el build de la imagen en CI. |
| `specs/phase-07-chat-realtime/spec.md` | Borrador funcional del chat temporal (DEC‑005); sin persistencia ni purgas. |

## 6. Faltantes o configuraciones a revisar

- **TLS y dominio:** implementado en código con el hostname gratuito `3.129.142.54.nip.io` (nip.io resuelve a la EIP sin registro DNS; Let's Encrypt por `webroot` con `scripts/tls/provision-tls.sh`, integrado en `setup.sh` y en CD-1, con renovación automática). La instancia **aún sirve solo HTTP**: falta fusionar el PR, ejecutar `terraform apply` (el script va incrustado en el documento SSM `utttgo-dev-deploy`) y desplegar para emitir el primer certificado. El aviso `Failed to determine the https port for redirect` persiste hasta entonces (ruido de `HttpsRedirectionMiddleware`, sin impacto funcional; el TLS termina en Nginx).
- **Media e imágenes (fase 05):** no hay `AWSSDK.S3` ni cliente de Content Safety; `avatarUrl` es siempre `null` y no se puede publicar ninguna imagen (BR‑007).
- **Catálogo (Sprint 2 de la fase 03):** `products` y `categories` **ya existen** (API‑011..015/019: búsqueda, filtros, publicación y disponibilidad del vendedor); sus fichas atómicas siguen PENDIENTE de conciliación y no hay filtro público `sellerId` ni “mis productos” (decisión registrada para la iteración siguiente).
- **Pedidos (Fases 1 y 2 de la fase 04):** API‑023/024/025 y API‑026/027/028/029 implementadas con `orders` y `orderAuditLogs`; el PR #25 está fusionado (`cac164f`) pero **falta el despliegue por CD‑1** (el CI‑1 del merge commit falló por el flake del contenedor de MongoDB en la E2E y el CD‑1 quedó *skipped*; el arreglo está en el **PR #26 con CI‑1 en verde**, pendiente de fusión). La notificación push (fase 08) y el hecho de Reviews (fase 06) siguen diferidos por D‑ORD‑15. No hay listado de pedidos del vendedor (D‑ORD‑05 sin aprobar).
- **Sesiones:** no hay `POST /api/v1/auth/logout` (API‑003) ni refresh token; el access token expira (3600 s) y la app móvil debe volver a autenticarse. TASK‑AUTH‑003 abierto.
- **OTP y correo:** el backend no envía ni verifica OTP; el móvil usa operaciones Supabase (`verifyOtp`/`resend`). SMTP institucional y costo cero siguen pendientes (TASK‑AUTH‑002).
- **Middleware de autenticación y CORS:** no hay `AddAuthentication()`/`UseAuthentication()`; la validación del JWT se hace por servicio con gate de Redis. No hay política CORS (irrelevante para móvil nativo; necesaria si hay cliente web).
- **Auditoría administrativa:** `UnconfiguredAdminUsersAudit` no persiste eventos (AC‑005 abierto por API‑040).
- **Pruebas contra dependencias reales:** la suite E2E ya usa **Testcontainers** (MongoDB 7 y Redis 7) para catálogo y pedidos; falta el entorno no productivo contra Atlas, Supabase real, S3/Content Safety y FCM.
- **Secretos de CI en AWS:** CD‑1 usa el usuario IAM `utttgo-dev-github-actions-deploy` (OIDC está bloqueado por el SCP de la organización). Falta que el propietario cree su clave (`iam:create-access-key`) y la guarde en los secretos del repositorio; hasta entonces CD‑1 automático fallará (el despliegue manual por `aws ssm send-command` sí funciona).
- **Tiempo de despliegue:** el build de la imagen ocurre en la t3.micro con los CPU credits agotados (balance ≈ 282/3000) y la publicación de .NET tardó **≈ 29 min**. Por eso el run de CD **44255395753** (2026-11-18) se canceló al cumplir `timeout-minutes` (75 min) con el build todavía en curso y dejó el stack sin responder (sonda `000`). **Resuelto por DEC-022**: la imagen se construye en GitHub Actions y se publica en GHCR; la instancia solo descarga y levanta. Pendiente: run de CD‑2 en verde con `GET /health` 200 y el `terraform apply` del propietario para re-sincronizar el documento SSM con el nuevo `deploy.sh`.
- **`description` del Security Group:** conserva el texto histórico porque es `ForceNew`; el estado real está en la etiqueta `Ingress`. Se corrige en el próximo reemplazo consciente de la instancia (junto con el rollover de AMI).
- **Terraform:** backend remoto del estado (S3 + DynamoDB) pendiente antes de producción; el estado sigue local y fuera de Git.
- **Secretos:** las credenciales de Atlas y Supabase fueron rotadas tras exponerse en una conversación; los valores viven solo en el entorno de la instancia. No documentarlos ni versionarlos.
- **API‑016:** resuelta por DEC‑019 (no se implementa ni redirige). La eliminación de producto sigue **sin ficha aprobada**.
- **`AllowedHosts: "*"`** es control de hosts, **no** una política CORS.

### Cinco bloqueos principales (ordenados por impacto)

1. **HTTPS con dominio:** el código TLS ya está listo (nip.io + Let's Encrypt, costo $0); falta el `terraform apply` del documento SSM y el despliegue que emite el certificado para que la app móvil cumpla las políticas de las tiendas.
2. **Catálogo y Media ausentes:** no hay productos, categorías ni imágenes; el valor funcional para el comprador todavía no existe.
3. **Ciclo de sesión incompleto:** sin refresh ni logout, la experiencia móvil se corta cada hora y no existe cierre de sesión en servidor.
4. **Secretos de CI en AWS:** sin la clave del usuario IAM de despliegue, el pipeline no puede publicar en la EC2 (el camino manual por SSM sí está validado).
5. **Gates SDD y pruebas reales:** cierre documental de fase 02 (TASK‑AUTH‑007/008), aprobación del Sprint 2 y pruebas contra Atlas/Redis reales en un entorno no productivo.
