# Estado real del backend UTTT-GO

> Corte: 2026-11-06. Inventario del código presente y prueba HTTP local de una instancia sin credenciales ni dependencias externas. Una ficha de API, un test con dobles y una ruta operativa **no** equivalen a una integración productiva. No se probaron servicios de producción.

> **Actualización MongoDB posterior:** se añadió `MongoDB.Driver` y `MongoOptions` (`MONGODB_URI`, `MONGODB_DATABASE` con default `utttgo`); `users` ahora tiene adaptadores Identity para registro, lectura, edición condicional y listado Admin, y `sellers` un lector público separado. Los índices de `users` se crean al arrancar con URI configurada, no por petición. La siembra local de Development en `scripts/dev/seed-seller.js` permitió verificar `GET /api/v1/sellers/{sellerId}`: `200` para vendedor de prueba y `404` para ID inexistente con MongoDB y Redis locales. **Siguen pendientes** Supabase, JWT de sesión, auditoría Admin real, resolución de `sellers` frente a `sellerProfiles` histórico, y validación de los adaptadores de usuario mediante flujo autenticado. Las secciones 1–6 inferiores son una fotografía previa, no el estado vigente de MongoDB.

## 1. Stack y estructura

El proyecto es **C# / ASP.NET Core (Controllers)** sobre **.NET 10** (`net10.0` en `src/UTTTGO.Api/UTTTGO.Api.csproj`), con arquitectura modular en un único proyecto API. No existen `package.json` ni `requirements.txt`: las dependencias se declaran en archivos `.csproj`. El proyecto API referencia `FluentValidation` **12.0.0**, `Microsoft.AspNetCore.OpenApi` **10.0.9** y `Microsoft.OpenApi` **2.7.5**; **no** referencia `MongoDB.Driver`, `StackExchange.Redis` ni un cliente Supabase. En esta máquina, `dotnet --info` mostró runtime ASP.NET Core/.NET **10.0.9** y únicamente el SDK **10.0.400-preview.0.26322.102** (no SDK estable instalado); no hay `global.json`. La constitución prescribe C# 14 y .NET 10 LTS, pero el proyecto no fija explícitamente `<LangVersion>`.

Árbol de carpetas, **máximo tres niveles**, sin `.git`, `.vs`, `bin`, `obj` ni `node_modules` (estos últimos no forman parte del proyecto):

```text
utttgo-backend/
├── .github/
│   └── workflows/
├── .semgrep/
├── certbot/
│   └── www/
├── ci-evidence/
├── docs/
├── infra/
│   └── terraform/
├── material/
├── nginx/
│   └── conf.d/
├── scripts/
│   └── ci/
├── skills/
├── specs/
│   ├── cross-cutting/
│   ├── phase-00-constitution/
│   ├── phase-02-auth-users/
│   ├── phase-03-catalog-offers/
│   └── phase-07-chat-realtime/
├── src/
│   └── UTTTGO.Api/
│       (Controllers/, Modules/ y Properties/ en el siguiente nivel)
├── tests/
│   ├── UtttGo.IntegrationTests/
│   ├── UtttGo.SecurityTests/
│   └── UtttGo.UnitTests/
├── .dockerignore
├── .env.example
├── AGENTS.md
├── Dockerfile
├── docker-compose.yml
├── memoria.md
├── README.md
├── setup.sh
└── UTTTGO.Backend.slnx
```

## 2. Endpoints realmente presentes

Las rutas se derivan de atributos de controladores y de `Program.cs`, **no** del inventario futuro de 42 fichas. Los archivos de handlers de la tabla son relativos a `src/UTTTGO.Api/`. «Previsto» indica contratos de servicio que requerirían esas dependencias; **en la aplicación ejecutada no se creó ninguna conexión real a MongoDB, Redis o Supabase**. Los errores de los controladores incluyen `correlationId`, `status`, `code` y `message`.

| Método | Ruta | Archivo del handler | Entrada (body / ruta / query / cabecera) | Salida prevista | Servicios previstos; estado real |
|---|---|---|---|---|---|
| GET | `/WeatherForecast` | `Controllers/WeatherForecastController.cs` | Sin entrada obligatoria | `200`: 5 pronósticos con fecha, temperaturas y resumen aleatorios | Ninguno; funciona sin servicios externos. Ruta de plantilla fuera de `/api/v1`. |
| POST | `/api/v1/auth/register` | `Modules/Identity/Controllers/AuthController.cs` | Body JSON `email`, `password`, `displayName`; límite candidato de 8 KiB | `202` y mensaje genérico; alternativamente `400`, `422`, `429` o `503` | Rate limit **Redis**, registro **Supabase**, perfil **MongoDB**: solo interfaces/adaptadores no configurados. |
| POST | `/api/v1/auth/login` | `Modules/Identity/Controllers/AuthController.cs` | Body JSON `email`, `password` | `200`: `accessToken`, `expiresIn`, `tokenType`, `user` (`id`, `roles`, `accountStatus`); o `400`/`401`/`429`/`503` | Rate limit/revocación JWT **Redis**, autenticación **Supabase**, perfil **MongoDB**: ninguno integrado. |
| GET | `/api/v1/auth/me` | `Modules/Identity/Controllers/AuthController.cs` | Cabecera `Authorization: Bearer` (sin body, ruta ni query) | `200`: `id`, `email`, `displayName`, `roles`, `accountStatus`; o `401`/`404`/`503` | Validación/revocación JWT **Redis**, identidad **Supabase**, perfil **MongoDB**: ninguno integrado. |
| GET | `/api/v1/users/me` | `Modules/Identity/Controllers/UsersController.cs` | Cabecera Bearer (sin body, ruta ni query) | `200`: `id`, `displayName`, `email`, `phone`, `avatarUrl`, `roles`; o `401`/`503` | Sesión como arriba; lectura opcional de avatar vía contrato Media, también sin integrar. |
| PATCH | `/api/v1/users/me` | `Modules/Identity/Controllers/UsersController.cs` | Cabecera Bearer; body JSON con `displayName` y/o `phone` (campos distintos rechazados) | `200`: perfil propio; o `400`/`401`/`409`/`503` | Sesión **Redis/Supabase/MongoDB**, escritura de perfil **MongoDB** y avatar opcional: adaptadores no configurados. |
| GET | `/api/v1/sellers/{sellerId}` | `Modules/Sellers/Controllers/SellersController.cs` | `sellerId` en ruta (24 hexadecimales); sin body ni query | `200`: `sellerId`, `displayName`, `ratingAverage`, `ratingCount`, `isActive`; o `400`/`404`/`429`/`503` | Rate limit público **Redis**, vendedor **MongoDB**, calificaciones por contrato Reviews: sin integración real. |
| GET | `/api/v1/admin/users` | `Modules/Admin/Controllers/AdminUsersController.cs` | Cabecera Bearer; query **obligatoria** `page`, `pageSize`; opcionales `search`, `role`, `accountStatus`, `sortBy` | `200`: `items` (id/nombre/roles/estado), `page`, `pageSize`, `total`; o `400`/`401`/`403`/`429`/`503` | Sesión **Redis/Supabase/MongoDB**, consulta **MongoDB**, rate limit **Redis** y auditoría: adaptadores no configurados. |
| GET | `/openapi/v1.json` | `Program.cs` (`app.MapOpenApi()`) | Sin entrada obligatoria; **solo Development** | `200`: especificación OpenAPI generada | Ningún servicio externo. Endpoint operativo, no ficha funcional. |

El perfil público de vendedor omite calificaciones si falla el lector opcional; el perfil propio omite el avatar si no puede obtenerse una URL aprobada. No hay endpoint `/health` ni implementación de las demás rutas previstas en las fichas.

## 3. Servicios externos y configuración

| Servicio | Archivo que crea la conexión/cliente | Archivos que registran sustitutos | Variables de entorno **leídas por el código actual** | Fallo de conexión/configuración en la aplicación actual |
|---|---|---|---|---|
| MongoDB Atlas | **Ninguno**: no existe `MongoClient` ni `MongoDB.Driver` en la API | `Program.cs`; `Modules/Identity/Infrastructure/UnconfiguredRegistrationDependencies.cs`, `UnconfiguredLoginDependencies.cs`, `UnconfiguredProfileUpdateRepository.cs`, `UnconfiguredAdminUserListReader.cs`; `Modules/Sellers/Infrastructure/UnconfiguredSellerProfileDependencies.cs` | **Ninguna** | Los repositorios ficticios lanzan `ExternalDependencyUnavailableException`; los servicios capturan el fallo y responden `503` genérico. No hay intento de conexión, retry ni health check de MongoDB. |
| Redis | **Ninguno**: no existe cliente `StackExchange.Redis` | `Program.cs`; `Modules/Identity/Infrastructure/UnconfiguredRegistrationDependencies.cs` (rate limit), `UnconfiguredLoginDependencies.cs` (gate JWT); `Modules/Sellers/Infrastructure/UnconfiguredSellerProfileDependencies.cs`; `Modules/Admin/Infrastructure/UnconfiguredAdminUsersDependencies.cs` | **Ninguna** | Rate limiting y validación de revocación fallan cerrados con `503` cuando se alcanza el adaptador; no se prueba un error de red real de Redis. |
| Supabase Auth | **Ninguno**: no existe cliente Supabase configurado | `Program.cs`; `Modules/Identity/Infrastructure/UnconfiguredRegistrationDependencies.cs`, `UnconfiguredLoginDependencies.cs`, `UnconfiguredSessionDependencies.cs` | **Ninguna** | Sus adaptadores lanzan `ExternalDependencyUnavailableException`; los servicios devuelven `503` genérico. En algunos flujos falla antes otro gate no configurado, por lo que no se llega siquiera a Supabase. |

**`.env.example`: no existe; por tanto no puede considerarse completo.** Tampoco hay lectura de un `.env` por la aplicación ni configuración de esas tres conexiones en `appsettings.json` o `appsettings.Development.json`. `SUPABASE_JWT_SECRET` aparece como **nombre propuesto en una tarea SDD**, pero no es una variable consumida por el código actual; no se debe confundir una propuesta documental con configuración implementada. No se incluyen aquí valores de credenciales, API keys ni URIs de conexión.

## 4. Ejecución y pruebas HTTP locales

Se inició la API con `dotnet run --no-build --no-launch-profile --project src/UTTTGO.Api/UTTTGO.Api.csproj`, `ASPNETCORE_ENVIRONMENT=Development` y `ASPNETCORE_URLS=http://127.0.0.1:51999`. Se enviaron peticiones a **cada ruta existente** en esa instancia; se usaron body de ejemplo y, para rutas privadas, un Bearer **ficticio** que no acredita autenticación real. Después se detuvo exclusivamente el servidor local de prueba. Una respuesta `503` significa que la ruta existe y responde, **no** que su función de negocio opere.

| Ruta probada | Estado funcional | HTTP y error o resultado exacto observado |
|---|---|---|
| `GET /WeatherForecast` | **Funciona** (plantilla) | `200`; arreglo de cinco pronósticos generados localmente. |
| `POST /api/v1/auth/register` | **Falla** | `503`; `code: AUTH_UNAVAILABLE`; `message: Servicio temporalmente no disponible.` |
| `POST /api/v1/auth/login` | **Falla** | `503`; `code: AUTH_UNAVAILABLE`; `message: Servicio temporalmente no disponible.` |
| `GET /api/v1/auth/me` | **Falla** | `503`; `code: AUTH_UNAVAILABLE`; `message: Servicio temporalmente no disponible.` |
| `GET /api/v1/users/me` | **Falla** | `503`; `code: AUTH_UNAVAILABLE`; `message: Servicio temporalmente no disponible.` |
| `PATCH /api/v1/users/me` | **Falla** | `503`; `code: PROFILE_UNAVAILABLE`; `message: Servicio temporalmente no disponible.` |
| `GET /api/v1/sellers/507f1f77bcf86cd799439011` | **Falla** | `503`; `code: SELLER_UNAVAILABLE`; `message: Servicio temporalmente no disponible.` |
| `GET /api/v1/admin/users?page=1&pageSize=10` | **Falla** | `503`; `code: ADMIN_USERS_UNAVAILABLE`; `message: Servicio temporalmente no disponible.` |
| `GET /openapi/v1.json` | **Funciona** (operativo, Development) | `200`; documento OpenAPI. |

**Stack traces:** no se produjo ninguno para esos fallos: los servicios interceptan excepciones de los adaptadores y emiten respuestas sanitizadas. No hay stack trace exacto que pegar sin inventarlo. Cada cuerpo `503` también incluye `correlationId` dinámico y `status: 503`; se omitió el identificador efímero porque varía por petición. El único diagnóstico adicional registrado por el servidor fue:

```text
warn: Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionMiddleware[3]
      Failed to determine the https port for redirect.
```

Esto corresponde al arranque HTTP local sin puerto HTTPS configurado; **no** es un stack trace ni demuestra un problema de conexión con los servicios externos. Los proyectos de tests existentes emplean dobles; sus resultados históricos en `docs/qa-api-*.md` no se reinterpretan aquí como pruebas actuales de Atlas, Redis o Supabase.

## 5. Documentación SDD presente

Archivos existentes en este árbol de trabajo (algunos son cambios locales todavía no confirmados en Git). Las descripciones son del contenido, **no** afirmaciones de aprobación integral:

| Archivo(s) | Contenido y estado |
|---|---|
| `docs/constitution.md` | Fuente de autoridad del stack, reglas BR-001..013, seguridad, arquitectura y decisiones abiertas. Su v2.4 (DEC-018) fija la nube AWS; la conciliación documental integral sigue pendiente. |
| `AGENTS.md` | Instrucciones de operación SDD, convenciones, pruebas, roles y límites para desarrollo del backend. Subordina contratos y stack a la constitución. |
| `memoria.md` | Historial vivo de decisiones, dependencias, estado de fases e integraciones faltantes. Distingue propuestas documentales de implementaciones probadas con dobles. |
| `docs/academic-requirements-phase-02.md` | Matriz de exigencias académicas y política/versiones propuestas para la fase 02. Mantiene abiertos los gates de aprobación, costo cero y dos instancias. |
| `docs/decisiones-pendientes-fase-02.md` | Registra autorizaciones funcionales parciales y decisiones técnicas pendientes. No convierte esas autorizaciones en aprobación completa de contratos y despliegue. |
| `docs/traceability.md` | Matriz del inventario de 42 fichas API y sus vínculos con RF, planes, tareas y evidencias previstas. No implica que existan 42 handlers. |
| `docs/traceability-siete-endpoints.md` | Trazabilidad acotada de siete rutas candidatas locales de identidad/usuarios/vendedores. Complementa, no sustituye, la matriz general. |
| `docs/referencias-profesor-backend.md` | Referencias metodológicas y técnicas del profesor para todo el backend. Son ejemplos subordinados al stack y a los contratos aprobados. |
| `docs/qa-api-001.md`, `docs/qa-api-002.md`, `docs/qa-api-004.md` | Evidencia de QA local de registro, login y sesión; casos con dobles y límites de lo que **no** se verificó externamente. |
| `docs/qa-api-006.md`, `docs/qa-api-007.md`, `docs/qa-api-008.md` | QA de perfil propio, edición y perfil público de vendedor. Registra contratos candidatos, pruebas con dobles y pendientes de Media/Mongo/seguridad real. |
| `docs/qa-api-039.md` | QA local de listado administrativo y auditoría con dobles. Aclara que no existe persistencia ni autorización integradas. |
| `specs/phase-00-constitution/00-01-principles-stack/spec.md` | Requisitos, diseño, límites, tests y aceptación de principios/stack. v1.2 es borrador de conciliación; una versión anterior fue aprobada. |
| `specs/phase-00-constitution/00-01-principles-stack/plan.md` | Plan de arquitectura base, estructura y dependencias de arranque. La infraestructura descrita es objetivo, no evidencia de conexiones creadas. |
| `specs/phase-00-constitution/00-01-principles-stack/task.md` | Desglose de preparación, dependencias, configuración, middleware y Docker. Contiene tareas futuras que no deben darse por implementadas. |
| `specs/phase-02-auth-users/spec.md` | Requisitos de identidad, usuarios y vendedores, historias, contratos y casos límite; v1.5 tiene aprobación individual **para desarrollo local incremental**, no aprobación integral del equipo. |
| `specs/phase-02-auth-users/plan.md` | Diseño de fronteras, datos, OTP, contratos y pruebas previstas. v1.6 sigue en conciliación y mantiene gates abiertos. |
| `specs/phase-02-auth-users/task.md` | Tareas TASK-AUTH, dependencias y criterios verificables; distingue trabajo local de cierre de integraciones reales y aprobación integral. |
| `specs/phase-02-auth-users/api-039-list-users/spec.md` | Ficha local candidata para listar usuarios con paginación, filtros y autorización Admin. La ratificación local no equivale a despliegue ni a contrato integral aprobado. |
| `specs/phase-02-auth-users/api-039-list-users/plan.md` | Diseño del flujo Admin → Identity, auditoría y persistencia futura. Indica que no hay adaptador de datos real. |
| `specs/phase-02-auth-users/api-039-list-users/task.md` | Tareas y evidencia de implementación/QA locales con dobles; quedan pendientes integraciones y gates de aprobación. |
| `specs/phase-03-catalog-offers/spec.md` | Borrador v2.0 de productos propios por vendedor, búsqueda y contratos API-011..022 por conciliar. No autoriza el modelo histórico de producto maestro/ofertas compartidas. |
| `specs/phase-03-catalog-offers/plan.md` | Modelo, rutas, seguridad, caché y pruebas propuestas para catálogo. Integraciones y aprobaciones siguen pendientes. |
| `specs/phase-03-catalog-offers/task.md` | Gates documentales y tareas de catálogo bloqueadas hasta conciliar/aprobar contratos. No existen handlers de catálogo en el proyecto actual. |
| `specs/phase-07-chat-realtime/spec.md` | Borrador funcional de historial temporal de chat por pedido y contratos pendientes. La persistencia, purga y pruebas todavía requieren diseño. |
| `specs/cross-cutting/infra-aws-free-tier/spec.md` | Requisitos de la infraestructura AWS de DEC-018 (EC2, EIP, SG, S3, IAM, CloudWatch, Budget) y criterios de aceptación. Aprobada; `plan`/`apply` pendientes. |
| `specs/cross-cutting/infra-aws-free-tier/plan.md` | Secuencia de implementación, decisiones de diseño y verificaciones de la IaC. Los pasos de código están completos; los de despliegue siguen pendientes del propietario. |
| `specs/cross-cutting/infra-aws-free-tier/task.md` | Tareas INFRA-T01..T15 con evidencia: IaC aplicada por el propietario y stack de despliegue verificado localmente; pendientes `setup.sh` en la EC2, TLS, alerta de Budget, backend remoto de estado y presigned en el backend. |
| `infra/terraform/` | IaC declarativa de AWS (proveedor AWS `us-east-2`): EC2 t3.micro Ubuntu 24.04 con IMDSv2, EIP, SG 22/80/443, Instance Profile (CloudWatch + SSM opcional), log group, bucket S3 privado con CORS y Budget condicional. Verificada con `fmt`, `init -backend=false` y `validate`; **aplicada por el propietario** (EIP `3.129.142.54`); falta un `apply` para el hop limit de IMDS y la alerta de Budget. |
| `.github/workflows/ci-1.yml` | CI con dobles: restore/NuGetAudit, formato, build Release, tres suites, resumen, Gitleaks (bloqueante), Semgrep (piloto informativo) y evidencia. Única edición reciente: `push` a `main` para habilitar el gate del CD. |
| `.github/workflows/cd-deploy.yml` | CD-1: se ejecuta solo con CI-1 verde en `push` a `main` (o manual con motivo); despliega por SSH el SHA verificado con `docker compose up -d --build --scale api=2`, espera réplicas sanas y publica evidencia. **Implementado y validado con actionlint, pero sin ejecutar**: faltan secrets, environment con revisores y deploy key en la instancia. |
| `Dockerfile` / `.dockerignore` | Imagen de la API .NET 10 (SDK 10 → ASP.NET 10, usuario `app`, sin secretos). Construida localmente con `docker compose build api`. |
| `docker-compose.yml` | Despliegue en la EC2: `api` con `deploy.replicas: 2`, `redis:7-alpine` sin puertos publicados en red interna, `nginx:alpine` con 80/443, `mongo:7` solo en perfil `local`. Validado con `docker compose config` y prueba local de balanceo entre las 2 réplicas. |
| `nginx/` | `nginx.conf`, `conf.d/default.conf` (balanceo por DNS de Docker, WebSocket, desafío ACME) y `conf.d/tls.conf.template` (443 + redirección). Sintaxis validada con `nginx -t` en la imagen real. |
| `setup.sh` | Script de la EC2 (Ubuntu 24.04): instala Docker, Docker Compose y Certbot, prepara `.env`, levanta 2 réplicas + Redis + Nginx, emite el TLS con hook de renovación y verifica. **No ejecutado todavía en la instancia.** |
| `specs/cross-cutting/ci-cd-devsecops/spec.md` | Requisitos transversales de CI/CD, seguridad y aceptación; registra evidencia CI-1, no integración externa ni CD aprobados. |
| `specs/cross-cutting/ci-cd-devsecops/plan.md` | Secuencia de quality gates CI-1 y fases posteriores. Separa evidencia de push/PR de futura integración real/despliegue. |
| `specs/cross-cutting/ci-cd-devsecops/task.md` | Tareas y evidencia de CI-1; aprobación integral, piloto adversarial y CD permanecen pendientes. |

`material/` contiene documentos fuente PDF, DOCX y XLSX, incluido el Excel de 42 fichas; no son archivos `spec.md` implementados. También hay un directorio `.github/workflows/` y artefactos `ci-evidence/`: no deben confundirse con un despliegue en la nube operativo.

## 6. Faltantes o configuraciones a revisar

- `.env.example` existe (Atlas, Redis, Supabase, S3 y región AWS) y `docker-compose.yml` ya está materializado (2 réplicas de la API + Redis + Nginx; MongoDB solo en perfil `local`); el estado de Terraform en `infra/terraform` es local y el despliegue no se ha ejecutado en la EC2; no se definieron en código los clientes reales de Atlas, Redis, S3 y Supabase. La tabla de variables prevista en SDD no está materializada en `appsettings`.
- Falta la implementación de `MongoDB.Driver`, `StackExchange.Redis`, autenticación/JWT real de Supabase, índices, persistencia y rate limiting distribuido. Los adaptadores actuales son deliberadamente **fail-closed**, no errores de contraseña o puertos comprobados.
- `Program.cs` configura `UseAuthorization()` pero no `AddAuthentication()`/`UseAuthentication()` ni política CORS; la autenticación candidata se hace mediante servicios en los handlers y no prueba el middleware JWT exigido para toda la API.
- No hay `/health` para sondear MongoDB y Redis; no hay configuración de producción comprobada para HTTPS/CORS. `AllowedHosts: "*"` en `appsettings.json` es control de hosts, **no** una política CORS.
- Los perfiles de `launchSettings.json` usan HTTP `localhost:5122` y HTTPS `localhost:7128`; la prueba con `--no-launch-profile` solo expuso HTTP `127.0.0.1:51999`, que explica la advertencia de redirección. No se verificó conectividad real de esos puertos predeterminados ni un certificado local.
- El SDK instalado es **preview** aunque la constitución pide .NET 10 LTS estable; verificar compilación con SDK estable en el entorno de entrega. No se constató una incompatibilidad de paquetes mediante esta prueba HTTP.
- La ruta de plantilla `/WeatherForecast` no cumple el prefijo constitucional `/api/v1`. La ruta OpenAPI solo se mapea en Development y no representa una ficha del negocio.
- Faltan decisiones/aprobación integral de contratos SDD y evidencia de viabilidad de dos instancias/costo cero antes de infraestructura o despliegue; los tests con dobles y un `200` de plantilla no satisfacen esos gates.

### Cinco bloqueos principales (ordenados por impacto)

1. **Integraciones críticas inexistentes:** sin clientes reales MongoDB, Redis y Supabase, las siete rutas de negocio probadas devuelven `503`.
2. **Seguridad productiva incompleta:** no hay middleware de autenticación JWT real, blacklist/revocación Redis ni rate limiting distribuido verificable.
3. **Falta configuración local reproducible:** `docker-compose.yml` y conexiones/variables consumidas por código no existen; no se pueden probar flujos externos de punta a punta en aislamiento.
4. **Gates SDD y académicos sin cierre integral:** contratos y viabilidad de infraestructura/despliegue requieren revisión y evidencia adicionales; no convertir candidatos locales en contratos finales.
5. **Operación y alcance incompletos:** faltan `/health`, HTTPS/CORS comprobados, módulos previstos y eliminación de la ruta de plantilla fuera de `/api/v1`; el SDK local es preview.
