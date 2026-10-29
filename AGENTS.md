# AGENTS.md — Backend UTTT-GO
> Manual operativo de agentes y desarrolladores del backend.
> Versión 2.0 · 2026-10-29
> Alineado con `constitution.md` v2.0. Si hay conflicto, gana `constitution.md`.
> Flujo SDD obligatorio: `constitution.md` → `spec.md` → `plan.md` → `task.md` → implementación.

---

## 1. Forma de trabajo

### 1.1 Stack obligatorio

> Fuente canónica: `constitution.md` § 2 y § 3. No introducir tecnologías fuera del stack sin una nueva versión de la constitución.

- **Runtime:** .NET 10 LTS, C# 14, ASP.NET Core 10.
- **Arquitectura:** monolito modular con fronteras por módulo.
- **Nube:** Microsoft Azure. Cómputo en **Azure App Service** (DEC-007 resuelta a favor de App Service).
- **Balanceo:** Azure Load Balancer / Application Gateway. La API es stateless.
- **IaC:** Terraform para toda la infraestructura Azure.
- **Datos:** MongoDB Atlas con `MongoDB.Driver`.
- **Auth:** Supabase Auth; roles en el perfil local.
- **Redis:** Azure Cache for Redis para caché, rate limiting distribuido y blacklist JWT.
- **Storage:** Azure Blob Storage con URLs SAS generadas exclusivamente por el backend.
- **Moderación:** Azure AI Content Safety API antes de publicar imágenes.
- **Push:** Firebase Cloud Messaging con FCM Admin SDK, únicamente desde el backend.
- **Tiempo real:** SignalR con backplane obligatorio.
- **API:** prefijo `/api/v1`.

### 1.2 Setup local

Los comandos asumen la estructura objetivo de § 11. Mientras el esqueleto no exista, las tareas de la FASE 0/1 de `plan.md` son las que crean estos archivos.

1. Copiar variables de entorno:
   ```powershell
   Copy-Item .env.example .env
   ```
   Completar valores locales ficticios. `.env` está en `.gitignore` y nunca se comitea.
2. Levantar dependencias locales:
   ```powershell
   docker compose up -d
   ```
   Incluye MongoDB 7 (espejo local de Atlas), Redis 7 y Azurite para Blob Storage. No apuntar pruebas locales a Atlas, Redis ni Blob de producción.
3. Restaurar y ejecutar:
   ```powershell
   dotnet restore utttgo-backend.sln
   dotnet run --project src/UtttGo.Api/UtttGo.Api.csproj
   ```
4. Verificar `GET /health`.

Para `dotnet run`, exportar las mismas variables del `.env` o usar secretos locales de desarrollo (`dotnet user-secrets`), nunca comiteados. En Azure, los valores provienen de **Azure Key Vault** y variables de entorno del App Service.

### 1.3 Comandos canónicos

| Objetivo | Comando |
|---|---|
| Restaurar | `dotnet restore utttgo-backend.sln` |
| Compilar Release | `dotnet build utttgo-backend.sln -c Release --no-restore` |
| Formato | `dotnet format utttgo-backend.sln --verify-no-changes --no-restore` |
| Unitarias | `dotnet test tests/UtttGo.UnitTests/UtttGo.UnitTests.csproj -c Release --no-build` |
| Integración | `dotnet test tests/UtttGo.IntegrationTests/UtttGo.IntegrationTests.csproj -c Release --no-build` |
| Seguridad | `dotnet test tests/UtttGo.SecurityTests/UtttGo.SecurityTests.csproj -c Release --no-build` |
| Ejecutar API | `dotnet run --project src/UtttGo.Api/UtttGo.Api.csproj` |
| Docker local | `docker compose up -d` / `docker compose down` |
| Terraform formato | `terraform -chdir=infra/terraform fmt -check -recursive` |
| Terraform validación | `terraform -chdir=infra/terraform init -backend=false` + `terraform -chdir=infra/terraform validate` |
| Terraform plan | `terraform -chdir=infra/terraform plan -var-file=envs/dev.tfvars` |

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
| BR-002 | Cada pedido corresponde a **una única oferta/producto** con su cantidad. |
| BR-003 | El **único método de pago es efectivo presencial** en el campus UTTT. No hay pasarela ni datos financieros. |
| BR-004 | **No existen menús ni horarios.** La disponibilidad depende solo de stock/estado de la oferta. |
| BR-005 | La propuesta de valor central son las **opciones comparativas tipo Eneba** para un mismo producto. |
| BR-006 | Se distingue **Producto canónico** (qué se vende) de **Oferta** (quién lo vende, precio y condiciones). |
| BR-007 | Toda imagen usa **SAS pre-firmada por el backend** y **moderación previa** con Azure AI Content Safety. |
| BR-008 | Las notificaciones push se envían **solo** por el backend con FCM Admin SDK. |
| BR-009 | Precio, total, estado, `userId`, `sellerId` y roles **nunca** se confían al cliente. |
| BR-010 | El total se calcula server-side como `price × quantity` de la oferta seleccionada. |

### 3.2 Stack y datos

- No introducir SQL Server, EF Core, migraciones EF, MassTransit, SQS, sagas, DLQ ni bases relacionales.
- MongoDB no usa migraciones: los índices y validaciones se declaran en el `spec.md` de cada módulo.
- Redis es obligatorio para blacklist JWT, caché y rate limiting distribuido.
- Las imágenes no se guardan en MongoDB ni en disco local; solo en Azure Blob Storage.
- Terraform es la única vía para crear/modificar infraestructura Azure de staging/producción.
- Prohibido el estado de Terraform (`*.tfstate`, `*.tfstate.backup`, `*.tfvars` con secretos) en Git.

### 3.3 Seguridad

- El logout revoca el JWT agregando su `jti` a la blacklist de Redis: `revoked:{jti}` con TTL hasta `exp`.
- Si Redis no responde, la blacklist y el rate limit de `/auth/*` son **fail-closed**.
- Las SAS solo las genera el backend, con alcance de un blob, TTL corto y permisos mínimos.
- Ningún contenedor de Blob es público.
- Sin moderación aprobada no hay publicación de imagen: **fail-closed**.
- El cliente nunca conoce el service account de FCM ni envía push directamente.
- Secretos solo en Azure Key Vault o variables de entorno. Nunca en código, `appsettings`, `.env` comiteado, Terraform con valores reales ni logs.
- No tocar el orden del pipeline de middleware de `Program.cs` sin aprobación de AGENT-BE-01 y actualización de `spec.md`.
- No relajar rate limits en producción; solo configuración explícita no-productiva.

---

## 4. Verificación y calidad

Antes de pedir revisión de un cambio:

- [ ] `dotnet build -c Release --no-restore` con 0 errores.
- [ ] `dotnet format --verify-no-changes` sin diferencias.
- [ ] Unitarias, integración y seguridad en verde.
- [ ] Pruebas de media/SAS y notificaciones FCM actualizadas si el cambio las impacta.
- [ ] `terraform fmt -check -recursive` y `terraform validate` en verde si cambió `infra/terraform`.
- [ ] Sin secretos en el diff; secret scanning del CI en verde.
- [ ] Sin endpoints nuevos fuera de `spec.md` y sin contratos sin ficha.

**NFR:** no hay umbrales de rendimiento o cobertura aprobados todavía. No inventarlos. Cuando se definan en un `spec.md` de NFR, cada umbral debe ser verificable por comando o test y este archivo se actualiza.

**Configuración:** la tabla de variables vive en `spec.md` § 2 y `.env.example`. `appsettings.*.json` solo documenta claves con código real; secretos fuera de Git.

---

## 5. Convenciones técnicas

### 5.1 Módulos y API

- Módulos previstos: `Identity`, `Users`, `Sellers`, `Catalog`, `Offers`, `Orders`, `Reviews`, `Favorites`, `Notifications`, `Chat`, `Media`, `Admin`.
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

### 5.4 Imágenes: SAS + moderación

Flujo obligatorio:

1. El cliente pide intención de subida al backend.
2. El backend valida JWT, MIME, extensión y tamaño; crea `mediaAssets` en `PendingUpload` y devuelve una SAS de escritura de un solo blob.
3. El cliente sube directo a un contenedor de staging.
4. El cliente notifica fin; el backend invoca Azure AI Content Safety.
5. Seguro → `Approved`; no seguro → se elimina el blob y queda `Rejected`.
6. Solo un `mediaAssetId` en `Approved` puede asociarse a producto/oferta.

- SAS de lectura para imágenes aprobadas: corta duración o CDN controlado.
- Nunca `AZURE_STORAGE_CONNECTION_STRING` ni account key al cliente.
- Error de Content Safety = no publicación (fail-closed).

### 5.5 FCM

- Envío exclusivo desde el backend con FCM Admin SDK.
- `deviceTokens` por usuario; tokens inválidos se eliminan tras el error de FCM.
- El service account vive en Key Vault; nunca en Git.
- Envíos idempotentes y derivados de eventos de dominio (estado de pedido, chat).

### 5.6 SignalR

- Grupos derivados de identidades autorizadas; nunca de IDs sin validar.
- Backplane obligatorio con múltiples instancias detrás del balanceador.
- Estado de reconexión en Redis/MongoDB, no en memoria del proceso.

### 5.7 Pedidos

- Un pedido = una oferta (BR-001, BR-002).
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
- SAS: alcance mínimo, un blob, TTL ≤ 5 minutos, content-type y tamaño restringidos.
- Moderación previa obligatoria; sin excepciones por entorno.
- Terraform sin secretos en código; valores sensibles desde Key Vault.
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
- Azurite para pruebas de Blob/SAS en local.
- Content Safety y FCM se mockean en unitarias/integración; nunca se llaman servicios reales en CI.
- Proyectos: `UtttGo.UnitTests`, `UtttGo.IntegrationTests`, `UtttGo.SecurityTests`.
- Casos mínimos:
  - Unitarias: servicios, validadores, cálculo de total, reglas de oferta/pedido.
  - Integración: endpoints contra MongoDB/Redis en contenedores; flujo SAS con Azurite.
  - Seguridad: token inválido/expirado/revocado, rol incorrecto, recurso ajeno, rate limit, blacklist.
  - Media/IA: SAS solo backend, alcance mínimo, moderación antes de publicar, rechazo de contenido no seguro.
  - Notificaciones: FCM mockeado, depuración de tokens, idempotencia.
  - Aceptación: Given/When/Then derivados del `spec.md`.
- No se usan Pact, NBomber, Stryker ni ChaosTest en esta fase.
- Prohibido ejecutar pruebas contra entornos productivos.
- `SecurityTests` puede reutilizar helpers de `UnitTests/Common` (`TokenHelper`, `TestDataFactory`).

---

## 8. Observabilidad

- Serilog con consola JSON en desarrollo y sink de Azure Monitor/Application Insights en Azure (cuando se habilite).
- `correlationId` en cada evento de log del ciclo de vida.
- `GET /health` verifica MongoDB y Redis; devuelve `503` si una dependencia crítica falla.
- Logs de moderación y FCM con resultado y `correlationId`, sin payloads sensibles.
- No se adopta Prometheus/Grafana en esta fase.
- No loggear tokens, SAS completas, `jti` ni service accounts.

---

## 9. Despliegue y operación

- **Azure App Service** detrás de Load Balancer/Application Gateway con health probes.
- API stateless; estado efímero en Redis.
- SignalR con backplane para múltiples instancias.
- Terraform en `infra/terraform` con:
  - Backend remoto de estado y bloqueo.
  - Entornos `dev`, `staging`, `prod`.
  - App Service, load balancer, Key Vault, Blob Storage, Azure Cache for Redis, Content Safety, networking, diagnósticos y alarmas.
- MongoDB Atlas se aprovisiona por su mecanismo documentado; connection strings en Key Vault.
- FCM service account en Key Vault.
- CI/CD con GitHub Actions y OIDC hacia Azure; sin credenciales estáticas.
- Prohibido crear o modificar recursos Azure a mano en staging/producción.
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
| AGENT-BE-04 | Security Reviewer | JWT, autorización por recurso, rate limit, SAS, Content Safety y secretos |
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
- **Verifica:** JWT + blacklist Redis, autorización por recurso, rate limit, SAS (alcance/TTL), Content Safety, Blob no público, FCM server-only, secretos, CORS, logs y contrato de error.
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
- `.slnx`, `appsettings.Example.json`, `UseInMemoryDatabase`, AWS, CloudFormation, ALB/EC2/RDS.
- Prometheus/Grafana como stack obligatorio.
- Scripts de endpoints/cobertura/mutación de otro proyecto.

---

## 11. Estructura objetivo

> Estructura a materializar; `spec.md` deberá actualizarse para reflejarla.

| Ruta | Contenido |
|---|---|
| `src/UtttGo.Api/` | Entry point `Program.cs`, configuración y middleware |
| `src/UtttGo.Api/Modules/{Module}/` | Endpoints, Application, Domain, Infrastructure, Contracts, Validators |
| `src/UtttGo.Api/Shared/` | Contratos transversales, errores, primitivas |
| `tests/UtttGo.UnitTests/` | xUnit unitarias |
| `tests/UtttGo.IntegrationTests/` | xUnit + WebApplicationFactory + Testcontainers |
| `tests/UtttGo.SecurityTests/` | Seguridad y autorización |
| `infra/terraform/` | IaC Azure, entornos y backend remoto |
| `docs/` | `constitution.md`, `traceability.md` |
| `skills/` | Skills de agentes |
| `.github/workflows/` | CI/CD |
| `docker-compose.yml` | MongoDB, Redis y Azurite locales |
| `.env.example` | Variables sin valores reales |
| `utttgo-backend.sln` | Solución .NET (formato `.sln`, no `.slnx`) |

---

## 12. Definición de hecho (DoD)

Una tarea solo se marca completada si:

- [ ] Existe `spec.md` aprobado y la tarea está en `task.md`.
- [ ] El código compila en Release sin errores.
- [ ] `dotnet format` pasa.
- [ ] Tests unitarios, integración y seguridad verdes.
- [ ] Se cubrieron pruebas de media/SAS y FCM si aplica.
- [ ] No hay secretos ni valores sensibles en el diff.
- [ ] Se respetaron las reglas BR-001 a BR-010 y la degradación de Redis.
- [ ] `memoria.md` actualizado.
- [ ] Matriz de trazabilidad actualizada.
- [ ] Sin TODOs sin issue ni código muerto.

---

## 13. Lo que este archivo NO autoriza

- Escribir código de producción sin `spec.md` aprobado.
- Cambiar el stack, la nube, las reglas de negocio o la infraestructura sin actualizar `constitution.md`.
- Introducir carrito, menús, horarios, pasarelas de pago, storage alternativo o notificaciones fuera de FCM.
- Permitir SAS generadas por el cliente o imágenes sin moderación.
- Crear recursos Azure manualmente fuera de Terraform en staging/producción.
- Comitear secretos, connection strings, tokens, service accounts o estado de Terraform.
- Deshabilitar tests, relajar rate limits en producción o alterar el orden del middleware sin aprobación.
