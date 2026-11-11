# task.md — Infraestructura AWS Free Tier · Backend UTTT-GO

> Versión documental **1.2** · 2026-11-11 · Derivado de `plan.md` v1.1 y del cierre de infraestructura DEC‑020/021. Estados: `[x]` completada con evidencia, `[ ]` pendiente, `[-]` cancelada con motivo.

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
| INFRA-T08 | Activar la alerta de Budget con el correo del responsable y adjuntar evidencia del primer mes a costo $0 | INFRA-07, INFRA-AC-07, DF02-11 | Propietario + responsable de presupuesto | `[x]` | `aws_budgets_budget.zero_cost_guard` aplicado el 2026-11-11 (`utttgo-dev-zero-cost-guard`: COST, USD, MONTHLY, límite 1 USD) con notificación `ACTUAL`/`GREATER_THAN`/umbral 0 % en estado `OK` (verificado con `DescribeNotificationsForBudget`; el CLI no expone esa operación). Correo institucional inyectado por `infra/terraform/budget.tfvars` (ignorado por Git) |
| INFRA-T09 | Materializar `docker-compose.yml` de producción (2 réplicas API + Nginx + Redis 7) y TLS en la EC2 | D-INFRA-01, D-INFRA-02 | AGENT-BE-01 | `[x]` | `docker-compose.yml` (2 réplicas, Redis sin puertos, Nginx 80/443, `mongo` en perfil `local`), `docker-compose.prod.yml` (override de la EC2 con el driver `awslogs`), `Dockerfile`, `.dockerignore`, `nginx/{nginx.conf,conf.d/default.conf,conf.d/tls.conf.template}` y `setup.sh`; verificados con `docker compose config`, build de imagen, `nginx -t`, prueba de balanceo local y **despliegue real en la EC2 por SSM el 2026-11-11 (2/2 réplicas sanas, `/health` 200)** |
| INFRA-T10 | Implementar en el backend la generación de presigned S3 y el Instance Profile (sin claves en la instancia) | INFRA-05 | AGENT-BE-04 / AGENT-BE-05 | `[ ]` | Fuera de esta spec; requiere IMDS accesible desde contenedores (ver INFRA-T12) |
| INFRA-T11 | Migrar el estado de Terraform a backend remoto (S3 + DynamoDB) | INFRA-08 | AGENT-BE-01 | `[ ]` | Refactor obligatorio antes de producción |
| INFRA-T12 | Aplicar en la EC2 el ajuste `http_put_response_hop_limit = 2` para que los contenedores alcancen IMDS | INFRA-14 | Propietario | `[x]` | Aplicado: `aws ec2 describe-instances` devuelve `HttpPutResponseHopLimit = 2` y el estado de Terraform lo refleja (`ignore_changes` solo sobre `ami` y `user_data`) |
| INFRA-T13 | Ejecutar `setup.sh` en la EC2 con un dominio real, verificar 2 réplicas sanas, TLS emitido y hook de renovación | INFRA-11, INFRA-12, INFRA-AC-12 | Propietario | `[ ]` | Pendiente: requiere registro A apuntando a `3.129.142.54` y `.env` con Atlas/Supabase/S3 reales |
| INFRA-T14 | Habilitar la alerta de Budget tras el `apply` y adjuntar la evidencia de costo $0 del primer mes | INFRA-AC-07 | Propietario + responsable de presupuesto | `[x]` | Budget con notificación activa y `ActualSpend` 0.00 USD; Cost Explorer del 2026‑11 en 0.00 USD. La evidencia del cierre de mes queda en manos del propietario (requiere el cierre de facturación) |
| INFRA-T15 | Verificar la memoria de la instancia con `docker stats` y ajustar límites si el OOM killer interviene | INFRA-15 | Propietario | `[ ]` | Pendiente de la ejecución en la EC2 |
| INFRA-T16 | Sustituir el healthcheck de Nginx por la sonda real `GET /health` y documentar la separación liveness/readiness | INFRA-16, INFRA-AC-13, HP-07, HP-08 | AGENT-BE-01 | `[x]` | `docker-compose.yml`: Nginx consulta `http://127.0.0.1/health`; la API conserva el sondeo TCP al puerto 8080. Verificado en stack local: Nginx `healthy` con Mongo y Redis, `unhealthy` al detener Mongo, y las réplicas de la API siguen `healthy` |
| INFRA-T17 | Cerrar el puerto 22 y mover el despliegue CD‑1 a AWS Systems Manager | DEC‑020 | AGENT-BE-01 | `[x]` | SG con ingress real **solo 80/443** (`aws ec2 describe-security-groups`); documento SSM `utttgo-dev-deploy` + `infra/ssm/deploy.sh`; usuario IAM `utttgo-dev-github-actions-deploy` (política `ssm-deploy`); CD‑1 migrado a `aws ssm send-command`. Despliegue real validado el 2026‑11‑11 (SHA `355db03`): 2/2 réplicas sanas, `/health` 200 |
| INFRA-T18 | Publicar los logs de los contenedores en CloudWatch Logs | DEC‑021, INFRA‑05 | AGENT-BE-01 | `[x]` | `docker-compose.prod.yml` con el driver `awslogs` (Instance Profile, IMDSv2); streams `api`, `nginx` y `redis` verificados en `/utttgo/api/dev`. Se descartó el CloudWatch Agent: la URL oficial del paquete `.deb` no era resoluble desde la instancia y el driver nativo no lo requiere |
| INFRA-T19 | Endurecer el CORS del bucket S3 (lista estricta de orígenes, comodín prohibido) | DEC‑020, BR‑007 | AGENT-BE-01 | `[x]` | `allowed_origins` vacío por defecto (bucket sin regla CORS) con validación que rechaza `*`; aplicado y verificado (`NoSuchCORSConfiguration`) |
| INFRA-T20 | Corregir el `description` del Security Group en el próximo reemplazo consciente de la instancia | DEC‑020 | Propietario | `[ ]` | Pendiente: el `description` es `ForceNew`; corregirlo recrearía el SG y la instancia (y perdería el `.env`). El estado real queda en la etiqueta `Ingress`; se corrige junto con el rollover de AMI |
| INFRA-T21 | Acortar el despliegue construyendo la imagen en CI (ECR) o habilitando T3 Unlimited en ventana de mantenimiento | DEC‑020 | AGENT-BE-01 / Propietario | `[ ]` | Pendiente: con los CPU credits agotados (balance ≈ 282/3000) el `dotnet publish` en la t3.micro tardó ≈ 29 min; CD‑1 sube su timeout a 75 min mientras tanto |

## 2. Comandos pendientes para el propietario (PowerShell)

```powershell
# 1) Credenciales: usa "aws configure" o variables de entorno. NO las pegues en el chat ni en archivos.
$env:AWS_REGION = "us-east-2"
# $env:AWS_ACCESS_KEY_ID = "<tu clave>"
# $env:AWS_SECRET_ACCESS_KEY = "<tu clave secreta>"

# 2) El apply del cierre de infraestructura ya se ejecutó (2026-11-11).
#    El correo del Budget vive en infra/terraform/budget.tfvars (ignorado por Git).
terraform -chdir=infra/terraform init
terraform -chdir=infra/terraform plan  "-var-file=infra/terraform/budget.tfvars"
terraform -chdir=infra/terraform apply "-var-file=infra/terraform/budget.tfvars"

# 3) CD-1 en GitHub Actions: crear la clave del usuario IAM de despliegue y
#    guardarla como secreto del repositorio (AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY)
aws iam create-access-key --user-name utttgo-dev-github-actions-deploy
```

```bash
# Despliegue manual equivalente al de CD-1 (mismo documento SSM, sin SSH).
# El CLI exige que el valor de cada parametro sea una lista JSON: sha=["<sha>"].
aws ssm send-command --document-name utttgo-dev-deploy \
  --targets "Key=instanceids,Values=i-0394b36c6c435e08d" \
  --parameters "sha=[\"<sha completo>\"]"

# Logs de los contenedores (en la EC2 no hay docker compose logs)
aws logs tail /utttgo/api/dev --since 10m
```

Notas: el `apply` inicial se ejecutó el 2026-11-06 (Elastic IP `3.129.142.54`) y el cierre de infraestructura el 2026-11-11 (SG sin 22, Budget con alerta, CORS estricto, logs en CloudWatch Logs). Quedan pendientes el `description` del SG, el backend remoto del estado, TLS con dominio y el build de la imagen en CI. El estado local (`*.tfstate`) queda fuera de Git; no versionar `terraform.tfvars`.

## 3. Criterios de cierre

- [x] INFRA-T08, INFRA-T12 y INFRA-T14 completados con evidencia (Budget con notificación `OK`, hop limit 2, costo del mes 0.00 USD).
- [x] INFRA-T17, INFRA-T18 e INFRA-T19 completados con evidencia (SG sin 22, despliegue real por SSM, logs publicados en CloudWatch Logs, CORS sin reglas).
- [ ] INFRA-T13 (TLS con dominio real), INFRA-T15 (memoria con `docker stats`), INFRA-T20 (`description` del SG) e INFRA-T21 (build de la imagen en CI) completados por el propietario con evidencia.
- [x] `memoria.md` actualizado con el resultado del apply (recursos reales, evidencia de costo) y con el despliegue ejecutado.
- [ ] `docs/traceability.md` y `docs/academic-requirements-phase-02.md` actualizados con la evidencia de las «dos instancias» y costo $0.
- [x] Sin secretos en el diff ni en los logs del plan; `.env` de la instancia sin versionar.
- [x] `GET /api/v1/auth/me` responde `401` sin token a través de Nginx (o `503` si Atlas/Supabase no están configurados) y las dos réplicas responden.
