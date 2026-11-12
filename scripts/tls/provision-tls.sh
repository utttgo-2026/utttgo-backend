#!/usr/bin/env bash
# =============================================================================
# UTTT-GO — aprovisionamiento TLS (Let's Encrypt) en la instancia EC2.
#
# Emite (o valida) el certificado por desafio ACME webroot para TLS_DOMAIN
# (por defecto ${PUBLIC_IP}.nip.io, DNS gratuito), renderiza
# nginx/conf.d/tls.conf desde la plantilla, recarga Nginx y deja el hook de
# renovacion con la ruta absoluta del checkout.
#
# Uso:
#   bash scripts/tls/provision-tls.sh guard        # solo consistencia tls.conf/certs
#   bash scripts/tls/provision-tls.sh provision    # emision + render + reload + hook
#   bash scripts/tls/provision-tls.sh print-domain # dominio efectivo
#
# Entradas (variables de entorno):
#   PUBLIC_IP   IP elastica (default 3.129.142.54)
#   TLS_DOMAIN  dominio a certificar (default ${PUBLIC_IP}.nip.io)
#   TLS_EMAIL   correo ACME (opcional; si falta, registro sin correo)
#   SKIP_TLS=1  sale sin hacer nada (codigo 2 en provision)
#   REPO_PATH   checkout (default: raiz del repositorio)
#
# Codigos de salida de `provision`:
#   0  tls.conf presente y certificado vigente
#   1  fallo de emision/render: el stack sigue sirviendo por HTTP
#   2  omitido (SKIP_TLS=1)
# =============================================================================
set -euo pipefail

PUBLIC_IP="${PUBLIC_IP:-3.129.142.54}"
TLS_DOMAIN="${TLS_DOMAIN:-${PUBLIC_IP}.nip.io}"
TLS_EMAIL="${TLS_EMAIL:-}"
SKIP_TLS="${SKIP_TLS:-0}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_PATH="${REPO_PATH:-$(cd "$SCRIPT_DIR/../.." && pwd)}"

MODE="${1:-provision}"

case "$MODE" in
    guard|provision|print-domain) ;;
    -h|--help) sed -n '2,26p' "$0"; exit 0 ;;
    *) echo "Modo desconocido: $MODE (use guard|provision|print-domain)" >&2; exit 1 ;;
esac

if [ "$MODE" = "print-domain" ]; then
    echo "$TLS_DOMAIN"
    exit 0
fi

cd "$REPO_PATH"
TLS_CONF="nginx/conf.d/tls.conf"
CERT_FULLCHAIN="/etc/letsencrypt/live/${TLS_DOMAIN}/fullchain.pem"
CERT_PRIVKEY="/etc/letsencrypt/live/${TLS_DOMAIN}/privkey.pem"

guard() {
    # Un tls.conf sin certificados impide que Nginx arranque: se retira y el
    # stack sigue en HTTP hasta que provision emita el certificado.
    if [ -f "$TLS_CONF" ] && { [ ! -f "$CERT_FULLCHAIN" ] || [ ! -f "$CERT_PRIVKEY" ]; }; then
        echo "[aviso] ${TLS_CONF} existe pero faltan certificados para ${TLS_DOMAIN}; se retira para no bloquear a Nginx."
        rm -f "$TLS_CONF"
    fi
}

if [ "$MODE" = "guard" ]; then
    guard
    exit 0
fi

if [ "$SKIP_TLS" = "1" ]; then
    echo "SKIP_TLS=1: TLS omitido."
    exit 2
fi

guard

cert_valid() {
    [ -f "$CERT_FULLCHAIN" ] && [ -f "$CERT_PRIVKEY" ] &&
        openssl x509 -in "$CERT_FULLCHAIN" -noout -checkend 2592000 >/dev/null 2>&1
}

if ! command -v openssl >/dev/null 2>&1; then
    echo "openssl no esta instalado; instalando."
    export DEBIAN_FRONTEND=noninteractive
    apt-get update -y
    apt-get install -y openssl
fi

if ! cert_valid; then
    mkdir -p certbot/www/.well-known/acme-challenge
    chmod -R 755 certbot/www

    resolved="$(getent hosts "$TLS_DOMAIN" 2>/dev/null | awk '{print $1}' | head -n 1 || true)"
    if [ -n "$resolved" ] && [ "$resolved" != "$PUBLIC_IP" ]; then
        echo "[aviso] ${TLS_DOMAIN} resuelve a ${resolved}, no a ${PUBLIC_IP}. Corrige el registro DNS antes de emitir."
    fi

    if ! command -v certbot >/dev/null 2>&1; then
        echo "certbot no esta instalado; instalando."
        export DEBIAN_FRONTEND=noninteractive
        apt-get update -y
        apt-get install -y certbot
    fi

    email_args=(--register-unsafely-without-email)
    if [ -n "$TLS_EMAIL" ]; then
        email_args=(--email "$TLS_EMAIL")
    fi

    if ! certbot certonly \
        --webroot \
        --webroot-path "$REPO_PATH/certbot/www" \
        --domain "$TLS_DOMAIN" \
        --agree-tos \
        --non-interactive \
        "${email_args[@]}"; then
        echo "[aviso] La emision del certificado para ${TLS_DOMAIN} fallo; el stack sigue en HTTP."
        exit 1
    fi
fi

if ! cert_valid; then
    echo "[aviso] Certificado ausente o por vencer tras la emision; se omite el render TLS." >&2
    exit 1
fi

sed "s/__DOMAIN__/${TLS_DOMAIN}/g" nginx/conf.d/tls.conf.template > "$TLS_CONF"

install -d /etc/letsencrypt/renewal-hooks/deploy
rm -f /etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh
cat > /etc/letsencrypt/renewal-hooks/deploy/utttgo-reload-nginx.sh <<HOOK
#!/usr/bin/env bash
set -euo pipefail
cd "${REPO_PATH}"
docker compose exec -T nginx nginx -s reload
HOOK
chmod 755 /etc/letsencrypt/renewal-hooks/deploy/utttgo-reload-nginx.sh

if [ -n "$(docker compose ps -q nginx 2>/dev/null || true)" ]; then
    if ! docker compose exec -T nginx nginx -t; then
        echo "[aviso] nginx -t rechazo la configuracion TLS; se retira ${TLS_CONF}." >&2
        rm -f "$TLS_CONF"
        exit 1
    fi
    docker compose exec -T nginx nginx -s reload
else
    echo "Nginx no esta corriendo: tls.conf queda listo para el proximo arranque."
fi

echo "TLS habilitado: https://${TLS_DOMAIN}"
exit 0
