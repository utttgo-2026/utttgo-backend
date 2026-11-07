# UTTT-GO Backend API

API RESTful en **.NET 10 (Monolito Modular)** para la plataforma de venta de alimentos universitarios de la **Universidad Tecnológica de Tula-Tepeji (UTTT)**.

El backend planifica un catálogo de productos independientes de cada vendedor, búsqueda y filtros para comparar alimentos de distintos vendedores, autenticación y contratos para pedidos, chat y notificaciones. El proyecto sigue **Spec-Driven Development (SDD)**: constitución, specs, planes y tareas anteceden al código. El inventario API de 42 fichas sigue pendiente de conciliación formal para los contratos afectados.

---

## Descripción

UTTT-GO conecta a estudiantes, docentes y personal de la UTTT con vendedores de alimentos dentro y alrededor del campus. El backend:

- Prevê un **catálogo de productos propios de cada vendedor**, con ingredientes, precio, existencias y disponibilidad.
- Permite buscar/filtrar opciones de distintos vendedores por datos y calificaciones disponibles; API-016 de ofertas compartidas requiere retiro/sustitución formal.
- Reserva stock de forma **atómica** al crear un pedido y lo libera al rechazar o cancelar.
- Autentica con **Supabase Auth** y autoriza por rol y por recurso.
- Modera las imágenes de producto con **Azure AI Content Safety**.
- Notifica cambios de pedido con **Firebase Cloud Messaging**.

Reglas de negocio del MVP:

- Sin carrito (BR-001).
- Pago en efectivo presencial (BR-003).
- Sin menús, horarios ni franjas de entrega (BR-004).
- Cada producto pertenece a un vendedor y contiene precio/stock; el comprador no fija esos importes al pedir.
- Productos pausados, agotados o en categorías inactivas no son comprables; la eliminación de producto necesita ficha API nueva.
- Al eliminar cuenta se elimina información propia; chat temporal se purga dentro de tres días de completarse el pedido; recuperación por enlace al correo registrado.

---

## Arquitectura y Stack

| Capa / Servicio | Tecnología |
|---|---|
| Runtime | .NET 10 LTS · C# 14 · ASP.NET Core 10 |
| Arquitectura | Monolito modular con módulos previstos `IdentityAndUsers`, `Catalog`, `Orders`, `Chat`, `Notifications` |
| Base de datos | MongoDB Atlas 7.x (driver oficial) |
| Autenticación | Supabase Auth (JWT) + roles y revocación en Redis |
| Caché | Azure Cache for Redis 7 (`cache:catalog:*`) |
| Almacenamiento de imágenes | Azure Blob Storage con SAS URLs de corta duración |
| Moderación de contenido | Azure AI Content Safety |
| Notificaciones push | Firebase Cloud Messaging (FCM) |
| Tiempo real | SignalR con backplane de Redis |
| Infraestructura | Azure App Service + Load Balancer · Terraform |
| Pruebas | xUnit · Testcontainers (MongoDB/Redis) · Azurite |

Las decisiones de arquitectura y las reglas transversales están en [`docs/constitution.md`](docs/constitution.md). El manual operativo de agentes y convenciones está en [`AGENTS.md`](AGENTS.md).

### Principios clave

1. **Contratos primero:** OpenAPI/HTTP documentados en cada spec antes de implementar.
2. **Aislamiento por módulo:** cada módulo expone su API interna; no hay acceso directo a colecciones de otro módulo.
3. **Seguridad por defecto:** fail-closed en autenticación, rate limiting distribuido, secretos fuera del repositorio.
4. **Precio como `decimal`/`Decimal128`:** nunca `float` ni `double`.
5. **Trazabilidad:** todo requisito (`RF-xx`) mapea a endpoint, tarea y prueba.

---

## Metodología: Spec-Driven Development (SDD)

Cada fase funcional sigue el mismo pipeline, en este orden y sin saltos:

```
docs/constitution.md
        ↓
specs/phase-XX-<modulo>/spec.md      → qué y por qué (RF, RNF, casos límite, contratos)
        ↓
specs/phase-XX-<modulo>/plan.md      → cómo (estructura, modelos, servicios, endpoints, pruebas)
        ↓
specs/phase-XX-<modulo>/task.md      → tareas atómicas de 20–30 min con "Hecho cuando"
        ↓
implementación + pruebas
```

### Especificaciones por fase

| Fase | Módulo | Artefactos |
|---|---|---|
| `phase-00-constitution` | Principios, stack y reglas transversales | [`spec.md`](specs/phase-00-constitution/00-01-principles-stack/spec.md) |
| `phase-02-auth-users` | Autenticación, usuarios y perfiles de vendedor | [`spec.md`](specs/phase-02-auth-users/spec.md) · [`plan.md`](specs/phase-02-auth-users/plan.md) · [`task.md`](specs/phase-02-auth-users/task.md) |
| `phase-03-catalog-offers` (nombre histórico) | Productos independientes por vendedor; API-016 pendiente de retiro/sustitución formal | [`spec.md`](specs/phase-03-catalog-offers/spec.md) · [`plan.md`](specs/phase-03-catalog-offers/plan.md) · [`task.md`](specs/phase-03-catalog-offers/task.md) |

El estado de decisiones, pendientes y mapa de colecciones se mantiene en [`memoria.md`](memoria.md).

---

## Estructura del repositorio

```text
.
├── docs/
│   └── constitution.md          # Fuente de verdad: stack, reglas de negocio, seguridad
├── specs/
│   ├── phase-00-constitution/
│   ├── phase-02-auth-users/
│   └── phase-03-catalog-offers/
├── skills/                      # Guías operativas reutilizables
├── AGENTS.md                    # Manual de agentes, roles y comandos
├── memoria.md                   # Decisiones, historial y pendientes
├── UTTTGO.Backend.slnx          # Solución real (.NET 10)
├── src/UTTTGO.Api/              # Proyecto real, Controllers y plantilla de API
├── material/                    # PDF académico, historias y 42 fichas originales
├── .gitignore
└── README.md
```

Las 42 fichas del Excel no se modifican sin aprobación. `docs/academic-requirements-phase-02.md` coteja el PDF oficial con la fase 02; `docs/traceability.md` vincula sus siete rutas prioritarias a HU/UI/PB. `src/UTTTGO.Api` incluye API-001/002 locales candidatas y `tests/UtttGo.UnitTests/`, `tests/UtttGo.IntegrationTests/` y `tests/UtttGo.SecurityTests/` las verifican **solo con dobles**, sin servicios externos (`docs/qa-api-001.md`, `docs/qa-api-002.md`). `.env.example`, `docker-compose.yml`, `infra/terraform` y `.github/workflows/` **aún no están creados**; las instrucciones para ellos siguen siendo previsiones.

**Gate académico documental:** el PDF exige SDD, pruebas, CI/CD, dos instancias en nube y costo cero; recomienda Terraform y sugiere herramientas concretas de IA, pero Terraform es **obligatorio por la constitución**, no por el PDF. La matriz académica registra la evaluación **pendiente** de viabilidad simultánea de dos instancias y servicios constitucionales a costo cero: sin cuotas/presupuesto comprobados, no crear infraestructura ni declarar cumplido el requisito. TASK-AUTH-001/006 están cerradas **solo documentalmente**; ni acreditan despliegue ni cierran el riesgo de costo cero.

---

## Requisitos previos

- [.NET SDK 10](https://dotnet.microsoft.com/) (LTS)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) con Docker Compose
- [Git](https://git-scm.com/)
- Opcional: Terraform 1.9+, Azure CLI y una cuenta de MongoDB Atlas para entornos remotos

Verifica la instalación:

```powershell
dotnet --version
docker --version
git --version
```

---

## Configuración local

### 1. Clonar el repositorio

```powershell
git clone https://github.com/utttgo-2026/utttgo-backend.git
cd utttgo-backend
```

### 2. Crear el archivo de entorno

Copia la plantilla **cuando se cree** y completa los valores locales. **Nunca comitees `.env`.**

```powershell
Copy-Item .env.example .env
```

```bash
# macOS / Linux
cp .env.example .env
```

Variables esperadas (los valores reales se solicitan al equipo o se obtienen de Azure Key Vault / Supabase):

```dotenv
ASPNETCORE_ENVIRONMENT=Development
MONGODB_CONNECTION_STRING=mongodb://localhost:27017
MONGODB_DATABASE=utttgo
REDIS_CONNECTION_STRING=localhost:6379
SUPABASE_URL=https://<proyecto>.supabase.co
SUPABASE_JWT_SECRET=<desde-el-gestor-de-secretos>
AZURE_STORAGE_CONNECTION_STRING=UseDevelopmentStorage=true
AZURE_CONTENT_SAFETY_ENDPOINT=
AZURE_CONTENT_SAFETY_KEY=
FIREBASE_PROJECT_ID=
```

> Si `.env.example` o `docker-compose.yml` aún no están presentes en tu copia, se incorporan con el esqueleto de la Fase 0/1. No crees archivos `.env` versionados ni subas credenciales.

### 3. Levantar dependencias locales

```powershell
docker compose up -d
```

Servicios previstos cuando se incorpore `docker-compose.yml` (no activos todavía):

| Servicio | Puerto | Uso |
|---|---|---|
| MongoDB | `27017` | Base de datos local |
| Redis | `6379` | Caché, rate limiting y revocación de JWT |
| Azurite (Blob) | `10000` | Emulador de Azure Blob Storage |

Para detenerlos:

```powershell
docker compose down
```

### 4. Restaurar y ejecutar la API

```powershell
dotnet restore UTTTGO.Backend.slnx
dotnet run --project src/UTTTGO.Api/UTTTGO.Api.csproj
```

La URL/puerto depende de `launchSettings.json`. **Cuando se implemente** la sonda operativa, verifica `GET /health` (la plantilla aún no la expone):

```powershell
curl https://localhost:5001/health
```

### 5. Ejecutar pruebas

```powershell
dotnet test
```

Las pruebas de integración **previstas** usarán Testcontainers y Azurite; requieren Docker. Aún no existen proyectos de pruebas; comandos previstos para cuando se creen:

```powershell
dotnet test tests/UtttGo.UnitTests/UtttGo.UnitTests.csproj
dotnet test tests/UtttGo.IntegrationTests/UtttGo.IntegrationTests.csproj
dotnet test tests/UtttGo.SecurityTests/UtttGo.SecurityTests.csproj
```

---

## Comandos útiles

```powershell
# Compilar en Release
dotnet build -c Release

# Verificar formato
dotnet format --verify-no-changes

# Aplicar formato
dotnet format

# Dependencias locales
docker compose up -d
docker compose ps
docker compose logs -f mongodb

# Limpiar artefactos
dotnet clean
```

---

## Seguridad

- Los secretos se gestionan con variables de entorno locales, Azure Key Vault o el gestor de secretos del equipo; **no** se versionan.
- `.env`, certificados, credenciales de Firebase/Azure y estados de Terraform están excluidos por `.gitignore`.
- Los JWT se validan contra Supabase y se revocan por `jti` en Redis.
- Las SAS URLs de imágenes son de corta duración y se generan solo en el backend.
- Toda imagen de producto pasa por Azure AI Content Safety antes de publicarse.
- El rate limiting del catálogo público es distribuido y fail-closed.
- No se almacenan datos financieros: el pago es en efectivo presencial (BR-003).

Si detectas un secreto expuesto, repórtalo de inmediato y rótalo; no abras un issue público con el valor.

---

## Contribución

1. Trabaja sobre una rama descriptiva (`feat/...`, `fix/...`, `docs/...`).
2. Respeta el pipeline SDD: actualiza `spec.md` → `plan.md` → `task.md` antes del código.
3. Marca las tareas de `task.md` solo cuando su "Hecho cuando" se cumpla y las pruebas estén en verde.
4. Ejecuta `dotnet build -c Release`, `dotnet format --verify-no-changes` y `dotnet test` antes del PR.
5. Registra decisiones y pendientes en `memoria.md`.

---

## Licencia

Proyecto académico de la Universidad Tecnológica de Tula-Tepeji (UTTT). Licencia por definir por el equipo.
