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

variable "key_name" {
  description = "Name of an existing EC2 key pair (us-east-2) for SSH. Leave null to skip; SSM Session Manager is the recommended alternative."
  type        = string
  default     = null
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
  description = "Origins allowed by the images bucket CORS for presigned PUT/GET traffic. Replace '*' with explicit origins before production (constitution § 6.1)."
  type        = list(string)
  default     = ["*"]
}

variable "budget_alert_email" {
  description = "Email subscriber for the AWS Budget alert that evidences the $0 objective. Leave null to skip the budget."
  type        = string
  default     = null
}

variable "budget_limit_usd" {
  description = "Monthly cost alert threshold in USD. Any real charge trips the notification."
  type        = number
  default     = 1
}

# Credentials note: supply AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY / AWS_REGION as
# environment variables or via "aws configure". No credential variables here on purpose,
# so no secret can land in *.tfvars or code. *.tfvars is gitignored.
