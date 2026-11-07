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
| Caché | Redis 7 (contenedor local y en la EC2; sin ElastiCache por costo $0, DEC-018) |
| Almacenamiento de imágenes | Amazon S3 bucket privado con URLs presignadas del backend |
| Moderación de contenido | Azure AI Content Safety (servicio externo conservado; DEC-018) |
| Notificaciones push | Firebase Cloud Messaging (FCM) |
| Tiempo real | SignalR con backplane de Redis |
| Infraestructura | AWS: EC2 t3.micro + Elastic IP + Nginx (2 réplicas Docker Compose) · Terraform |
| Pruebas | xUnit · Testcontainers (MongoDB/Redis) · dobles o emulación S3 |

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

Las 42 fichas del Excel no se modifican sin aprobación. `docs/academic-requirements-phase-02.md` coteja el PDF oficial con la fase 02; `docs/traceability.md` vincula sus siete rutas prioritarias a HU/UI/PB. `src/UTTTGO.Api` incluye API-001/002 locales candidatas y `tests/UtttGo.UnitTests/`, `tests/UtttGo.IntegrationTests/` y `tests/UtttGo.SecurityTests/` las verifican **solo con dobles**, sin servicios externos (`docs/qa-api-001.md`, `docs/qa-api-002.md`). `infra/terraform` **ya existe** con el stack AWS de DEC-018 (EC2, S3, IAM, CloudWatch, Budget) validado y **aplicado por el propietario** (Elastic IP `3.129.142.54`); falta la evidencia de costo $0 (alerta AWS Budget). `docker-compose.yml`, `Dockerfile`, `nginx/` y `setup.sh` materializan el despliegue en la EC2 (2 réplicas de la API + Redis + Nginx) y están validados localmente, sin ejecución en la instancia todavía.

**Gate académico documental:** el PDF exige SDD, pruebas, CI/CD, dos instancias en nube y costo cero; recomienda Terraform y sugiere herramientas concretas de IA, pero Terraform es **obligatorio por la constitución**, no por el PDF. El requisito estructural se resolvió con **DEC-018** (constitución v2.4): nube única AWS, una EC2 t3.micro ($0 en Free Tier) con **2 réplicas Docker Compose detrás de Nginx** como las «dos instancias». La evidencia final de costo $0 queda pendiente de la alerta AWS Budget tras el primer apply; ni el cambio de nube ni esta documentación acreditan despliegue.

---

## Requisitos previos

- [.NET SDK 10](https://dotnet.microsoft.com/) (LTS)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) con Docker Compose
- [Git](https://git-scm.com/)
- Opcional: Terraform 1.9+, AWS CLI (o solo variables de entorno), cuenta MongoDB Atlas y Free Tier de AWS para entornos remotos

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

Variables esperadas (los valores reales se solicitan al equipo o se obtienen de variables de entorno seguras / Supabase):

```dotenv
MONGODB_URI=mongodb://mongo:27017
MONGODB_DATABASE=utttgo
REDIS_URL=redis://redis:6379
SUPABASE_URL=https://<proyecto>.supabase.co
SUPABASE_ANON_KEY=
SUPABASE_JWKS_URL=
SUPABASE_JWT_SECRET=<desde-el-gestor-de-secretos>
SUPABASE_JWT_ISSUER=
SUPABASE_JWT_AUDIENCE=
AZURE_CONTENT_SAFETY_ENDPOINT=
AZURE_CONTENT_SAFETY_KEY=
S3_BUCKET_IMAGES=utttgo-images-local
AWS_REGION=us-east-2
FIREBASE_PROJECT_ID=
```

> `.env.example` y `docker-compose.yml` ya existen en el repositorio. No crees archivos `.env` versionados ni subas credenciales.

### 3. Levantar el stack local

```powershell
docker compose up -d
```

| Servicio | Puerto | Uso |
|---|---|---|
| `api` (2 réplicas) | interno `8080` | API .NET 10 detrás de Nginx (estado stateless) |
| `nginx` | `80`, `443` | Proxy inverso y balanceo entre las 2 réplicas |
| `redis` | interno `6379` | Caché, rate limiting y revocación de JWT |
| `mongo` (perfil `local`) | `27017` | Espejo local de MongoDB Atlas |

MongoDB solo arranca con el perfil `local` (en la EC2 se usa Atlas):

```powershell
docker compose --profile local up -d
```

Para detener todo:

```powershell
docker compose --profile local down
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

Los proyectos de pruebas existentes (`UtttGo.UnitTests`, `UtttGo.IntegrationTests`, `UtttGo.SecurityTests`) corren **con dobles**; la integración con Testcontainers y con emulación de S3 queda prevista y requiere Docker:

```powershell
dotnet test tests/UtttGo.UnitTests/UtttGo.UnitTests.csproj
dotnet test tests/UtttGo.IntegrationTests/UtttGo.IntegrationTests.csproj
dotnet test tests/UtttGo.SecurityTests/UtttGo.SecurityTests.csproj
```

---

## Despliegue en la EC2 (AWS)

La instancia (EC2 t3.micro, Elastic IP `3.129.142.54`, Ubuntu 24.04) ejecuta el stack con Docker Compose: **2 réplicas de la API** detrás de **Nginx** (balanceo local) y **Redis 7** solo en la red interna. `setup.sh` instala Docker, Docker Compose y Certbot, despliega el stack y emite el TLS:

```bash
# En la instancia, con el repositorio clonado
sudo bash setup.sh --domain api.tudominio.mx --email tu@correo.com

# Sin TLS (solo HTTP por la IP)
sudo bash setup.sh --skip-tls
```

Requisitos: el dominio debe apuntar con un registro A a `3.129.142.54` (Let's Encrypt no emite certificados para IP desnudas) y el `.env` de la instancia debe tener los valores reales de Atlas, Supabase y S3 (el script lo crea desde `.env.example` si no existe).

```bash
docker compose ps
docker compose logs -f api
docker compose up -d --scale api=2
```

Notas operativas:

- El script es idempotente: reutiliza el Docker que ya instala el `user_data` de Terraform.
- El certificado vive en `/etc/letsencrypt` del host y Nginx lo monta en solo lectura; la renovación usa `certbot` con un hook que recarga Nginx.
- Redis no publica puertos: solo es alcanzable desde la red interna de Compose.
- El backend toma las credenciales de AWS por el Instance Profile (IMDSv2); por eso Terraform fija `http_put_response_hop_limit = 2` para que los contenedores alcancen IMDS.
- `app.UseHttpsRedirection()` no redirige dentro del contenedor porque no hay puerto HTTPS configurado: el TLS termina en Nginx. Interpretar `X-Forwarded-Proto` en el backend exige un cambio de middleware aprobado por AGENT-BE-01.

### CD automático (GitHub Actions)

`.github/workflows/cd-deploy.yml` despliega en la EC2 **solo cuando CI-1 (`ci-1.yml`) termina en verde en un push a `main`** (`workflow_run`, con verificación de resultado, evento y rama). Descarga por SSH el SHA exacto verificado, ejecuta `docker compose up -d --build --scale api=2`, espera que las réplicas queden sanas, hace smoke HTTP y publica la evidencia del despliegue. Un despliegue manual (`workflow_dispatch`) exige indicar el motivo.

Secretos que hay que configurar en **Settings → Secrets and variables → Actions**:

| Secreto | Valor |
|---|---|
| `EC2_SSH_KEY` | Clave privada completa del usuario de despliegue (con permisos de lectura en el repositorio y acceso a Docker en la instancia) |
| `EC2_HOST` | `3.129.142.54` |
| `EC2_USER` | `ubuntu` (o el usuario de despliegue creado en la instancia) |
| `EC2_KNOWN_HOSTS` | Recomendado: línea `known_hosts` de la instancia (evita aceptar la clave del host sin verificar) |

Variables opcionales: `DEPLOY_PATH` (`/opt/utttgo`) y `EC2_PORT` (`22`). Además, crea el environment `production` con revisores requeridos y registra la clave pública de despliegue como **deploy key de solo lectura** del repositorio en la instancia, para que `git fetch` funcione sin credenciales personales.

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
docker compose logs -f api

# Limpiar artefactos
dotnet clean
```

---

## Seguridad

- Los secretos se gestionan con variables de entorno locales (incluidas `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY` de Terraform) o el gestor del equipo; **no** se versionan.
- `.env`, certificados, credenciales de Firebase/AWS/Azure y estados de Terraform están excluidos por `.gitignore`.
- Los JWT se validan contra Supabase y se revocan por `jti` en Redis.
- Las URLs presignadas de S3 son de corta duración, de un solo objeto y se generan solo en el backend; el bucket es privado (Block Public Access).
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
