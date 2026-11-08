# task.md — Infraestructura AWS Free Tier · Backend UTTT-GO

> Versión documental **1.1** · 2026-11-06 · Derivado de `plan.md` v1.1. Estados: `[x]` completada con evidencia, `[ ]` pendiente, `[-]` cancelada con motivo.

## 1. Tareas

| ID | Tarea | Requisitos | Responsable | Estado | Evidencia |
|---|---|---|---|---|---|
| INFRA-T01 | Congelar DEC-018 (Azure → AWS) en constitución v2.4 y propagar a `AGENTS.md`, `memoria.md`, `docs/` y specs por fase | DEC-018 | AGENT-BE-01 / AGENT-BE-06 | `[x]` | `docs/constitution.md` v2.4 (§ 2, § 9, § 16.1, changelog); `AGENTS.md` v2.3; `memoria.md` fila DEC-018 y log de cambios v2.4; `docs/academic-requirements-phase-02.md` §§ 13/15; `docs/traceability.md`; `docs/decisiones-pendientes-fase-02.md` DF02-11 |
| INFRA-T02 | Escribir `infra/terraform/variables.tf` con validaciones y sin variables de credencial | INFRA-01, INFRA-09 | AGENT-BE-01 | `[x]` | `infra/terraform/variables.tf` (`aws_region`, `project_name`, `environment`, `instance_type`, `root_volume_size` 8–30, `key_name`, `install_docker`, `enable_ssm`, `app_log_group_name`, `log_retention_days`, `images_bucket_suffix`, `bucket_force_destroy`, `allowed_origins`, `budget_alert_email`, `budget_limit_usd`) |
| INFRA-T03 | Escribir `infra/terraform/main.tf` con provider, data sources, SG, IAM, log group, EC2, EIP, S3 y Budget | INFRA-01..08 | AGENT-BE-01 | `[x]` | `infra/terraform/main.tf`; `outputs`: `instance_id`, `instance_public_ip`, `elastic_ip`, `images_bucket_name`, `log_group_name` |
| INFRA-T04 | Escribir `infra/terraform/terraform.tfvars.example` sin secretos | INFRA-09 | AGENT-BE-01 | `[x]` | `infra/terraform/terraform.tfvars.example` (`.tfvars` real está en `.gitignore`) |
| INFRA-T05 | Verificación estática con Terraform portable (`init -backend=false`, `fmt -check -recursive`, `validate`) | INFRA-10, INFRA-AC-01..03 | AGENT-BE-01 | `[x]` | Salidas: proveedores instalados (aws 6.72.0, random 3.9.1); `FMT_GREEN`; `Success! The configuration is valid.` Correcciones aplicadas: bloque `cors_rule` (AWS v6) y `locals.docker_bootstrap` para el heredoc de `user_data` |
| INFRA-T06 | Ejecutar `terraform plan` con credenciales del propietario y revisar el diff sin secretos | INFRA-AC-06 | Propietario | `[x]` | Ejecutado por el propietario el 2026-11-06 (evidencia declarada por el propietario; el agente no tiene credenciales) |
| INFRA-T07 | Ejecutar `terraform apply` tras aprobar el plan y verificar Elastic IP, SG y bucket privado | INFRA-AC-04, INFRA-AC-05 | Propietario | `[x]` | `apply` correcto; Elastic IP `3.129.142.54` (declarado por el propietario) |
| INFRA-T08 | Activar la alerta de Budget con el correo del responsable y adjuntar evidencia del primer mes a costo $0 | INFRA-07, INFRA-AC-07, DF02-11 | Propietario + responsable de presupuesto | `[ ]` | Pendiente: definir `budget_alert_email` y re-aplicar |
| INFRA-T09 | Materializar `docker-compose.yml` de producción (2 réplicas API + Nginx + Redis 7) y TLS en la EC2 | D-INFRA-01, D-INFRA-02 | AGENT-BE-01 | `[x]` | `docker-compose.yml` (2 réplicas, Redis sin puertos, Nginx 80/443, `mongo` en perfil `local`), `Dockerfile`, `.dockerignore`, `nginx/{nginx.conf,conf.d/default.conf,conf.d/tls.conf.template}` y `setup.sh`; verificados con `docker compose config`, build de imagen, `nginx -t` y prueba de balanceo local (2 réplicas `401` vía Nginx, upstream alternado) |
| INFRA-T10 | Implementar en el backend la generación de presigned S3 y el Instance Profile (sin claves en la instancia) | INFRA-05 | AGENT-BE-04 / AGENT-BE-05 | `[ ]` | Fuera de esta spec; requiere IMDS accesible desde contenedores (ver INFRA-T12) |
| INFRA-T11 | Migrar el estado de Terraform a backend remoto (S3 + DynamoDB) | INFRA-08 | AGENT-BE-01 | `[ ]` | Refactor obligatorio antes de producción |
| INFRA-T12 | Aplicar en la EC2 el ajuste `http_put_response_hop_limit = 2` para que los contenedores alcancen IMDS | INFRA-14 | Propietario | `[ ]` | Código listo en `infra/terraform/main.tf`; requiere `terraform apply` (actualización en sitio, sin recrear la instancia) |
| INFRA-T13 | Ejecutar `setup.sh` en la EC2 con un dominio real, verificar 2 réplicas sanas, TLS emitido y hook de renovación | INFRA-11, INFRA-12, INFRA-AC-12 | Propietario | `[ ]` | Pendiente: requiere registro A apuntando a `3.129.142.54` y `.env` con Atlas/Supabase/S3 reales |
| INFRA-T14 | Habilitar la alerta de Budget tras el `apply` y adjuntar la evidencia de costo $0 del primer mes | INFRA-AC-07 | Propietario + responsable de presupuesto | `[ ]` | Pendiente; cierra el punto residual de DF02-11 |
| INFRA-T15 | Verificar la memoria de la instancia con `docker stats` y ajustar límites si el OOM killer interviene | INFRA-15 | Propietario | `[ ]` | Pendiente de la ejecución en la EC2 |
| INFRA-T16 | Sustituir el healthcheck de Nginx por la sonda real `GET /health` y documentar la separación liveness/readiness | INFRA-16, INFRA-AC-13, HP-07, HP-08 | AGENT-BE-01 | `[x]` | `docker-compose.yml`: Nginx consulta `http://127.0.0.1/health`; la API conserva el sondeo TCP al puerto 8080. Verificado en stack local: Nginx `healthy` con Mongo y Redis, `unhealthy` al detener Mongo, y las réplicas de la API siguen `healthy` |

## 2. Comandos pendientes para el propietario (PowerShell)

```powershell
# 1) Credenciales: usa "aws configure" o variables de entorno. NO las pegues en el chat ni en archivos.
$env:AWS_REGION = "us-east-2"
# $env:AWS_ACCESS_KEY_ID = "<tu clave>"
# $env:AWS_SECRET_ACCESS_KEY = "<tu clave secreta>"

# 2) Actualización pendiente: hop limit de IMDS para que los contenedores alcancen el Instance Profile
terraform -chdir=infra/terraform init
terraform -chdir=infra/terraform plan
terraform -chdir=infra/terraform apply

# 3) Evidencia de costo $0: define budget_alert_email en terraform.tfvars (local, gitignored) y re-aplica
terraform -chdir=infra/terraform apply
```

```bash
# En la EC2 (3.129.142.54), con el repositorio clonado y un dominio con registro A a esa IP
sudo bash setup.sh --domain api.tudominio.mx --email tu@correo.com
docker compose ps
docker compose logs -f api
```

Notas: el `apply` inicial ya se ejecutó (Elastic IP `3.129.142.54`); lo pendiente es la actualización del hop limit y la alerta de Budget. El estado local (`*.tfstate`) queda fuera de Git; no versionar `terraform.tfvars`.

## 3. Criterios de cierre

- [ ] INFRA-T08, INFRA-T12, INFRA-T13, INFRA-T14 e INFRA-T15 completados por el propietario con evidencia (salidas de `plan`/`apply`, `docker compose ps` con 2 réplicas sanas, certificado y hook de renovación, alerta de Budget).
- [ ] `memoria.md` actualizado con el resultado del apply (recursos reales, evidencia de costo) y con el despliegue ejecutado.
- [ ] `docs/traceability.md` y `docs/academic-requirements-phase-02.md` actualizados con la evidencia de las «dos instancias» y costo $0.
- [ ] Sin secretos en el diff ni en los logs del plan; `.env` de la instancia sin versionar.
- [ ] `GET /api/v1/auth/me` responde `401` sin token a través de Nginx (o `503` si Atlas/Supabase no están configurados) y las dos réplicas responden.
