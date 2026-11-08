# spec.md — Infraestructura AWS Free Tier · Backend UTTT-GO

> Versión documental **1.1** · 2026-11-06 · Estado: **aprobada** por el propietario de producto (DEC-018, constitución v2.4). `infra/terraform/` verificada con `terraform fmt -check -recursive`, `terraform init -backend=false` y `terraform validate`, y **aplicada por el propietario** (Elastic IP `3.129.142.54`, 2026-11-06). El despliegue en la EC2 (Compose + Nginx + Redis + TLS) está materializado en la raíz del repositorio y validado localmente; su ejecución en la instancia y la evidencia de costo $0 quedan pendientes.
> Autoridad: `docs/constitution.md` v2.4 (DEC-018) → `docs/referencias-profesor-backend.md` → este `spec.md` → `plan.md` → `task.md`.

## 1. Objetivo y alcance

Declarar en IaC (Terraform) la infraestructura mínima del backend UTTT-GO en Amazon AWS, región `us-east-2`, compatible con el objetivo de costo $0 del PDF académico (punto 15) y con la arquitectura aprobada en DEC-018: **una EC2 t3.micro con Elastic IP** que hospeda **2 réplicas Docker Compose de la API detrás de Nginx**, **Redis 7 como contenedor** (no ElastiCache) y **S3 privado** para imágenes con URLs presignadas generadas por el backend.

Alcance de esta spec: únicamente los recursos AWS declarativos y su verificación estática. **No** incluye el `docker-compose.yml` de producción, Nginx, TLS/certificados, el código de presigned del backend, los workflows de CD ni la promoción entre entornos.

## 2. Requisitos

| ID | Requisito | Verificación |
|---|---|---|
| INFRA-01 | Región `us-east-2` y cómputo en una única instancia `t3.micro` Ubuntu 24.04 LTS (AMI Canonical `099720109477`) | `variables.tf` (`aws_region`, `instance_type`), `data.aws_ami` |
| INFRA-02 | Elastic IP fija y SG con ingress 22/80/443 y egress libre | `aws_eip.api`, `aws_security_group.api` |
| INFRA-03 | IMDSv2 obligatorio, volumen raíz gp3 ≤ 30 GB cifrado, sin secretos en la instancia | `metadata_options.http_tokens = "required"`, `root_block_device` |
| INFRA-04 | IAM por Instance Profile con alcance mínimo: logs de CloudWatch y (opcional) SSM; sin access keys para el backend | `aws_iam_role.ec2`, `aws_iam_role_policy.cloudwatch_logs`, `aws_iam_role_policy_attachment.ssm` |
| INFRA-05 | Bucket S3 **privado** (Block Public Access completo), SSE AES256, CORS para presigned PUT/GET/HEAD, retención con `force_destroy` controlable | `aws_s3_bucket.images` + `public_access_block` + `encryption_configuration` + `cors_configuration` |
| INFRA-06 | Redis sin ElastiCache (contenedor Compose en la EC2) | No se declara `aws_elasticache_cluster` en el código |
| INFRA-07 | Evidencia de costo $0: alerta AWS Budget configurable por correo con límite mensual | `aws_budgets_budget.zero_cost_guard` |
| INFRA-08 | Estado local fuera de Git; backend remoto (S3 + DynamoDB) como refactor obligatorio antes de producción | `terraform` sin `backend "s3"`; `.gitignore` excluye `*.tfstate*` y `.terraform/` |
| INFRA-09 | Credenciales solo por variables de entorno o `aws configure`; ningún valor sensible en variables, código o `*.tfvars` comiteado | `variables.tf` sin variables de credencial; `terraform.tfvars.example` solo con valores no sensibles |
| INFRA-10 | Verificación estática obligatoria: `terraform fmt -check -recursive`, `terraform init -backend=false`, `terraform validate` | `AGENTS.md` § 1.3 y § 12; comandos ejecutados en verde |
| INFRA-11 | El despliegue dentro de la EC2 se define con `docker-compose.yml`: API en **2 réplicas** (`deploy.replicas: 2`), Redis 7 **sin puertos publicados** y Nginx como proxy inverso que balancea entre réplicas | `docker-compose.yml`; `docker compose config` y prueba de balanceo con `ua="$upstream_addr"` |
| INFRA-12 | TLS con Let's Encrypt: Certbot en la instancia, desafío ACME por `webroot` compartido con Nginx y renovación automática con hook de recarga | `setup.sh`, `nginx/conf.d/tls.conf.template`, hook en `/etc/letsencrypt/renewal-hooks/deploy/` |
| INFRA-13 | La imagen de la API se construye desde el repositorio (multi-stage .NET 10), corre como usuario no root y sin secretos incrustados | `Dockerfile`, `.dockerignore` |
| INFRA-14 | Los contenedores alcanzan las credenciales de AWS por el Instance Profile (IMDSv2), sin claves en la imagen ni en variables de Compose | `http_put_response_hop_limit = 2` en `aws_instance.api` |
| INFRA-15 | El consumo de memoria se acota por servicio para una t3.micro (1 GiB): API 220 MiB por réplica, Redis 128 MiB, Nginx 64 MiB y `DOTNET_GCHeapHardLimit` de 96 MiB | `docker-compose.yml` (`deploy.resources.limits`, variables `DOTNET_*`) |
| INFRA-16 | El healthcheck del balanceador consulta una ruta **real** de la API (`GET /health`, ficha `specs/cross-cutting/api-health-probe/`); el de la API permanece como liveness TCP para no reiniciar réplicas por una caída de Atlas | `docker-compose.yml` (healthcheck de Nginx y de API) |

## 3. Recursos declarados

| Recurso | Tipo | Propósito |
|---|---|---|
| `data.aws_ami.ubuntu_2404` | Data source | AMI Ubuntu 24.04 LTS oficial de Canonical |
| `data.aws_vpc.default` | Data source | VPC por defecto de la cuenta |
| `aws_security_group.api` | SG | Ingress 22/80/443, egress total |
| `aws_iam_role.ec2` / `aws_iam_role_policy.cloudwatch_logs` / `aws_iam_role_policy_attachment.ssm` / `aws_iam_instance_profile.ec2` | IAM | Acceso de la instancia a CloudWatch Logs y SSM opcional |
| `aws_cloudwatch_log_group.app` | CloudWatch | Grupo `/utttgo/api/<env>` con retención configurable (7 días por defecto) |
| `aws_instance.api` | EC2 | t3.micro Ubuntu 24.04, Docker + Compose vía `user_data` sin secretos |
| `aws_eip.api` | Elastic IP | IP pública fija del API |
| `aws_s3_bucket.images` + controles | S3 | Imágenes privadas, cifradas, con CORS para presigned |
| `aws_budgets_budget.zero_cost_guard` | Budget | Alerta de costo (condicional a `budget_alert_email`) |
| `random_id.images_suffix` | random | Sufijo único del bucket para evitar colisiones de nombre global |

Salidas: `instance_id`, `instance_public_ip`, `elastic_ip`, `images_bucket_name`, `log_group_name`.

### 3.1 Archivos de despliegue en la raíz del repositorio

| Archivo | Contenido | Verificación |
|---|---|---|
| `Dockerfile` | Build multi-stage `mcr.microsoft.com/dotnet/sdk:10.0` → `aspnet:10.0`; publicación de `src/UTTTGO.Api/UTTTGO.Api.csproj`; usuario `app`; sin secretos | `docker compose build api` (imagen construida) |
| `.dockerignore` | Excluye `.git`, `bin/obj`, `tests`, `material`, `docs`, `specs`, `.env` y `certbot` del contexto de build | Revisión del contexto de build |
| `docker-compose.yml` | `api` (2 réplicas), `redis` (interno), `nginx` (80/443), `mongo` solo con perfil `local`; redes `frontend` (con salida) y `backend` (interna) | `docker compose config` + stack local probado |
| `nginx/nginx.conf` | Log format con `upstream_addr`, gzip, `client_max_body_size 10m`, mapa de `Upgrade` para WebSocket/SignalR | `nginx -t` en imagen real (sintaxis válida) |
| `nginx/conf.d/default.conf` | HTTP en el puerto 80, desafío ACME y proxy a `api:8080` con `resolver 127.0.0.11` (balanceo por DNS de Docker) | `nginx -t` y prueba de reparto entre réplicas |
| `nginx/conf.d/tls.conf.template` | Plantilla TLS (443 + redirección 80→443) con marcador `__DOMAIN__` | Renderizado y validado con `nginx -t` |
| `setup.sh` | Instalación de Docker/Compose/Certbot, `.env`, build, `up --scale api=2`, emisión TLS, hook de renovación, verificación | Ejecución local no destructiva del flujo; ejecución real en la EC2 pendiente |

## 4. Límites y exclusiones

- **No** ALB, ASG, ElastiCache, RDS, DocumentDB, NAT Gateway ni instance profile con `AdministratorAccess`.
- **No** claves SSH, secretos, tokens, service accounts ni connection strings en Terraform.
- **No** `terraform apply`/`destroy` en PR ni en CI; el estado remoto se migra a S3 + DynamoDB antes de producción (INFRA-08).
- **No** endpoints nuevos de la API ni cambios de middleware (`Program.cs`).
- No se declara en esta spec la iluminación de las "dos instancias" más allá de las 2 réplicas Compose dentro de la misma EC2 (decisión DEC-018).

## 5. Criterios de aceptación

| ID | Criterio |
|---|---|
| INFRA-AC-01 | `terraform init -backend=false` completa la instalación de proveedores `hashicorp/aws` y `hashicorp/random` sin errores. |
| INFRA-AC-02 | `terraform fmt -check -recursive` no reporta diferencias en `infra/terraform/`. |
| INFRA-AC-03 | `terraform validate` responde «The configuration is valid». |
| INFRA-AC-04 | El bucket S3 queda con `block_public_acls`, `block_public_policy`, `ignore_public_acls` y `restrict_public_buckets` en `true`. |
| INFRA-AC-05 | La instancia exige IMDSv2 y su perfil de rol no incluye políticasadministrativas. |
| INFRA-AC-06 | El diff de `terraform plan` revisado por el propietario no muestra secretos ni credenciales embebidas. |
| INFRA-AC-07 | Tras el primer `apply`, la alerta de AWS Budget está activa y es la evidencia del objetivo de costo $0 (pendiente hasta que el propietario ejecute `apply`). |
| INFRA-AC-08 | `docker compose config` es válido y el perfil renderizado muestra `replicas: 2` para `api` y ningún puerto publicado para `redis`. |
| INFRA-AC-09 | Con el stack local levantado, las peticiones al API atraviesan Nginx y se reparten entre las dos réplicas (se observa en el log `ua="$upstream_addr"`). |
| INFRA-AC-10 | Redis no es alcanzable desde el host (sin puertos publicados) y sí desde el contenedor de la API. |
| INFRA-AC-11 | `nginx -t` valida tanto la configuración HTTP como la plantilla TLS renderizada con un dominio real. |
| INFRA-AC-12 | En la EC2, `setup.sh` completa la instalación y deja el stack arriba con 2 réplicas sanas, el certificado emitido y la renovación con hook configurada. |
| INFRA-AC-13 | El contenedor Nginx queda `healthy` solo cuando `GET /health` responde `200` y pasa a `unhealthy` cuando MongoDB o Redis no responden, sin que las réplicas de la API se reinicien. |

## 6. Riesgos

| Riesgo | Mitigación / responsable |
|---|---|
| Costo por Elastic IP y EBS fuera de Free Tier | Budget con alerta y revisión mensual; NAT Gateway y ALB evitados por diseño |
| SG con 22 abierto a `0.0.0.0/0` | Decisión explícita del propietario (petición literal); mitigación pendiente con bastion/IAM en EC2 antes de producción |
| `allowed_origins = ["*"]` por defecto | Sustituir por orígenes explícitos antes de producción; la app móvil usa orígenes controlados |
| Datos en estado local de Terraform | `.gitignore` lo excluye; backend remoto es refactor obligatorio antes de producción |
| Boot de Docker no verificado | `plan`/`apply` y una prueba de inicio de instancia confirmarán `user_data`; no se afirma en esta spec que la instancia esté creada |
| IMDS inaccesible desde los contenedores | `http_put_response_hop_limit = 2` en la instancia; si el `apply` no incluye ese cambio, el backend no obtiene credenciales del Instance Profile y el presigned de S3 falla |
| Memoria de la t3.micro (1 GiB) | Límites por servicio y `DOTNET_GCHeapHardLimit`; vigilar `docker stats` y el OOM killer tras el arranque |
| TLS sin dominio válido | El script exige `--domain` con registro A correcto; sin dominio el API queda en HTTP por la IP y sin redirección |
| `UseHttpsRedirection()` sin puerto HTTPS | Dentro del contenedor no redirige (aviso en log) porque el TLS termina en Nginx; el esquema reenviado requiere cambio de middleware aprobado |

## 7. Aprobación

- Aprobación de nube y costo $0: **DEC-018** (constitución v2.4, 2026-11-06), propietario de producto.
- Implementación de código de infraestructura: AGENT-BE-01 con apoyo de AGENT-BE-03.
- `plan`/`apply`: ejecución local del propietario con sus credenciales; el agente no ejecuta `apply`.
