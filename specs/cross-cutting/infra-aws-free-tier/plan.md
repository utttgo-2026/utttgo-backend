# plan.md — Infraestructura AWS Free Tier · Backend UTTT-GO

> Versión documental **1.1** · 2026-11-06 · Derivado de `spec.md` v1.1 (aprobada). Estado: IaC implementada y **aplicada por el propietario** (EIP `3.129.142.54`); despliegue en la EC2 materializado en la raíz del repositorio y validado localmente; ejecución en la instancia, TLS y evidencia de costo $0 pendientes.

## 1. Secuencia de implementación

| Paso | Contenido | Responsable | Estado |
|---|---|---|---|
| P1 | Congelar la decisión de nube/costo en la constitución (DEC-018) y propagar a `AGENTS.md`, `memoria.md`, `docs/traceability.md`, `docs/academic-requirements-phase-02.md`, `README.md`, `PROJECT_STATUS.md` y specs por fase | AGENT-BE-01 + AGENT-BE-06 | Completado |
| P2 | Crear `infra/terraform/` con `variables.tf` (variables con validación, sin credenciales) | AGENT-BE-01 | Completado |
| P3 | Crear `infra/terraform/main.tf`: provider + `default_tags`, data sources (AMI Ubuntu 24.04, VPC), SG, IAM (rol, política de logs, SSM opcional, instance profile), log group, EC2 (IMDSv2, gp3 cifrado, `user_data` sin secretos), EIP, S3 (privado, SSE, CORS, ownership), Budget condicional, outputs | AGENT-BE-01 | Completado |
| P4 | Crear `infra/terraform/terraform.tfvars.example` con valores no sensibles y nota de credenciales | AGENT-BE-01 | Completado |
| P5 | Verificación estática con binario portable de Terraform: `init -backend=false`, `fmt -check -recursive`, `validate` | AGENT-BE-01 | Completado (verde) |
| P6 | Revisión del propietario: `plan` con sus credenciales y `apply` tras aprobar el plan | Propietario | Pendiente |
| P7 | Evidencia de costo $0: alerta de Budget activa y primer mes sin cobros fuera de Free Tier | Propietario + responsable de presupuesto | Pendiente |
| P8 | Complementos de despliegue (fuera de esta spec): `docker-compose.yml` de producción con 2 réplicas API + Nginx + Redis 7, TLS, y presigned S3 en el backend | AGENT-BE-01 / AGENT-BE-05 | Pendiente |
| P9 | `Dockerfile` multi-stage (.NET 10 SDK → ASP.NET 10), usuario no root y `.dockerignore` | AGENT-BE-01 | Completado |
| P10 | `docker-compose.yml` de despliegue: `api` con `deploy.replicas: 2`, `redis:7-alpine` sin puertos publicados en red interna, `nginx:alpine` con 80/443 y `mongo` bajo perfil `local` | AGENT-BE-01 | Completado |
| P11 | Configuración de Nginx (`nginx/nginx.conf`, `conf.d/default.conf`, `conf.d/tls.conf.template`) con balanceo por DNS de Docker, WebSocket y desafío ACME | AGENT-BE-01 | Completado |
| P12 | `setup.sh` para Ubuntu 24.04: instala Docker, Docker Compose y Certbot, prepara `.env`, construye y levanta el stack con 2 réplicas, emite el TLS con hook de renovación y verifica | AGENT-BE-01 | Completado |
| P13 | Ajuste de Terraform para acceso a IMDS desde los contenedores (`http_put_response_hop_limit = 2`) y `apply` de actualización | AGENT-BE-01 + propietario | Código listo; `apply` pendiente |
| P14 | Ejecución de `setup.sh` en la EC2 con dominio real, verificación de las 2 réplicas y del certificado | Propietario | Pendiente |

## 2. Decisiones de diseño

| ID | Decisión | Motivo |
|---|---|---|
| D-INFRA-01 | Una sola EC2 en lugar de dos instancias administradas | El PDF exige «dos instancias» y costo $0; dos t3.micro más ALB/NAT exceden el Free Tier. DEC-018: 2 réplicas Compose detrás de Nginx dentro de la misma EC2 |
| D-INFRA-02 | Redis como contenedor, no ElastiCache | ElastiCache no es elegible en Free Tier y Redis 7 en contenedor cumple blacklist y rate limit distribuido entre réplicas |
| D-INFRA-03 | AMI de Canonical con dos patrones de nombre (`hvm-ssd-gp3` y `hvm-ssd`) | Amazon cambia el sufijo de la imagen en sus publicaciones; ambos filtros resuelven la misma AMI Ubuntu 24.04 |
| D-INFRA-04 | IMDSv2 obligatorio + raíz cifrada | Endurecimiento base de instancia sin costo |
| D-INFRA-05 | Nombre del bucket con sufijo aleatorio de 4 bytes | S3 exige unicidad global; `random_id` evita colisiones sin exigir un nombre fijo |
| D-INFRA-06 | Política de logs inline con `Resource` acotado al log group y sus streams | Mínimo privilegio (sin `logs:*` global) |
| D-INFRA-07 | Budget condicional a `budget_alert_email` | Evita fallo de provider cuando no se define correo y activa la evidencia de costo $0 cuando el propietario lo fija |
| D-INFRA-08 | Sin bloque `backend "s3"` todavía | Estado remoto (S3 + DynamoDB) es refactor obligatorio antes de producción; activarlo sin bucket/dynamo provisioned no es ejecutable |
| D-INFRA-09 | Credenciales fuera de Terraform (env vars / `aws configure`) | Evita secretos en código, `*.tfvars` y logs de plan |
| D-INFRA-10 | `user_data` mínimo (Docker + Compose + directorio de trabajo) | El `docker-compose.yml` de producción se materializa después; no se instalan imágenes ni secretos por bootstrap |
| D-INFRA-11 | Balanceo con `resolver 127.0.0.11` y `proxy_pass http://$api_upstream$request_uri` en lugar de un `upstream` estático | Docker DNS devuelve todas las IP de las réplicas y nginx reparte por petición; un bloque `upstream` fijaría la resolución al arrancar |
| D-INFRA-12 | Redis con `--maxmemory-policy noeviction` y AOF activo | La blacklist JWT no puede ser descartada por evicción: ante presión de memoria Redis falla (fail-closed) en vez de revivir tokens revocados |
| D-INFRA-13 | `mongo` bajo perfil `local` en el Compose de la raíz | En la EC2 la base es MongoDB Atlas; el perfil permite el espejo local sin publicar MongoDB en el despliegue |
| D-INFRA-14 | TLS con Certbot del host y `webroot` bind-montado en Nginx | Evita depender del puerto 80 libre para el modo `standalone` y permite renovar sin detener el balanceador |
| D-INFRA-15 | `ASPNETCORE_HTTP_PORTS` sin `ASPNETCORE_URLS` | Evita el aviso «Overriding HTTP_PORTS/HTTPS_PORTS» del host genérico y deja un único origen de configuración del puerto |
| D-INFRA-16 | Límites de memoria por servicio y `DOTNET_GCHeapHardLimit` de 96 MiB | Una t3.micro tiene 1 GiB: sin límites, dos réplicas .NET + Redis + Nginx pueden activar el OOM killer |

## 3. Verificación

| Comando | Resultado esperado | Estado |
|---|---|---|
| `terraform init -backend=false` | Proveedores `hashicorp/aws` y `hashicorp/random` instalados | Verde |
| `terraform fmt -check -recursive` | Sin diferencias | Verde (se aplicó `fmt` en `main.tf`) |
| `terraform validate` | «The configuration is valid» | Verde |
| `terraform plan` (propietario, con credenciales) | Diff sin secretos; revisar EIP,AMI, SG y bucket | Ejecutado por el propietario; `apply` correcto (EIP `3.129.142.54`) |
| `terraform apply` (propietario, tras aprobar el plan) | Recursos creados; evidencia en Budget | Ejecutado por el propietario; segunda `apply` pendiente para el hop limit de IMDS |
| `docker compose config` | Configuración renderizada válida con `replicas: 2` y Redis sin puertos | Verde |
| `docker compose build api` | Imagen `utttgo/api:0.1.0` construida desde el `Dockerfile` | Verde |
| `docker compose up -d --scale api=2` + prueba HTTP | 2 réplicas sanas; `401` en `/api/v1/auth/me` vía Nginx; log con `ua=` alternando entre las dos IP | Verde (prueba local con MongoDB del perfil `local`) |
| `nginx -t` (imagen real) | Sintaxis válida de `nginx.conf`, `default.conf` y `tls.conf` renderizado | Verde |
| `setup.sh` en la EC2 | Stack arriba con 2 réplicas, TLS emitido y renovación con hook | Pendiente (P14) |

Notas de la corrección aplicada durante la verificación: el bloque CORS del proveedor AWS v6 es `cors_rule` (no `rule`) y el `user_data` condicional se construye en `locals.docker_bootstrap` para validar la sintaxis del heredoc.

Notas de la verificación del despliegue (2026-11-06): `docker compose config` es válido; la imagen de la API compila; con el stack local las dos réplicas responden `401` en `/api/v1/auth/me` a través de Nginx y el log muestra direcciones de upstream alternadas (balanceo real); Redis no responde desde el host y sí desde el contenedor de la API; `nginx -t` valida la configuración HTTP y la plantilla TLS renderizada; el aviso `Failed to determine the https port for redirect` de `UseHttpsRedirection` es esperado porque el TLS termina en Nginx (no redirige dentro del contenedor).

## 4. Dependencias

- Cuentas AWS con región `us-east-2` habilitada y credenciales del propietario (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` o `aws configure`).
- MongoDB Atlas (M0) y FCM como servicios externos; sus secretos llegan al contenedor por variables de entorno de la instancia, no por Terraform.
- Aprobación de `plan` antes de `apply` (política del proyecto: `apply` manual del propietario).
