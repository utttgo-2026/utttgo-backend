#!/usr/bin/env bash
# =============================================================================
# UTTT-GO Backend — setup de despliegue en EC2 (Ubuntu 24.04)
#
# Instala Docker + Docker Compose + Certbot, despliega la API en 2 replicas,
# Redis y Nginx, y opcionalmente emite el certificado TLS (Let's Encrypt).
#
# Uso:
#   sudo bash setup.sh --domain api.tudominio.mx --email tu@correo.com
#   sudo bash setup.sh --skip-tls              # solo HTTP por IP
#
# Requisitos: el dominio debe tener un registro A apuntando a la IP pública de
# la instancia. Let's Encrypt no emite certificados para IPs desnudas.
# =============================================================================
set -euo pipefail

DOMAIN=""
EMAIL=""
PUBLIC_IP="${PUBLIC_IP:-3.129.142.54}"
REPO_URL="${REPO_URL:-}"
SKIP_TLS=0
DEPLOY_USER="${DEPLOY_USER:-ubuntu}"

log()  { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
warn() { printf '\033[1;33m[aviso] %s\033[0m\n' "$*" >&2; }
die()  { printf '\033[1;31m[error] %s\033[0m\n' "$*" >&2; exit 1; }

usage() {
    sed -n '2,18p' "$0"
    exit 0
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --domain)  DOMAIN="${2:-}"; shift 2 ;;
        --email)   EMAIL="${2:-}"; shift 2 ;;
        --ip)      PUBLIC_IP="${2:-}"; shift 2 ;;
        --repo)    REPO_URL="${2:-}"; shift 2 ;;
        --skip-tls) SKIP_TLS=1; shift ;;
        -h|--help) usage ;;
        *) die "Argumento desconocido: $1" ;;
    esac
done

if [[ $EUID -ne 0 ]]; then
    exec sudo -E bash "$0" ${DOMAIN:+--domain "$DOMAIN"} ${EMAIL:+--email "$EMAIL"} --ip "$PUBLIC_IP" ${REPO_URL:+--repo "$REPO_URL"} $([[ $SKIP_TLS -eq 1 ]] && echo --skip-tls)
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

export DEBIAN_FRONTEND=noninteractive

# -----------------------------------------------------------------------------
# 1. Paquetes base y Docker
# -----------------------------------------------------------------------------
log "1/7 Instalando Docker, Docker Compose y Certbot"
apt-get update -y
apt-get install -y ca-certificates curl gnupg git certbot

if ! command -v docker >/dev/null 2>&1; then
    log "Instalando Docker Engine desde el repositorio oficial"
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    chmod a+r /etc/apt/keyrings/docker.asc
    . /etc/os-release
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu ${UBUNTU_CODENAME:-${VERSION_CODENAME}} stable" > /etc/apt/sources.list.d/docker.list
    apt-get update -y
    if ! apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin; then
        warn "Repositorio oficial no disponible; usando los paquetes de Ubuntu"
        apt-get install -y docker.io docker-compose-v2
    fi
else
    log "Docker ya instalado; se instala el plugin Compose si falta"
    apt-get install -y docker-compose-plugin || apt-get install -y docker-compose-v2 || true
fi

systemctl enable --now docker
usermod -aG docker "$DEPLOY_USER" 2>/dev/null || true

docker --version
docker compose version

# -----------------------------------------------------------------------------
# 2. Código fuente
# -----------------------------------------------------------------------------
log "2/7 Verificando el código fuente"
if [[ ! -f docker-compose.yml ]]; then
    [[ -n "$REPO_URL" ]] || die "No se encuentra docker-compose.yml. Clona el repositorio o usa --repo <url>."
    git clone "$REPO_URL" utttgo-backend
    cd utttgo-backend
fi

# -----------------------------------------------------------------------------
# 3. Configuración de entorno (sin secretos en Git)
# -----------------------------------------------------------------------------
log "3/7 Preparando el archivo .env"
if [[ ! -f .env ]]; then
    cp .env.example .env
    warn "Se creó .env a partir de .env.example: completa MONGODB_URI (Atlas), SUPABASE_*, S3_BUCKET_IMAGES y el resto de valores."
else
    log ".env ya existe; no se modifica"
fi
for required in MONGODB_URI SUPABASE_URL SUPABASE_ANON_KEY; do
    if ! grep -qE "^${required}=.+" .env; then
        warn "Falta el valor de ${required} en .env: la API responderá 503 en las rutas que dependan de ese servicio."
    fi
done

mkdir -p certbot/www/.well-known/acme-challenge
chmod -R 755 certbot/www

# -----------------------------------------------------------------------------
# 4. Imágenes y despliegue
# -----------------------------------------------------------------------------
log "4/7 Construyendo la imagen de la API y descargando las imágenes base"
docker compose pull --ignore-buildable redis nginx
docker compose build --pull api

log "5/7 Levantando el stack (2 replicas de la API, Redis y Nginx)"
docker compose up -d --scale api=2
docker compose ps

log "Esperando a que las réplicas de la API estén sanas"
for attempt in $(seq 1 30); do
    healthy=$(docker compose ps --format json api 2>/dev/null | grep -c '"Health":"healthy"' || true)
    if [[ "${healthy:-0}" -ge 2 ]]; then
        break
    fi
    sleep 5
done
docker compose ps

# -----------------------------------------------------------------------------
# 5. TLS con Let's Encrypt
# -----------------------------------------------------------------------------
if [[ $SKIP_TLS -eq 1 ]]; then
    log "6/7 TLS omitido (--skip-tls): el API queda disponible solo por HTTP en ${PUBLIC_IP}"
elif [[ -z "$DOMAIN" || -z "$EMAIL" ]]; then
    log "6/7 TLS omitido: faltan --domain y --email (el API queda disponible solo por HTTP en ${PUBLIC_IP})"
else
    log "6/7 Emitiendo certificado TLS para ${DOMAIN}"
    resolved="$(getent hosts "$DOMAIN" | awk '{print $1}' | head -n 1 || true)"
    if [[ -n "$resolved" && "$resolved" != "$PUBLIC_IP" ]]; then
        warn "El dominio ${DOMAIN} resuelve a ${resolved}, no a ${PUBLIC_IP}. Corrige el registro DNS antes de continuar."
    fi

    certbot certonly \
        --webroot \
        --webroot-path "$(pwd)/certbot/www" \
        --domain "$DOMAIN" \
        --email "$EMAIL" \
        --agree-tos \
        --non-interactive \
        --no-eff-email \
        || warn "La emisión del certificado falló; el API sigue disponible por HTTP."

    if [[ -f "/etc/letsencrypt/live/${DOMAIN}/fullchain.pem" ]]; then
        sed "s/__DOMAIN__/${DOMAIN}/g" nginx/conf.d/tls.conf.template > nginx/conf.d/tls.conf
        install -d /etc/letsencrypt/renewal-hooks/deploy
        cat > /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh <<'HOOK'
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.." 2>/dev/null || true
docker compose exec -T nginx nginx -s reload
HOOK
        chmod +x /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh
        docker compose exec -T nginx nginx -t
        docker compose exec -T nginx nginx -s reload
        log "TLS habilitado: https://${DOMAIN}"
    fi
fi

# -----------------------------------------------------------------------------
# 6. Firewall
# -----------------------------------------------------------------------------
log "7/7 Ajustando el firewall local"
if command -v ufw >/dev/null 2>&1 && ufw status | grep -q "Status: active"; then
    ufw allow 80/tcp
    ufw allow 443/tcp
    log "Reglas 80/TCP y 443/TCP permitidas en ufw"
else
    log "ufw inactivo o ausente; el Security Group de AWS ya expone 80/443"
fi

# -----------------------------------------------------------------------------
# 7. Verificación
# -----------------------------------------------------------------------------
log "Verificación final"
printf 'API por HTTP (debe responder 401 sin token): '
curl -s -o /dev/null -w '%{http_code}\n' "http://127.0.0.1/api/v1/auth/me" || true
if [[ -n "$DOMAIN" && -f "/etc/letsencrypt/live/${DOMAIN}/fullchain.pem" ]]; then
    printf 'API por HTTPS: '
    curl -s -o /dev/null -w '%{http_code}\n' "https://${DOMAIN}/api/v1/auth/me" || true
fi

docker compose ps
log "API desplegada. Comandos útiles: docker compose logs -f api | docker compose ps | docker compose up -d --scale api=2"
warn "El bucket S3, MongoDB Atlas y Supabase siguen siendo externos: revisa que .env tenga los valores reales."
