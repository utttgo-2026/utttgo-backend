# Terraform — UTTT-GO Backend en AWS

IaC del stack de DEC-018/020/021 (región `us-east-2`, cuenta de desarrollo).

## Recursos declarados

| Recurso | Nombre | Notas |
|---|---|---|
| EC2 | `utttgo-dev-api` | t3.micro, Ubuntu 24.04, IMDSv2 (hop limit 2) |
| Elastic IP | `3.129.142.54` | fija, asociada a la instancia |
| Security Group | `utttgo-api-dev-*` | ingress **80/443**; el 22 está cerrado (administración por SSM) |
| S3 | `utttgo-images-*` | privado, Block Public Access, SSE, CORS estricto (vacío por defecto) |
| IAM | rol de la instancia + `utttgo-dev-github-actions-deploy` | permisos de CloudWatch Logs y `ssm-deploy` |
| CloudWatch Logs | `/utttgo/api/dev` | alimentado por el driver `awslogs` de Docker (DEC-021) |
| SSM | `utttgo-dev-deploy` | documento de despliegue ejecutado por CD-1 |
| Budget | `utttgo-dev-zero-cost-guard` | COST mensual, umbral 0 %, `prevent_destroy` |

## Uso

```bash
terraform init
terraform plan  -var-file=budget.tfvars
terraform apply -var-file=budget.tfvars
```

`budget.tfvars` contiene el correo suscriptor del Budget y **está ignorado por Git**
(`*.tfvars`); también puede inyectarse con `TF_VAR_budget_alert_email`.
El correo debe ser institucional `@uttt.edu.mx`: la variable lo valida y el `apply`
falla si falta, para no destruir en silencio la alerta de costo.

## Credenciales de CD-1 en GitHub Actions

El SCP de la organización deniega `iam:CreateOpenIDConnectProvider`, por lo que
GitHub Actions **no puede autenticarse en AWS por OIDC** en esta cuenta. CD-1 usa
un usuario IAM de privilegio mínimo creado por este Terraform:

```bash
aws iam create-access-key --user-name utttgo-dev-github-actions-deploy
```

Guarda el `AccessKeyId` y el `SecretAccessKey` como secretos del repositorio
(**Settings → Secrets and variables → Actions**):

| Secreto | Valor |
|---|---|
| `AWS_ACCESS_KEY_ID` | `AccessKeyId` de la salida anterior |
| `AWS_SECRET_ACCESS_KEY` | `SecretAccessKey` de la salida anterior |

La clave **no** se escribe en Terraform, en el estado ni en el repositorio. El
usuario solo puede enviar comandos SSM y leer los logs del grupo de la API.
Rotar la clave: `aws iam delete-access-key` + `create-access-key` y actualizar el secreto.

## Verificación del Budget y de los logs

```bash
# Costo real del mes
aws ce get-cost-and-usage --time-period Start=2026-11-01,End=2026-11-12 \
  --granularity MONTHLY --metrics BlendedCost

# Notificaciones del Budget (el CLI no las expone en describe-budget:
# requiere la operación DescribeNotificationsForBudget o la consola)
aws budgets describe-budget --account-id <cuenta> --budget-name utttgo-dev-zero-cost-guard --debug

# Logs de los contenedores
aws logs tail /utttgo/api/dev --since 10m
```

## Notas de operación

- El `description` del Security Group conserva el texto histórico porque en el
  proveedor de AWS es `ForceNew`: cambiarlo recrearía la instancia y perdería el
  `.env` del servidor. El estado real de la exposición está en la etiqueta
  `Ingress` y se corrige en el próximo reemplazo consciente de la instancia.
- `ami` y `user_data` están en `ignore_changes`: cambiarlos obligaría a recrear
  la instancia. El rollover de AMI se decide en una ventana de mantenimiento.
- El estado es local y está fuera de Git; el backend remoto (S3 + DynamoDB) es un
  refactor obligatorio antes de producción.
