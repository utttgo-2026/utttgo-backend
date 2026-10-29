# UTTT-GO Backend API

API RESTful en **.NET 10 (Monolito Modular)** para la plataforma de venta de alimentos universitarios de la **Universidad Tecnológica de Tula-Tepeji (UTTT)**.

El backend expone el catálogo de productos canónicos, las ofertas comparables por vendedor, la autenticación de usuarios y los contratos base para pedidos, chat y notificaciones. El proyecto se construye bajo **Spec-Driven Development (SDD)**: la constitución, las especificaciones, los planes y las tareas son la fuente de verdad antes de escribir código de producción.

---

## Descripción

UTTT-GO conecta a estudiantes, docentes y personal de la UTTT con vendedores de alimentos dentro y alrededor del campus. El backend:

- Publica un **catálogo de productos canónicos** (un producto puede tener varias ofertas).
- Permite comparar **ofertas tipo Eneba**: precio, vendedor, punto de entrega y disponibilidad.
- Reserva stock de forma **atómica** al crear un pedido y lo libera al rechazar o cancelar.
- Autentica con **Supabase Auth** y autoriza por rol y por recurso.
- Modera las imágenes de producto con **Azure AI Content Safety**.
- Notifica cambios de pedido con **Firebase Cloud Messaging**.

Reglas de negocio del MVP:

- Sin carrito (BR-001).
- Pago en efectivo presencial (BR-003).
- Sin menús, horarios ni franjas de entrega (BR-004).
- Un producto canónico **no** contiene precio ni stock: esos datos viven en la oferta.
- Una oferta `SoldOut` no es comprable; la comparativa solo muestra ofertas disponibles.

---

## Arquitectura y Stack

| Capa / Servicio | Tecnología |
|---|---|
| Runtime | .NET 10 LTS · C# 14 · ASP.NET Core 10 |
| Arquitectura | Monolito modular con módulos `IdentityAndUsers`, `CatalogAndOffers`, `Orders`, `Chat`, `Notifications` |
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
| `phase-03-catalog-offers` | Catálogo de productos y ofertas comparables | [`spec.md`](specs/phase-03-catalog-offers/spec.md) · [`plan.md`](specs/phase-03-catalog-offers/plan.md) · [`task.md`](specs/phase-03-catalog-offers/task.md) |

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
├── .env.example                 # Plantilla de variables (sin secretos)
├── docker-compose.yml           # MongoDB, Redis y Azurite locales
├── .gitignore
└── README.md
```

El código de producción se incorporará en `src/` y las pruebas en `tests/` conforme avancen las fases (`src/UtttGo.Api`, `tests/UtttGo.UnitTests`, `tests/UtttGo.IntegrationTests`, `tests/UtttGo.SecurityTests`).

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

Copia la plantilla y completa los valores locales. **Nunca comitees `.env`.**

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

Servicios expuestos por defecto:

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
dotnet restore
dotnet run --project src/UtttGo.Api/UtttGo.Api.csproj
```

La API queda disponible en `https://localhost:5001` (o el puerto indicado por `launchSettings.json`). Verifica el estado:

```powershell
curl https://localhost:5001/health
```

### 5. Ejecutar pruebas

```powershell
dotnet test
```

Las pruebas de integración usan Testcontainers y Azurite; requieren Docker en ejecución. Para ejecutar solo una suite:

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
