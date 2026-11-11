# =============================================================================
# UTTT-GO Backend — main.tf (AWS Free Tier, constitución v2.6 / DEC-018/020/021)
# Alcance: una EC2 t3.micro + EIP, SG 80/443 (22 cerrado), S3 privado con CORS
# estricto, Instance Profile con CloudWatch Logs y SSM, log group y Budget $0.
# Sin ALB/ASG/ElastiCache/RDS a propósito. Sin secretos en este código.
# =============================================================================

terraform {
  required_version = ">= 1.9.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.7"
    }
  }

  # MVP: estado local excluido por .gitignore. Backend remoto (S3 + DynamoDB)
  # es refactor obligatorio antes de producción (constitution.md § 9).
  # backend "s3" {}
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = var.project_name
      Environment = var.environment
      ManagedBy   = "terraform"
    }
  }
}

# -----------------------------------------------------------------------------
# Data sources
# -----------------------------------------------------------------------------

# Canonical publishes the Ubuntu 24.04 LTS (noble) server AMI. Both the
# ssd-gp3 and legacy ssd naming variants are accepted to survive image revamps.
data "aws_ami" "ubuntu_2404" {
  most_recent = true
  owners      = ["099720109477"]

  filter {
    name = "name"
    values = [
      "ubuntu/images/hvm-ssd-gp3/ubuntu-noble-24.04-amd64-server-*",
      "ubuntu/images/hvm-ssd/ubuntu-noble-24.04-amd64-server-*",
    ]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

data "aws_vpc" "default" {
  default = true
}

# -----------------------------------------------------------------------------
# Network: security group restricted to HTTP/HTTPS.
# SSH (22) queda CERRADO: la administracion de la instancia y el despliegue CD-1
# se hacen por AWS Systems Manager (Session Manager / send-command), sin claves
# SSH en la instancia ni en los secretos del repositorio (DEC-020).
#
# El `description` NO se modifica: en el proveedor de AWS es ForceNew y cambiarlo
# destruiria y recrearia el SG, lo que a su vez obliga a recrear la instancia
# (vpc_security_group_ids es ForceNew) y perderia el .env del servidor. El estado
# real de la exposicion queda en la etiqueta Ingress y en la documentacion.
# La correccion del texto queda pendiente para el proximo reemplazo consciente
# de la instancia (actualizacion de AMI).
# -----------------------------------------------------------------------------

resource "aws_security_group" "api" {
  name_prefix = "${var.project_name}-api-${var.environment}-"
  description = "UTTT-GO API ingress: SSH 22, HTTP 80, HTTPS 443"
  vpc_id      = data.aws_vpc.default.id

  tags = {
    Ingress = "80,443 (SSH cerrado; administracion por SSM Session Manager)"
  }

  ingress {
    description = "HTTPS"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "HTTP"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    description = "All outbound"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# -----------------------------------------------------------------------------
# IAM: instance profile with CloudWatch Logs (and optional SSM)
# -----------------------------------------------------------------------------

resource "aws_iam_role" "ec2" {
  name = "${var.project_name}-${var.environment}-ec2-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ec2.amazonaws.com"
        }
      }
    ]
  })
}

resource "aws_iam_role_policy" "cloudwatch_logs" {
  name = "cloudwatch-logs"
  role = aws_iam_role.ec2.name

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "logs:CreateLogGroup",
          "logs:CreateLogStream",
          "logs:DescribeLogStreams",
          "logs:PutLogEvents"
        ]
        Resource = [
          aws_cloudwatch_log_group.app.arn,
          "${aws_cloudwatch_log_group.app.arn}:*",
        ]
      }
    ]
  })
}

# Publicacion de logs: el driver `awslogs` de Docker escribe directamente en
# CloudWatch Logs usando el Instance Profile (IMDSv2). La politica anterior
# `cloudwatch-logs` cubre CreateLogStream y PutLogEvents; el driver no necesita
# el agente de CloudWatch instalado en la instancia (DEC-021).
resource "aws_iam_role_policy_attachment" "ssm" {
  count      = var.enable_ssm ? 1 : 0
  role       = aws_iam_role.ec2.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

resource "aws_iam_instance_profile" "ec2" {
  name = "${var.project_name}-${var.environment}-ec2-profile"
  role = aws_iam_role.ec2.name
}

# -----------------------------------------------------------------------------
# Credenciales de despliegue para CI (DEC-020).
# El SCP de la organizacion deniega `iam:CreateOpenIDConnectProvider`, por lo que
# GitHub Actions NO puede autenticarse en AWS por OIDC en esta cuenta. La
# alternativa admitida es un usuario IAM dedicado de privilegio minimo con una
# clave de acceso que vive en los secretos del repositorio
# (AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY), NO en Terraform ni en el estado.
#
# La clave se crea a mano (ver infra/terraform/README.md) para que el secreto
# nunca pase por el estado de Terraform ni por un artefacto.
# -----------------------------------------------------------------------------

resource "aws_iam_user" "github_deploy" {
  name = "${var.project_name}-${var.environment}-github-actions-deploy"
  path = "/ci/"

  tags = {
    Purpose = "CD-1 despliegue por SSM con send-command"
  }
}

resource "aws_iam_user_policy" "github_deploy" {
  name = "ssm-deploy"
  user = aws_iam_user.github_deploy.name

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "ssm:SendCommand",
          "ssm:GetCommandInvocation",
          "ssm:ListCommandInvocations",
          "ssm:ListCommands",
          "ssm:DescribeInstanceInformation"
        ]
        Resource = "*"
      },
      {
        Effect = "Allow"
        Action = [
          "ec2:DescribeInstances",
          "ec2:DescribeTags"
        ]
        Resource = "*"
      },
      {
        # Evidencia de despliegue: ultimos eventos del grupo de logs de la API.
        Effect = "Allow"
        Action = [
          "logs:DescribeLogStreams",
          "logs:FilterLogEvents",
          "logs:GetLogEvents"
        ]
        Resource = [
          aws_cloudwatch_log_group.app.arn,
          "${aws_cloudwatch_log_group.app.arn}:*"
        ]
      }
    ]
  })
}

# -----------------------------------------------------------------------------
# Systems Manager: documento de despliegue.
# El script vive en el repositorio (infra/ssm/deploy.sh) y se inyecta como
# documento SSM: la instancia queda descrita por la IaC y CD-1 lo invoca con
# `aws ssm send-command` (sin SSH, puerto 22 cerrado).
# -----------------------------------------------------------------------------

resource "aws_ssm_document" "deploy" {
  name            = "${var.project_name}-${var.environment}-deploy"
  document_type   = "Command"
  document_format = "JSON"
  target_type     = "/"

  content = jsonencode({
    schemaVersion = "2.2"
    description   = "Despliega el SHA indicado en el stack Docker Compose de la instancia."
    parameters = {
      sha = {
        type    = "String"
        default = ""
      }
      REPOURL = {
        type    = "String"
        default = "https://github.com/${var.github_repository}.git"
      }
      DEPLOYPATH = {
        type    = "String"
        default = "/opt/utttgo"
      }
      GITHUBTOKEN = {
        type    = "String"
        default = ""
      }
    }
    mainSteps = [
      {
        action = "aws:runShellScript"
        name   = "deployStack"
        inputs = {
          runCommand = [file("${path.module}/../ssm/deploy.sh")]
        }
      }
    ]
  })
}

# -----------------------------------------------------------------------------
# Compute: one t3.micro with Elastic IP, IMDSv2 and Docker bootstrap
# -----------------------------------------------------------------------------

resource "aws_cloudwatch_log_group" "app" {
  name              = "${var.app_log_group_name}/${var.environment}"
  retention_in_days = var.log_retention_days
}

locals {
  docker_bootstrap = <<-EOT
    #!/bin/bash
    set -euo pipefail
    apt-get update
    apt-get install -y docker.io docker-compose-v2
    systemctl enable --now docker
    usermod -aG docker ubuntu
    mkdir -p /opt/utttgo
  EOT
}

resource "aws_instance" "api" {
  ami                         = data.aws_ami.ubuntu_2404.id
  instance_type               = var.instance_type
  vpc_security_group_ids      = [aws_security_group.api.id]
  iam_instance_profile        = aws_iam_instance_profile.ec2.name
  associate_public_ip_address = true

  metadata_options {
    http_tokens = "required"
    # Hop limit 2 permite que los contenedores del Compose alcancen IMDS
    # (Instance Profile) sin access keys dentro de la imagen.
    http_put_response_hop_limit = 2
  }

  root_block_device {
    volume_type           = "gp3"
    volume_size           = var.root_volume_size
    encrypted             = true
    delete_on_termination = true
  }

  user_data = var.install_docker ? local.docker_bootstrap : null

  tags = {
    Name = "${var.project_name}-${var.environment}-api"
  }

  lifecycle {
    # La AMI no se actualiza en caliente: cambiarla obliga a recrear la instancia
    # y perderia el .env y el stack desplegados. El rollover de AMI se decide
    # explicitamente en una ventana de mantenimiento (DEC-020).
    # El user_data solo se aplica en el lanzamiento: el proveedor no puede
    # cambiarlo en una instancia en ejecucion sin reiniciarla.
    ignore_changes = [ami, user_data]
  }
}

resource "aws_eip" "api" {
  instance = aws_instance.api.id
  domain   = "vpc"
}

# -----------------------------------------------------------------------------
# Storage: private images bucket with CORS for presigned traffic
# -----------------------------------------------------------------------------

resource "random_id" "images_suffix" {
  byte_length = 4
}

locals {
  images_bucket_name = coalesce(
    var.images_bucket_suffix != null ? "${var.project_name}-images-${var.images_bucket_suffix}" : null,
    "${var.project_name}-images-${random_id.images_suffix.hex}"
  )
}

resource "aws_s3_bucket" "images" {
  bucket        = local.images_bucket_name
  force_destroy = var.bucket_force_destroy
}

resource "aws_s3_bucket_ownership_controls" "images" {
  bucket = aws_s3_bucket.images.id

  rule {
    object_ownership = "BucketOwnerEnforced"
  }
}

resource "aws_s3_bucket_public_access_block" "images" {
  bucket = aws_s3_bucket.images.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "images" {
  bucket = aws_s3_bucket.images.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_cors_configuration" "images" {
  # Sin origenes no hay regla CORS: el bucket queda inaccesible desde navegadores
  # y el cliente movil nativo (sin cabecera Origin) sigue funcionando (DEC-020).
  count = length(var.allowed_origins) > 0 ? 1 : 0

  bucket = aws_s3_bucket.images.id

  dynamic "cors_rule" {
    for_each = length(var.allowed_origins) > 0 ? [1] : []
    content {
      allowed_headers = ["*"]
      allowed_methods = ["PUT", "GET", "HEAD"]
      allowed_origins = var.allowed_origins
      expose_headers  = ["ETag", "x-amz-request-id"]
      max_age_seconds = 3000
    }
  }
}

# -----------------------------------------------------------------------------
# Cost guard: monthly budget alert that evidences the $0 objective.
# El presupuesto se crea SIEMPRE (la variable de correo es obligatoria): si el
# apply olvidara el valor, Terraform falla en vez de destruir silenciosamente la
# alerta. prevent_destroy impide borrarlo por accidente (DEC-020).
# -----------------------------------------------------------------------------

resource "aws_budgets_budget" "zero_cost_guard" {
  name         = "${var.project_name}-${var.environment}-zero-cost-guard"
  budget_type  = "COST"
  limit_amount = tostring(var.budget_limit_usd)
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  notification {
    comparison_operator = "GREATER_THAN"
    notification_type   = "ACTUAL"
    threshold           = 0
    threshold_type      = "PERCENTAGE"
    subscriber_email_addresses = [
      var.budget_alert_email,
    ]
  }

  lifecycle {
    prevent_destroy = true
  }
}

# -----------------------------------------------------------------------------
# Outputs (kept inline per approved ticket main.tf + variables.tf only)
# -----------------------------------------------------------------------------

output "instance_id" {
  description = "EC2 instance running the API containers."
  value       = aws_instance.api.id
}

output "instance_public_ip" {
  description = "Temporary public IP of the instance."
  value       = aws_instance.api.public_ip
}

output "elastic_ip" {
  description = "Fixed public IP for the API endpoint."
  value       = aws_eip.api.public_ip
}

output "images_bucket_name" {
  description = "Private S3 bucket for presigned product images."
  value       = aws_s3_bucket.images.id
}

output "log_group_name" {
  description = "CloudWatch Logs group for the API lifecycle logs."
  value       = aws_cloudwatch_log_group.app.name
}

output "budget_name" {
  description = "AWS Budget que vigila el objetivo de costo $0."
  value       = aws_budgets_budget.zero_cost_guard.name
}

output "github_deploy_user" {
  description = "Usuario IAM de privilegio minimo que CD-1 usa para desplegar por SSM (la clave vive en los secretos del repositorio)."
  value       = aws_iam_user.github_deploy.name
}

output "ssm_document_names" {
  description = "Documentos SSM gestionados (despliegue CD-1 por send-command)."
  value = {
    deploy = aws_ssm_document.deploy.name
  }
}
