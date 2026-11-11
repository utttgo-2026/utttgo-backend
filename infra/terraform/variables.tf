# =============================================================================
# UTTT-GO Backend — variables.tf (AWS Free Tier, DEC-018)
# Sin secretos: las credenciales llegan por variables de entorno del usuario
# (AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY); jamás por tfvars ni código.
# =============================================================================

variable "aws_region" {
  description = "AWS region for the UTTT-GO backend stack (Free Tier eligible)."
  type        = string
  default     = "us-east-2"
}

variable "project_name" {
  description = "Short project identifier used in resource names and tags."
  type        = string
  default     = "utttgo"
}

variable "environment" {
  description = "Environment tag (dev/staging/prod). The MVP Free Tier stack targets dev first."
  type        = string
  default     = "dev"

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "environment must be one of: dev, staging, prod."
  }
}

variable "instance_type" {
  description = "EC2 instance type. t3.micro is required to stay inside the Free Tier (750 h month)."
  type        = string
  default     = "t3.micro"
}

variable "root_volume_size" {
  description = "Root EBS volume size in GiB. Default 25 stays within the 30 GiB Free Tier storage bundle."
  type        = number
  default     = 25

  validation {
    condition     = var.root_volume_size >= 8 && var.root_volume_size <= 30
    error_message = "root_volume_size must stay between 8 and 30 GiB to protect the $0 cost objective."
  }
}

variable "install_docker" {
  description = "Install Docker Engine and the Compose plugin on boot (needed for the 2 API replicas + Nginx + Redis Compose stack)."
  type        = bool
  default     = true
}

variable "enable_ssm" {
  description = "Attach AmazonSSMManagedInstanceCore so the instance can be managed via Session Manager without opening SSH."
  type        = bool
  default     = true
}

variable "app_log_group_name" {
  description = "CloudWatch Logs group for the API lifecycle logs."
  type        = string
  default     = "/utttgo/api"
}

variable "log_retention_days" {
  description = "CloudWatch Logs retention. Short retention keeps storage inside the free allowance."
  type        = number
  default     = 7
}

variable "images_bucket_suffix" {
  description = "Suffix appended to the images bucket name for global uniqueness. Leave null to generate a random one."
  type        = string
  default     = null
}

variable "bucket_force_destroy" {
  description = "Allow terraform destroy to delete a non-empty images bucket (dev convenience). Set false before prod."
  type        = bool
  default     = true
}

variable "allowed_origins" {
  description = <<-EOT
    Orígenes permitidos por el CORS del bucket de imágenes para el tráfico presigned
    PUT/GET. Lista ESTRICTA: el comodín "*" queda prohibido (constitución § 6.1).
    Por defecto está vacía, es decir, el bucket NO expone ninguna regla CORS: el
    cliente móvil nativo no envía cabecera Origin y no la necesita, y ningún
    navegador puede leer el bucket. Cuando exista el dominio de la API, declarar
    aquí el origen exacto (p. ej. ["https://api.tudominio.mx"]) y aplicar.
  EOT
  type        = list(string)
  default     = []

  validation {
    condition = length(var.allowed_origins) == 0 || (
      alltrue([
        for origin in var.allowed_origins :
        can(regex("^https://[A-Za-z0-9.-]+(:[0-9]{1,5})?$", origin))
      ])
    )
    error_message = "allowed_origins debe estar vacia o contener solo origenes https:// explicitos; el comodin esta prohibido."
  }
}

variable "budget_alert_email" {
  description = <<-EOT
    Buzón suscriptor de la alerta de costo de AWS Budget (objetivo $0).
    Es dato personal: se inyecta por variable de entorno (TF_VAR_budget_alert_email)
    o por un archivo *.tfvars local ignorado por Git; nunca se versiona.
    Debe ser un correo institucional @uttt.edu.mx real para recibir la notificación.
  EOT
  type        = string
  nullable    = false

  validation {
    condition     = can(regex("^[A-Za-z0-9._%+-]+@uttt\\.edu\\.mx$", var.budget_alert_email))
    error_message = "budget_alert_email debe ser un correo institucional valido con dominio @uttt.edu.mx."
  }
}

variable "budget_limit_usd" {
  description = "Umbral mensual de alerta de costo en USD. Cualquier cargo real dispara la notificacion."
  type        = number
  default     = 1
}

variable "github_repository" {
  description = "Repositorio GitHub propietario del flujo CD-1 (formato owner/repo)."
  type        = string
  default     = "utttgo-2026/utttgo-backend"
}

variable "github_branch" {
  description = "Rama cuyo workflow despliega a la EC2."
  type        = string
  default     = "main"
}

# Credentials note: supply AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY / AWS_REGION as
# environment variables or via "aws configure". No credential variables here on purpose,
# so no secret can land in *.tfvars or code. *.tfvars is gitignored.
