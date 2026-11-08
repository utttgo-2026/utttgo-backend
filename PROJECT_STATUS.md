# Estado real del backend UTTT-GO

> Corte: 2026-11-08. El backend está **desplegado y operativo en Amazon AWS** (EC2 `3.129.142.54`, 2 réplicas Docker Compose detrás de Nginx, Redis contenedor, MongoDB Atlas y Supabase Auth configurados por variables de entorno en la instancia). Este documento separa lo **verificado externamente** (sondas HTTP contra la instancia y CD‑1 en verde) de lo **verificado con dobles** (suites locales). Un `200` con dobles no prueba una integración externa.

## 1. Stack, estructura y despliegue

| Elemento | Estado real |
|---|---|
| Runtime | .NET 10 con SDK **10.0.400 (estable, no preview)**, C# 14, ASP.NET Core 10, monolito modular en un único proyecto API. |
| Paquetes de la API | `FluentValidation 12.0.0`, `MongoDB.Driver 3.11.0`, `StackExchange.Redis 2.7.27`, `System.IdentityModel.Tokens.Jwt 8.3.1`, `Microsoft.AspNetCore.OpenApi 10.0.9`, `Microsoft.OpenApi 2.7.5`. **Sin SDK de AWS** (S3 presigned), sin cliente de Content Safety, sin FCM y sin SignalR. |
| Pruebas | `xUnit 2.9.3`, `FluentAssertions 6.12.2`, `Microsoft.AspNetCore.Mvc.Testing 10.0.9`. **Sin Testcontainers**: las suites usan dobles; no hay pruebas contra Atlas/Redis reales. |
| Nube | AWS `us-east-2`: 1 × EC2 t3.micro (Ubuntu 24.04) + EIP `3.129.142.54`, Security Group 22/80/443, S3 privado, IAM Instance Profile, CloudWatch Logs, alerta de Budget. Terraform **aplicado por el propietario**. |
| Contenedores | `docker-compose.yml`: `api` con `deploy.replicas: 2`, `redis:7-alpine` sin puertos publicados en red interna (`noeviction` + AOF), `nginx:alpine` con 80/443, `mongo:7` solo en perfil `local`. |
| Estado en la instancia | **En ejecución y verificado**: `GET /health` → `200` con MongoDB y Redis en `Healthy`; CD‑1 ejecutó un run **verde** el 2026-11-08 con evidencia `ci-evidence/cd-deploy.txt`. TLS/dominio sigue pendiente (solo HTTP). |
| CI/CD | `ci-1.yml` (gate en `pull_request` a `main` y `push` a `feature/**` y `main`: restore, NuGetAudit, formato, build Release, tres suites, Gitleaks bloqueante, Semgrep informativo) y `cd-deploy.yml` (CD‑1 por `workflow_run` con CI‑1 verde, despliegue por SSH del SHA exacto, `docker compose up -d --build --scale api=2`, espera de réplicas sanas, smoke y evidencia). |
| Configuración | `.env.example` documenta `REDIS_URL`, `MONGODB_URI`, `MONGODB_DATABASE`, `SUPABASE_URL`, `SUPABASE_ANON_KEY`, `SUPABASE_JWKS_URL`, `SUPABASE_JWT_SECRET`, `SUPABASE_JWT_ISSUER`, `SUPABASE_JWT_AUDIENCE`, `S3_BUCKET_IMAGES` y `AWS_REGION`. Los valores reales viven **solo** en el entorno de la instancia y en secretos locales; nunca en Git. `appsettings.json` contiene únicamente quotas de rate limiting y registro. |

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
├── scripts/{ci,dev}/         # auditoría NuGet, resumen de tests, siembra local de vendedor
├── skills/
├── specs/
│   ├── cross-cutting/        # api-health-probe, ci-cd-devsecops, infra-aws-free-tier
│   ├── phase-00-constitution/
│   ├── phase-02-auth-users/
│   ├── phase-03-catalog-offers/
│   └── phase-07-chat-realtime/
├── src/UTTTGO.Api/
│   ├── Modules/{Identity,Sellers,Admin}/
│   └── Shared/{Contracts,Health,Infrastructure}/
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
| POST | `/api/v1/sellers/activation-requests` | Bearer + cuenta `Active` | `201`/`200` (perfil de negocio con `sellerId`); `400`/`401`/`403`/`404`/`409`/`429`/`503` | **Sprint 1 de la fase 03**: autoactivación sin aprobación administrativa (DEC‑002); asigna el rol `Seller` en el perfil local. |
| GET | `/api/v1/sellers/me` | Bearer | `200` perfil de negocio propio; `401`/`404`/`429`/`503` | Ficha nueva aprobada (DEC‑019.d). No expone `email`, `phone` ni `version`. |
| PATCH | `/api/v1/sellers/me` | Bearer | `200`; `400`/`401`/`404`/`409`/`429`/`503` | Edición del propio perfil con control de concurrencia por `version`. |
| GET | `/openapi/v1.json` | Solo Development | `200` documento OpenAPI | Sin servicios externos. No es ficha funcional. |

No existen todavía: productos, categorías, media/imágenes, pedidos, reseñas, favoritos, notificaciones push, chat, recuperación de contraseña, logout y refresh token.

## 3. Servicios externos y configuración

| Servicio | Cliente en código | Variables leídas | Estado |
|---|---|---|---|
| MongoDB Atlas | `MongoClient`/`MongoStore` (`Shared/Infrastructure/MongoOptions.cs`), colecciones `users` y `sellers` | `MONGODB_URI`, `MONGODB_DATABASE` | **Real y operativo**; índices de `users` y `sellers` al arrancar; un fallo de índice no impide el arranque. |
| Redis | `IConnectionMultiplexer` + `RedisRateLimiter` + `RedisJwtGate` | `REDIS_URL` | **Real y operativo**: rate limiting distribuido, blacklist JWT (`revoked:{jti}`) y healthcheck. |
| Supabase Auth | `SupabaseAuthClient` (HTTP a `/auth/v1/signup`, `/token`, `/user`) | `SUPABASE_URL`, `SUPABASE_ANON_KEY`, `SUPABASE_JWKS_URL`, `SUPABASE_JWT_SECRET`, `SUPABASE_JWT_ISSUER`, `SUPABASE_JWT_AUDIENCE` | **Real y operativo**; el JWT se valida por JWKS o HMAC. |
| Amazon S3 | **No existe cliente** | `S3_BUCKET_IMAGES`, `AWS_REGION` (declarados) | Bucket privado aprovisionado; el flujo presigned + moderación pertenece a la fase 05. |
| Azure AI Content Safety | **No existe cliente** | — | Pendiente (fase 05, Media). |
| Firebase Cloud Messaging | **No existe cliente** | — | Pendiente (fase de notificaciones). |
| SignalR | **No implementado** | — | Pendiente (fase de chat); con 2 réplicas requiere backplane Redis. |

## 4. Verificación: sondas externas y suites locales

### 4.1 Sondas contra la instancia (2026-11-08, desde Internet)

| Sonda | Resultado |
|---|---|
| `GET /health` | `200` `{"status":"Healthy","checks":{"mongodb":"Healthy","redis":"Healthy"}}` |
| `GET /api/v1/auth/me` | `401` `UNAUTHORIZED` (contrato de error único con `correlationId`) |
| `GET /api/v1/users/me` | `401` `UNAUTHORIZED` |
| `GET /api/v1/sellers/000000000000000000000000` | `404` `SELLER_NOT_FOUND` → **Atlas responde** (antes `503`) |
| `GET /api/v1/admin/users?page=1&pageSize=20` | `401` `UNAUTHORIZED` |
| CD‑1 (push a `main`) | Run **verde**: clonado en la EC2, despliegue del SHA, 2 réplicas sanas, smoke y evidencia con la sonda `/health` |

Estas sondas prueban **disponibilidad y enrutamiento**, no el flujo autenticado completo: la validación con una cuenta real `@uttt.edu.mx` confirmada en Supabase queda como evidencia del Sprint 1 (TASK‑SELLER‑041).

### 4.2 Suites locales

`dotnet build -c Release` sin errores · `dotnet format --verify-no-changes` sin diferencias · las tres suites en verde. **Los tests usan dobles**: no prueban Atlas, Redis ni Supabase reales; para eso hacen falta contenedores de prueba (Testcontainers) o un entorno no productivo. El histórico por endpoint está en `docs/qa-api-*.md`.

## 5. Documentación SDD presente

| Archivo(s) | Contenido y estado |
|---|---|
| `docs/constitution.md` | **v2.5**: nube AWS (DEC‑018) y DEC‑019 (retirada de API‑016, identidad del vendedor, ficha `GET /api/v1/sellers/me`, alcance del Sprint 1). |
| `AGENTS.md` | Manual operativo SDD v2.3, alineado con la constitución y con la sonda `/health`. |
| `memoria.md` | Registro vivo de decisiones, dependencias, mapa de colecciones y estado de fases. |
| `docs/traceability.md` | Matriz de las 42 fichas + fichas nuevas, con el estado real de cada ruta y la evidencia de despliegue. |
| `docs/academic-requirements-phase-02.md`, `docs/decisiones-pendientes-fase-02.md` | Matriz académica y decisiones técnicas pendientes de la fase 02 (OTP, refresh, SemVer). |
| `docs/qa-api-*.md` | Evidencia de QA local con dobles de API‑001/002/004/006/007/008/039. |
| `specs/phase-02-auth-users/*` | Identidad, usuarios y vendedores: contratos y tareas con gates documentales abiertos (TASK‑AUTH‑002/003/007/008). |
| `specs/phase-03-catalog-offers/*` | **v2.1 aprobada para el Sprint 1 (vendedor)** por DEC‑019; el Sprint 2 (catálogo y categorías) sigue bloqueado hasta su propia aprobación. |
| `specs/cross-cutting/api-health-probe/*` | v1.1: ficha de `GET /health` (HP‑01..HP‑11) con el fix del registro de la sonda en CD‑1. |
| `specs/cross-cutting/ci-cd-devsecops/*` | Requisitos, secuencia y tareas de CI‑1/CD‑1 con la evidencia del run verde. |
| `specs/cross-cutting/infra-aws-free-tier/*` | INFRA‑01..15: IaC aplicada, stack en ejecución; pendientes hop limit de IMDS, alerta de Budget y backend remoto de estado. |
| `specs/phase-07-chat-realtime/spec.md` | Borrador funcional del chat temporal (DEC‑005); sin persistencia ni purgas. |

## 6. Faltantes o configuraciones a revisar

- **TLS y dominio:** la instancia solo sirve HTTP en la IP. `certbot` requiere un dominio real, y las tiendas móviles exigen HTTPS. Decisión abierta: dominio en un registrador con DNS gratuito y desafío DNS‑01 contra la EIP (evita Route 53, que sí tendría costo).
- **Media e imágenes (fase 05):** no hay `AWSSDK.S3` ni cliente de Content Safety; `avatarUrl` es siempre `null` y no se puede publicar ninguna imagen (BR‑007).
- **Catálogo (Sprint 2 de la fase 03):** no existen `products` ni `categories`; API‑011..015 y API‑019..022 necesitan `spec/plan/task` v2.2 y aprobación propia antes de código.
- **Sesiones:** no hay `POST /api/v1/auth/logout` (API‑003) ni refresh token; el access token expira (3600 s) y la app móvil debe volver a autenticarse. TASK‑AUTH‑003 abierto.
- **OTP y correo:** el backend no envía ni verifica OTP; el móvil usa operaciones Supabase (`verifyOtp`/`resend`). SMTP institucional y costo cero siguen pendientes (TASK‑AUTH‑002).
- **Middleware de autenticación y CORS:** no hay `AddAuthentication()`/`UseAuthentication()`; la validación del JWT se hace por servicio con gate de Redis. No hay política CORS (irrelevante para móvil nativo; necesaria si hay cliente web).
- **Auditoría administrativa:** `UnconfiguredAdminUsersAudit` no persiste eventos (AC‑005 abierto por API‑040).
- **Pruebas contra dependencias reales:** no hay Testcontainers ni entorno no productivo; las suites prueban lógica con dobles.
- **Terraform y costo:** falta un `apply` del hop limit de IMDS, la alerta de Budget y el backend remoto del estado; la evidencia de costo $0 depende de la alerta tras el primer apply.
- **Secretos:** las credenciales de Atlas y Supabase fueron rotadas tras exponerse en una conversación; los valores viven solo en el entorno de la instancia. No documentarlos ni versionarlos.
- **API‑016:** resuelta por DEC‑019 (no se implementa ni redirige). La eliminación de producto sigue **sin ficha aprobada**.
- **`AllowedHosts: "*"`** es control de hosts, **no** una política CORS.

### Cinco bloqueos principales (ordenados por impacto)

1. **HTTPS con dominio:** sin TLS la app móvil no puede publicarse en las tiendas ni cumplir las políticas de las plataformas.
2. **Catálogo y Media ausentes:** no hay productos, categorías ni imágenes; el valor funcional para el comprador todavía no existe.
3. **Ciclo de sesión incompleto:** sin refresh ni logout, la experiencia móvil se corta cada hora y no existe cierre de sesión en servidor.
4. **Cierre de infraestructura y costo:** hop limit de IMDS, alerta de Budget, backend remoto de estado y evidencia de costo $0.
5. **Gates SDD y pruebas reales:** cierre documental de fase 02 (TASK‑AUTH‑007/008), aprobación del Sprint 2 y pruebas contra Atlas/Redis reales en un entorno no productivo.
