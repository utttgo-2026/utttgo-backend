# AGENTS.md — Backend UTTT-GO
> Manual operativo de agentes y desarrolladores del backend.
> Versión 2.4 · 2026-11-08
> Alineado con `docs/constitution.md` v2.5 (DEC-018: nube AWS; DEC-019: retirada de API-016 e identidad del vendedor). Si hay conflicto, gana `docs/constitution.md`.
> Flujo SDD obligatorio: `docs/constitution.md` → `spec.md` → `plan.md` → `task.md` → implementación.
> Referencias metodológicas y técnicas del profesor para **todo el backend**: `docs/referencias-profesor-backend.md`; son subordinadas a la constitución y no modifican contratos aprobados.

---

## 1. Forma de trabajo

### 1.1 Stack obligatorio

> Fuente canónica: `constitution.md` § 2 y § 3. No introducir tecnologías fuera del stack sin una nueva versión de la constitución.

- **Runtime:** .NET 10 LTS, C# 14, ASP.NET Core 10.
- **Arquitectura:** monolito modular con fronteras por módulo.
- **Nube:** Amazon AWS, región `us-east-2`. Cómputo en **una EC2 t3.micro** (Ubuntu 24.04) con Elastic IP (DEC-018).
- **Balanceo:** Nginx en la instancia con **2 réplicas Docker Compose** de la API («dos instancias» del PDF académico a costo $0). La API es stateless.
- **IaC:** Terraform con proveedor AWS en `infra/terraform`.
- **Datos:** MongoDB Atlas con `MongoDB.Driver`.
- **Auth:** Supabase Auth; roles en el perfil local.
- **Vendedores:** autoactivación al completar nombre de negocio, descripción y punto de recogida; sin aprobación administrativa previa en el MVP (DEC-002). El cliente no asigna roles.
- **Redis:** contenedor Redis 7 en el `docker-compose.yml` de la instancia (sin ElastiCache por costo $0, DEC-018) para caché, rate limiting distribuido y blacklist JWT.
- **Storage:** Amazon S3 bucket **privado** con URLs presignadas generadas exclusivamente por el backend.
- **Moderación:** Azure AI Content Safety API antes de publicar imágenes (se conserva como servicio externo; DEC-018).
- **Push:** Firebase Cloud Messaging con FCM Admin SDK, únicamente desde el backend.
- **Tiempo real:** SignalR con backplane obligatorio.
- **API:** prefijo `/api/v1`.

### 1.2 Setup local

La solución y el proyecto **existentes** son `UTTTGO.Backend.slnx` y `src/UTTTGO.Api/UTTTGO.Api.csproj` (Controllers). Ya existen `tests/UtttGo.UnitTests/`, `tests/UtttGo.IntegrationTests/` y `tests/UtttGo.SecurityTests/` **solo con cobertura local simulada de API-001/002**. El despliegue en la EC2 **ya existe como configuración**: `Dockerfile`, `docker-compose.yml` (2 réplicas de la API + Redis interno + Nginx; MongoDB solo en perfil `local`), `nginx/` y `setup.sh` (Docker, Compose, Certbot, TLS); validado localmente con `docker compose config`, build de imagen, `nginx -t` y prueba de balanceo entre réplicas, **sin ejecutarse todavía en la instancia**. Los workflows de CD siguen siendo objetivo **aún no implementado**. `infra/terraform` **existe y fue aplicado por el propietario** (DEC-018: EC2, EIP, SG, S3, IAM, CloudWatch, Budget; Elastic IP `3.129.142.54`), con la alerta de Budget y el ajuste de hop limit de IMDS pendientes de un nuevo `apply`. No ejecutar sus comandos como si fueran verificaciones realizadas. La conciliación técnica de la solución `.slnx` sustituye la prohibición documental anterior, sin cambiar el stack.

En fase 02, `docs/academic-requirements-phase-02.md` distingue obligaciones del PDF (incluidas **dos instancias y costo cero**) de recomendaciones (Terraform en el PDF, obligatorio por la constitución) y herramientas sugeridas. La viabilidad se resolvó con **DEC-018** (cambio a AWS: una EC2 + 2 réplicas Compose + Nginx); la **evidencia final de costo $0 queda pendiente** de la alerta AWS Budget tras el primer apply. TASK-AUTH-001/006 están cerradas por evidencia documental; eso no cierra TASK-AUTH-007.

1. Copiar variables de entorno:
   ```powershell
   Copy-Item .env.example .env
   ```
   Completar valores locales ficticios. `.env` está en `.gitignore` y nunca se comitea.
2. Levantar dependencias locales:
   ```powershell
   docker compose up -d
   ```
   El `docker-compose.yml` de la raíz ya existe: API en **2 réplicas** (`deploy.replicas: 2`), `redis:7-alpine` **sin puertos publicados** en red interna, `nginx:alpine` con 80/443 y `mongo:7` solo bajo el perfil `local` (espejo local de Atlas). No apuntar pruebas locales a Atlas, Redis ni S3 de producción.
3. Restaurar y ejecutar:
   ```powershell
   dotnet restore UTTTGO.Backend.slnx
   dotnet run --project src/UTTTGO.Api/UTTTGO.Api.csproj
   ```
4. Verificar la sonda operativa `GET /health`: devuelve `200` con `{"status":"Healthy","checks":{...}}` si MongoDB y Redis responden, y `503` con el contrato de error único si alguna dependencia crítica falla. Implementada en `src/UTTTGO.Api/Shared/Health/` (ficha `specs/cross-cutting/api-health-probe/`).

Para `dotnet run`, exportar las mismas variables del `.env` o usar secretos locales de desarrollo (`dotnet user-secrets`), nunca comiteados. En la EC2, los valores son variables de entorno del contenedor (`.env` del servidor, fuera de Git); la sonda de salud y el despliegue se coordinan con el `docker-compose.yml` futuro.

### 1.3 Comandos canónicos

| Objetivo | Comando |
|---|---|
| Restaurar | `dotnet restore UTTTGO.Backend.slnx` |
| Compilar Release | `dotnet build UTTTGO.Backend.slnx -c Release --no-restore` |
| Formato | `dotnet format UTTTGO.Backend.slnx --verify-no-changes --no-restore` |
| Unitarias | `dotnet test tests/UtttGo.UnitTests/UtttGo.UnitTests.csproj -c Release --no-build` |
| Integración | `dotnet test tests/UtttGo.IntegrationTests/UtttGo.IntegrationTests.csproj -c Release --no-build` |
| Seguridad | `dotnet test tests/UtttGo.SecurityTests/UtttGo.SecurityTests.csproj -c Release --no-build` |
| Ejecutar API | `dotnet run --project src/UTTTGO.Api/UTTTGO.Api.csproj` |
| Docker local | `docker compose up -d` / `docker compose down` (MongoDB local: `docker compose --profile local up -d`) |
| Terraform formato | `terraform -chdir=infra/terraform fmt -check -recursive` |
| Terraform validación | `terraform -chdir=infra/terraform init -backend=false` + `terraform -chdir=infra/terraform validate` |
| Terraform plan | `terraform -chdir=infra/terraform plan` |

Unitarias, integración y seguridad **locales con dobles** de API-001/002 ya existen y se ejecutan (`docs/qa-api-001.md`, `docs/qa-api-002.md`); **Terraform existe ahora y está validado** con `terraform init -backend=false`, `fmt -check` y `validate`; su `plan/apply` corre localmente el propietario con sus credenciales, sin secretos en Git, y la Dockerización de despliegue sigue prevista. No inferir integraciones externas ni seguridad real verde del build o de pruebas con dobles.

### 1.4 Orden de CI en PR

```
restore → format → build → unit → integration → security → secret scan → SAST (si está habilitado) → terraform fmt/validate (si cambia infraestructura)
```

- Los tests corren con `--no-build` después del build Release.
- No se adoptan Pact, NBomber, Stryker ni ChaosTest en esta fase.
- Todo PR debe pasar build y tests en verde; un PR no se aprueba con pruebas rojas o deshabilitadas.
- Terraform `apply` no se ejecuta desde un PR; se ejecuta en el pipeline de despliegue con aprobación.

### 1.5 Ramas, PRs y naming

- `main` protegida; `develop` como rama de trabajo.
- Un PR = una feature o fix. No mezclar refactors masivos con funcionalidad.
- Checklist mínimo del PR:
  - [ ] Existe `spec.md` aprobado para la funcionalidad.
  - [ ] `plan.md` y `task.md` actualizados.
  - [ ] Build Release sin errores.
  - [ ] Tests nuevos/actualizados y verdes.
  - [ ] Sin secretos, connection strings, tokens ni service accounts.
  - [ ] `memoria.md` actualizado.
  - [ ] Trazabilidad actualizada.
- Código en inglés; documentación en español.
- Endpoints en kebab-case; JSON en camelCase.
- Colecciones MongoDB en camelCase plural (`products`, `offers`, `orders`).
- Dinero como `decimal` en MXN; nunca `float`/`double`.
- Sin prefijos legacy de tablas/columnas; no aplican en MongoDB.
- Sin TODOs sin issue, sin usings no usados, sin código muerto.
- Al terminar una tarea, resumir qué cambió y qué decisión debe revisarse.

---

## 2. Memoria y gobernanza SDD

- `memoria.md` es el registro vivo de decisiones, estado, dependencias y glosario.
- **Al empezar:** leer `memoria.md` para conocer estado y decisiones previas.
- **Al terminar:** actualizar estado, decisiones importantes (con su porqué) y errores a evitar.
- **Al cerrar una fase:** conciliar `spec.md`, `plan.md`, `task.md` y la implementación con rutas, nombres reales de archivos, tests y criterios verificables.
- Si una decisión se vuelve regla permanente, proponer moverla a `AGENTS.md` o `constitution.md`; no dejarla solo en memoria.
- Mantener la memoria breve y sin datos sensibles (claves, tokens, datos personales).
- Las decisiones abiertas se registran en `constitution.md` § 16 y en `memoria.md`. Ningún agente las resuelve unilateralmente.

---

## 3. Reglas duras

### 3.1 Negocio (innegociables)

> Fuente canónica: `constitution.md` § 3. Estas reglas no se reinterpretan en specs ni tareas.

| ID | Regla |
|---|---|
| BR-001 | **No existe carrito de compras.** La compra es directa e individual. |
| BR-002 | Cada pedido corresponde a **un único producto de un vendedor** con su cantidad. |
| BR-003 | El **único método de pago es efectivo presencial** en el campus UTTT. No hay pasarela ni datos financieros. |
| BR-004 | **No existen menús ni horarios.** La disponibilidad depende de existencias/estado del producto; ingredientes no son menú. |
| BR-005 | La comparación se hace entre **productos independientes de distintos vendedores** encontrados por búsqueda/filtros (ingredientes, precios y calificaciones disponibles). |
| BR-006 | Cada vendedor posee y administra sus **propios productos/publicaciones**; no hay producto maestro compartido ni ofertas asociadas. |
| BR-007 | Toda imagen usa **URL presignada de S3 generada por el backend** y **moderación previa** con Azure AI Content Safety. |
| BR-008 | Las notificaciones push se envían **solo** por el backend con FCM Admin SDK. |
| BR-009 | El comprador no decide precio/total/estado ni identidad; el vendedor propietario puede solicitar cambios de precio, stock y publicación, validados por backend; identidades/roles nunca se confían al cliente. |
| BR-010 | Total server-side = precio vigente del producto elegido × cantidad; definir concurrencia/snapshot en Orders. |
| BR-011 | Eliminar cuenta elimina información propia, incluyendo productos/publicaciones/historial del vendedor; datos compartidos requieren diseño específico. |
| BR-012 | Chat conservado mientras el pedido esté en curso; historial eliminado dentro de tres días de completarse. |
| BR-013 | Recuperación de contraseña por enlace al correo registrado, usando Supabase Auth; ruta backend nueva requiere ficha aprobada. |

### 3.2 Stack y datos

- No introducir SQL Server, EF Core, migraciones EF, MassTransit, SQS, sagas, DLQ ni bases relacionales.
- MongoDB no usa migraciones: los índices y validaciones se declaran en el `spec.md` de cada módulo.
- Redis es obligatorio para blacklist JWT, caché y rate limiting distribuido.
- Las imágenes no se guardan en MongoDB ni en disco local; solo en un bucket S3 privado.
- Terraform es la única vía para crear/modificar infraestructura AWS de staging/producción.
- Prohibido el estado de Terraform (`*.tfstate`, `*.tfstate.backup`, `*.tfvars` con secretos) en Git.

### 3.3 Seguridad

- El logout revoca el JWT agregando su `jti` a la blacklist de Redis: `revoked:{jti}` con TTL hasta `exp`.
- Si Redis no responde, la blacklist y el rate limit de `/auth/*` son **fail-closed**.
- Las URLs presignadas solo las genera el backend, con alcance de un objeto S3, TTL corto y permisos mínimos.
- Ningún objeto del bucket de imágenes es público (Block Public Access activado).
- Sin moderación aprobada no hay publicación de imagen: **fail-closed**.
- El cliente nunca conoce el service account de FCM ni envía push directamente.
- Secretos solo en variables de entorno de la instancia/contenedor. Nunca en código, `appsettings`, `.env` comiteado, Terraform con valores reales ni logs.
- No tocar el orden del pipeline de middleware de `Program.cs` sin aprobación de AGENT-BE-01 y actualización de `spec.md`.
- No relajar rate limits en producción; solo configuración explícita no-productiva.

---

## 4. Verificación y calidad

Antes de pedir revisión de un cambio:

- [ ] `dotnet build -c Release --no-restore` con 0 errores.
- [ ] `dotnet format --verify-no-changes` sin diferencias.
- [ ] Unitarias, integración y seguridad en verde.
- [ ] Pruebas de media/presigned y notificaciones FCM actualizadas si el cambio las impacta.
- [ ] `terraform fmt -check -recursive` y `terraform validate` en verde si cambió `infra/terraform`.
- [ ] Sin secretos en el diff; secret scanning del CI en verde.
- [ ] Sin endpoints nuevos fuera de `spec.md` y sin contratos sin ficha.

**Alcance provisional del MVP:** las 42 fichas API-001 a API-042 de `material/UTTT_GO_Punto_4_Especificacion_API_v1.xlsx` siguen sin modificación formal. **API-016 está resuelta por DEC-019 (constitución v2.5): no se implementa ni se redirige** y su función queda cubierta por la búsqueda y los filtros de API-011. Cualquier ruta adicional requiere ficha nueva y aprobación (aprobada: `GET /api/v1/sellers/me`, DEC-019.d); `GET /health` es sonda operativa. Las diferencias constan en `docs/traceability.md` y se concilian `constitution.md` → `spec.md` → `plan.md` → `task.md`.

**NFR:** no hay umbrales de rendimiento o cobertura aprobados todavía. No inventarlos. Cuando se definan en un `spec.md` de NFR, cada umbral debe ser verificable por comando o test y este archivo se actualiza.

**Configuración:** la tabla de variables vive en `spec.md` § 2 y `.env.example`. `appsettings.*.json` solo documenta claves con código real; secretos fuera de Git.

---

## 5. Convenciones técnicas

### 5.1 Módulos y API

- Módulos previstos: `Identity`, `Users`, `Sellers`, `Catalog`, `Orders`, `Reviews`, `Favorites`, `Notifications`, `Chat`, `Media`, `Admin`. No presupone módulo independiente `Offers`.
- Un módulo no accede a colecciones ni repositorios de otro. Se comunica por interfaces/contratos del módulo dueño.
- Rutas: `/api/v1/...`; acciones en kebab-case.
- DTOs explícitos + FluentValidation. No exponer documentos de MongoDB directamente.
- Contrato de error único de `spec.md` § 4; `correlationId` en toda respuesta de error.
- Paginación obligatoria en listados: `page` + `pageSize` con máximo configurable.

### 5.2 MongoDB

- Driver oficial `MongoDB.Driver`; POCOs con `[BsonId]`.
- `ObjectId` serializado como `string` en JSON.
- Fechas UTC; no `DateTimeOffset`.
- Sin ORM ni migraciones. Índices justificados por consultas reales en el spec del módulo.
- Cada módulo es dueño de sus colecciones; el mapa se mantiene en `memoria.md`.

### 5.3 Redis

| Uso | Clave | TTL |
|---|---|---|
| Blacklist JWT | `revoked:{jti}` | Hasta `exp` |
| Caché | `cache:{recurso}:{clave}` | Configurable por recurso |
| Rate limit | `ratelimit:{policy}:{partición}` | Ventana de la política |

- Cliente `StackExchange.Redis`.
- Prohibido cachear contraseñas, tokens completos o datos personales sensibles.
- Las claves de prueba deben limpiarse entre tests; no depender de estado global.

### 5.4 Imágenes: presigned S3 + moderación

Flujo obligatorio:

1. El cliente pide intención de subida al backend.
2. El backend valida JWT, MIME, extensión y tamaño; crea `mediaAssets` en `PendingUpload` y devuelve una **presigned PUT** de un solo objeto.
3. El cliente sube directo a la carpeta de staging del bucket S3 privado (CORS configurado en Terraform).
4. El cliente notifica fin; el backend invoca Azure AI Content Safety.
5. Seguro → `Approved`; no seguro → se elimina el objeto y queda `Rejected`.
6. Solo un `mediaAssetId` en `Approved` puede asociarse a un producto publicado.

- Presigned GET de lectura para imágenes aprobadas: corta duración, generada por el backend.
- Nunca access keys de AWS ni session tokens al cliente; el backend usa el Instance Profile de la EC2.
- Error de Content Safety = no publicación (fail-closed).

### 5.5 FCM

- Envío exclusivo desde el backend con FCM Admin SDK.
- `deviceTokens` por usuario; tokens inválidos se eliminan tras el error de FCM.
- El service account vive solo en variables de entorno de la instancia; nunca en Git.
- Envíos idempotentes y derivados de eventos de dominio (estado de pedido, chat).

### 5.6 SignalR

- Grupos derivados de identidades autorizadas; nunca de IDs sin validar.
- Backplane obligatorio con múltiples instancias detrás del balanceador.
- Estado de reconexión en Redis/MongoDB, no en memoria del proceso.

### 5.7 Pedidos

- Un pedido = un producto de un vendedor (BR-001, BR-002).
- Pago fijo `Cash`; `Delivered` implica efectivo recibido (BR-003).
- Máquina de estados de `constitution.md` § 13. Transición inválida = `409 Conflict`.

---

## 6. Seguridad y DevSecOps

- JWT de Supabase validado en cada request: firma, issuer, audience, expiración y blacklist Redis.
- Roles **no** se leen del JWT; se leen del perfil local en MongoDB.
- Autorización por recurso: verificar propiedad, no solo rol.
- Rate limiting distribuido en todas las rutas públicas; más estricto en `/auth/*`; respuesta `429` con contrato de error.
- CORS explícito; prohibido `*` en producción.
- HTTPS en todos los entornos.
- `correlationId` en logs y respuestas de error.
- Logs sin contraseñas, tokens, `jti`, datos personales sensibles ni claves.
- Presigned: alcance mínimo, un objeto, TTL ≤ 5 minutos, content-type y tamaño restringidos.
- Moderación previa obligatoria; sin excepciones por entorno.
- Terraform sin secretos en código; credenciales del usuario solo por variables de entorno locales (`AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY`), nunca comiteadas.
- SAST y escaneo de dependencias: recomendados en CI; se vuelven bloqueantes cuando el equipo los habilite formalmente.

### 6.1 Degradación de Redis

| Uso | Comportamiento | Resultado |
|---|---|---|
| Blacklist JWT | Fail-closed | Rechazar token (`401`/`503`) |
| Rate limit `/auth/*` | Fail-closed | Rechazar autenticación |
| Rate limit general | Fail-closed con alerta | `503` + incidente |
| Caché | Fail-open | Ir a MongoDB + registrar degradación |

---

## 7. Testing

- xUnit + FluentAssertions + Moq/NSubstitute.
- `WebApplicationFactory` para integración y seguridad.
- Testcontainers para MongoDB y Redis.
- Emulación S3 (LocalStack o Moto) o dobles para pruebas de imágenes en local; sin acceso al bucket real desde CI.
- Content Safety y FCM se mockean en unitarias/integración; nunca se llaman servicios reales en CI.
- Proyectos: `UtttGo.UnitTests`, `UtttGo.IntegrationTests`, `UtttGo.SecurityTests`.
- Casos mínimos:
  - Unitarias: servicios, validadores, cálculo de total, propiedad y reglas de producto/pedido.
   - Integración: endpoints contra MongoDB/Redis en contenedores; flujo presigned con emulador S3 o dobles.
  - Seguridad: token inválido/expirado/revocado, rol incorrecto, recurso ajeno, rate limit, blacklist.
   - Media/IA: presigned solo backend, alcance mínimo, moderación antes de publicar, rechazo de contenido no seguro.
  - Notificaciones: FCM mockeado, depuración de tokens, idempotencia.
  - Aceptación: Given/When/Then derivados del `spec.md`.
- No se usan Pact, NBomber, Stryker ni ChaosTest en esta fase.
- Prohibido ejecutar pruebas contra entornos productivos.
- `SecurityTests` puede reutilizar helpers de `UnitTests/Common` (`TokenHelper`, `TestDataFactory`).

---

## 8. Observabilidad

- Serilog con consola JSON en desarrollo y entrega de logs a CloudWatch Logs desde la instancia (cuando se habilite el agente o el sink en la app).
- `correlationId` en cada evento de log del ciclo de vida.
- `GET /health` verifica MongoDB y Redis; devuelve `503` si una dependencia crítica falla. Es la **readiness** del stack: el healthcheck de Nginx la consulta; el healthcheck del contenedor de la API sigue siendo **liveness** (TCP al puerto 8080) para no reiniciar réplicas por una caída de Atlas.
- Logs de moderación y FCM con resultado y `correlationId`, sin payloads sensibles.
- No se adopta Prometheus/Grafana en esta fase.
- No loggear tokens, presigned completas, `jti`, access keys ni service accounts.

---

## 9. Despliegue y operación

- **Una EC2 t3.micro** (Ubuntu 24.04) con **Elastic IP fija**; costo objetivo $0 (Free Tier). Nginx en la instancia reparte tráfico entre **2 réplicas Docker Compose** de la API; el requisito académico de «dos instancias» se cumple internamente (DEC-018).
- API stateless; estado efímero en Redis (contenedor del mismo Compose).
- SignalR con backplane Redis para las 2 réplicas.
- Terraform en `infra/terraform` (proveedor AWS, región `us-east-2`):
  - Estado local fuera de Git en el MVP; backend remoto (S3 + DynamoDB) es refactor obligatorio antes de producción.
  - EC2 + EIP, Security Group (22/80/443), bucket S3 privado (CORS, Block Public Access, SSE), rol/Instance Profile IAM (CloudWatch Logs, opcional SSM), log group y alerta AWS Budget.
  - Sin secretos: las credenciales del propio dueño llegan por variables de entorno, nunca por código.
- MongoDB Atlas se aprovisiona por su mecanismo documentado; connection strings solo por variables de entorno de la instancia.
- FCM service account en variables de entorno de la instancia.
- CI/CD futuro con GitHub Actions e OIDC hacia AWS sin credenciales estáticas; mientras tanto `apply` manual local del propietario con su aprobación.
- Prohibido crear o modificar recursos a mano en staging/producción (cambios solo por Terraform).
- Promoción entre entornos por pipeline, no manual.
- Redis: timeouts cortos, retry exponencial y comportamiento de degradación de § 6.1.
- Contenedores: verificar herramientas disponibles antes de sondear desde dentro de la imagen.

---

## 10. Agentes y sub-agentes

### 10.1 Roles SDD del proyecto

| Agente | Rol | Responsabilidad principal |
|---|---|---|
| AGENT-BE-01 | Architect | Arquitectura, módulos, ADRs, Terraform y alineación con la constitución |
| AGENT-BE-02 | API Designer | Contratos, endpoints, Given/When/Then y specs verificables |
| AGENT-BE-03 | Data Modeler | Colecciones MongoDB, índices, Redis y fronteras de datos |
| AGENT-BE-04 | Security Reviewer | JWT, autorización por recurso, rate limit, presigned, Content Safety y secretos |
| AGENT-BE-05 | Test Engineer | Unitarias, integración, seguridad, media/IA y notificaciones |
| AGENT-BE-06 | Memory Keeper | `memoria.md`, decisiones, glosario y log de cambios |

Ningún agente genera código de producción sin `spec.md` aprobado. Ningún agente cambia el stack, las reglas de negocio ni la infraestructura unilateralmente.

### 10.2 Sub-agentes operativos

Solo se integran los que tienen sentido directo para UTTT-GO:

#### `module-scaffolder`
- **Propósito:** crear el esqueleto compilable de un módulo (contratos, endpoint base, servicio, repositorio, validadores y pruebas esqueleto).
- **Entrada:** `spec.md` aprobado + tarea de `task.md`.
- **Salida:** estructura del módulo, registro DI, health check si aplica y tests con nombres derivados de Given/When/Then.
- **Límites:** no implementa lógica de negocio fuera del spec; no crea carrito, menús, horarios ni pasarelas; no comitea si el build falla.
- **Dueño:** AGENT-BE-01, con apoyo de AGENT-BE-03.

#### `security-reviewer`
- **Propósito:** gate de seguridad previo a push/PR; revisa sin editar.
- **Verifica:** JWT + blacklist Redis, autorización por recurso, rate limit, presigned (alcance/TTL), Content Safety, S3 no público, FCM server-only, secretos, CORS, logs y contrato de error.
- **Salida:** hallazgos por severidad y bloqueantes.
- **Límites:** no modifica código ni specs; reporta para que AGENT-BE-04 y el autor corrijan.
- **Dueño:** AGENT-BE-04.

#### `traceability-clerk`
- **Propósito:** mantener la matriz viva `spec ↔ plan ↔ task ↔ endpoint ↔ test`.
- **Salida:** matriz actualizada en `docs/traceability.md` y reporte de drift.
- **Límites:** no edita `spec.md`, `plan.md`, `task.md` ni código; solo reporta y actualiza la matriz.
- **Dueño:** AGENT-BE-06, con apoyo de AGENT-BE-02.

### 10.3 Invocación

- En desarrollo: invocación manual (`@module-scaffolder`, `@security-reviewer`, `@traceability-clerk`).
- En CI: solo checks ligeros (build, tests, formato, secret scan, terraform validate). La revisión de seguridad matizada y la trazabilidad son manuales.
- Los sub-agentes solo enlazan skills; no copian reglas dentro de sus archivos.

### 10.4 Skill mapping

| Agente / sub-agente | Skill |
|---|---|
| AGENT-BE-01 | `skills/be-architecture.md` |
| AGENT-BE-02 | `skills/be-api-design.md` |
| AGENT-BE-03 | `skills/be-data-modeling.md` |
| AGENT-BE-04 | `skills/be-security.md` |
| AGENT-BE-05 | `skills/be-testing.md` |
| AGENT-BE-06 | `skills/be-memory.md` |
| `module-scaffolder` | `be-architecture` + `be-data-modeling` |
| `security-reviewer` | `be-security` |
| `traceability-clerk` | `be-api-design` + `be-memory` |

### 10.5 Descartado explícitamente

No se importan de la guía de referencia ni se usan en UTTT-GO:

- SQL Server, EF Core, migraciones EF, tablas legacy y prefijos `Ven*`, `Cli*`, `Emp*`, `Pro*`, `Seg*`.
- MassTransit, SQS, sagas, consumers, DLQ y eventos de bus.
- NBomber, Pact, Stryker, MutationTest, ChaosTest, ContractTest y Fuzzing.
- Argon2id, BCrypt local, lockout persistente y 2FA TOTP local (la autenticación es Supabase Auth).
- `appsettings.Example.json`, `UseInMemoryDatabase`, CloudFormation y servicios gestionados no adoptados (ECS/EKS, ALB, ElastiCache, RDS); el alcance AWS de DEC-018 se limita a Free Tier nativo: una EC2, S3, IAM, CloudWatch y Nginx/Compose en la instancia. La solución real es `.slnx`; no convertirla por una prohibición heredada.
- Prometheus/Grafana como stack obligatorio.
- Scripts de endpoints/cobertura/mutación de otro proyecto.

---

## 11. Estructura objetivo

> Estructura a materializar; `spec.md` deberá actualizarse para reflejarla.

| Ruta | Contenido |
|---|---|
| `src/UTTTGO.Api/` | Proyecto real; `Program.cs` de plantilla y Controllers |
| `src/UTTTGO.Api/Modules/{Module}/` | Objetivo futuro: Controllers, Application, Domain, Infrastructure, Contracts, Validators |
| `src/UTTTGO.Api/Shared/` | Objetivo futuro: contratos transversales, errores, primitivas |
| `tests/UtttGo.UnitTests/` | xUnit unitarias |
| `tests/UtttGo.IntegrationTests/` | xUnit + WebApplicationFactory + Testcontainers |
| `tests/UtttGo.SecurityTests/` | Seguridad y autorización |
| `infra/terraform/` | IaC AWS Free Tier de DEC-018 (EC2, EIP, SG, S3, IAM, CloudWatch, Budget) |
| `Dockerfile` / `.dockerignore` | Imagen de la API .NET 10 (multi-stage, usuario no root, sin secretos) |
| `docker-compose.yml` | Despliegue en la EC2: 2 réplicas de la API + Redis interno + Nginx; MongoDB solo en perfil `local` |
| `nginx/` | Proxy inverso: balanceo entre réplicas, WebSocket, ACME y plantilla TLS |
| `setup.sh` | Preparación de la EC2 (Ubuntu 24.04): Docker, Compose, Certbot, despliegue y TLS |
| `docs/` | `constitution.md`, `traceability.md` |
| `skills/` | Skills de agentes |
| `.github/workflows/` | CI/CD |
| `docker-compose.yml` | MongoDB y Redis locales; 2 réplicas API + Nginx en EC2 pendientes de despliegue |
| `.env.example` | Variables sin valores reales |
| `UTTTGO.Backend.slnx` | Solución .NET real, no renombrar sin decisión aprobada |

---

## 12. Definición de hecho (DoD)

Una tarea solo se marca completada si:

- [ ] Existe `spec.md` aprobado y la tarea está en `task.md`.
- [ ] El código compila en Release sin errores.
- [ ] `dotnet format` pasa.
- [ ] Tests unitarios, integración y seguridad verdes.
- [ ] Se cubrieron pruebas de media/presigned y FCM si aplica.
- [ ] No hay secretos ni valores sensibles en el diff.
- [ ] Se respetaron las reglas BR-001 a BR-013 y la degradación de Redis.
- [ ] `memoria.md` actualizado.
- [ ] Matriz de trazabilidad actualizada.
- [ ] Sin TODOs sin issue ni código muerto.

---

## 13. Lo que este archivo NO autoriza

- Escribir código de producción sin `spec.md` aprobado.
- Cambiar el stack, la nube, las reglas de negocio o la infraestructura sin actualizar `constitution.md`.
- Introducir carrito, menús, horarios, pasarelas de pago, storage alternativo o notificaciones fuera de FCM.
- Permitir URLs presignadas generadas por el cliente o imágenes sin moderación.
- Crear recursos en la nube manualmente fuera de Terraform en staging/producción.
- Comitear secretos, connection strings, tokens, service accounts o estado de Terraform.
- Deshabilitar tests, relajar rate limits en producción o alterar el orden del middleware sin aprobación.
