#!/usr/bin/env bash
# =============================================================================
# UTTT-GO — despliegue del stack en la instancia EC2.
#
# Se ejecuta en la instancia por AWS Systems Manager. Desde 2026-11-18 la
# imagen se construye en GitHub Actions y se publica en GHCR con el tag del
# commit: la instancia solo descarga y levanta contenedores (DEC-022). El
# build de .NET en una t3.micro (1 vCPU / 1 GiB) tardaba ~29-45 min y hacia
# fallar el CD por timeout.
#
# Canales de invocacion (ambos soportados):
#   1. Inline (CD-1): el runner envia el contenido de este archivo como
#      AWS-RunShellScript y exporta DEPLOY_SHA / API_IMAGE.
#   2. Documento SSM "utttgo-dev-deploy" (Terraform): los parametros se
#      inyectan por sustitucion textual {{ ... }}.
#
# Entradas (por variable de entorno o por el documento SSM):
#   DEPLOY_SHA    commit exacto a desplegar
#   REPOURL       URL del repositorio
#   DEPLOYPATH    ruta del checkout en la instancia
#   API_IMAGE     imagen a desplegar (por defecto ghcr.io/<org>/utttgo-api:<sha>)
#   GHCR_USERNAME usuario de GHCR (opcional; solo si la imagen es privada)
#   GHCR_TOKEN    token de solo lectura de GHCR (opcional; nunca se muestra)
#   GITHUBTOKEN   token de solo lectura del repositorio (opcional)
# =============================================================================
set -euo pipefail

# Systems Manager ejecuta el script como root con un entorno minimo: sin HOME
# git no puede resolver el archivo de configuracion global.
export HOME="${HOME:-/root}"

# Los parametros del documento SSM se inyectan por sustitucion textual
# {{ ... }}. Si las variables ya vienen del entorno (ejecucion inline), se
# respetan.
[ -n "${DEPLOY_SHA:-}" ] || DEPLOY_SHA='{{ sha }}'
[ -n "${REPOURL:-}" ] || REPOURL='{{ REPOURL }}'
[ -n "${DEPLOYPATH:-}" ] || DEPLOYPATH='{{ DEPLOYPATH }}'
[ -n "${GITHUBTOKEN:-}" ] || GITHUBTOKEN='{{ GITHUBTOKEN }}'

# Si el documento SSM no llego a sustituir {{ ... }} (o el canal inyecta la
# plantilla literal), el valor no es una URL ni una ruta: se trata como vacio
# para que manden los defaults de abajo. Sin este filtro el script crearia un
# directorio llamado "{{ DEPLOYPATH }}" y fallaria el clone con
# "repository '{{ REPOURL }}' does not exist" (run CD 44463627005).
case "$REPOURL" in "{{"*) REPOURL="";; esac
case "$DEPLOYPATH" in "{{"*) DEPLOYPATH="";; esac

DEPLOY_PATH="${DEPLOYPATH:-/opt/utttgo}"
TARGET_SHA="${DEPLOY_SHA:-}"
REPO_URL="${REPOURL:-https://github.com/utttgo-2026/utttgo-backend.git}"
DEPLOY_USER="${DEPLOY_USER:-ubuntu}"
FIREBASE_CREDENTIALS_HOST_PATH="${FIREBASE_CREDENTIALS_HOST_PATH:-$DEPLOY_PATH/secrets/firebase-credentials.json}"
FIREBASE_CREDENTIALS_PATH="${FIREBASE_CREDENTIALS_PATH:-/run/secrets/firebase-credentials.json}"
export FIREBASE_CREDENTIALS_HOST_PATH FIREBASE_CREDENTIALS_PATH

case "$TARGET_SHA" in
  *'{{'*)
    echo "No se recibio el SHA a desplegar (DEPLOY_SHA)." >&2
    exit 1
    ;;
esac

if [ ! -e "$DEPLOY_PATH/.git" ]; then
  mkdir -p "$DEPLOY_PATH"
  if [ -n "$(ls -A "$DEPLOY_PATH" 2>/dev/null)" ]; then
    echo "$DEPLOY_PATH no es un repositorio Git y no esta vacio; no se puede clonar encima." >&2
    exit 1
  fi
  echo "Clon inicial del repositorio en $DEPLOY_PATH"
  git clone "$REPO_URL" "$DEPLOY_PATH"
fi

cd "$DEPLOY_PATH"
git remote set-url origin "$REPO_URL"

# El checkout pertenece al usuario `ubuntu` y SSM corre como root: git
# rechaza operar sobre un repositorio de otro propietario ("dubious
# ownership"), asi que se declara este checkout como seguro.
git config --global --add safe.directory "$DEPLOY_PATH"

# El canal inyecta DEPLOY_SHA / API_IMAGE y, si el paquete GHCR no es
# publico, GHCR_USERNAME / GHCR_TOKEN. El token del repositorio no viaja:
# el repositorio es publico, asi que git lo opera sin credenciales. Se
# iguala a vacio porque el script corre con `set -u`.
GITHUB_TOKEN="${GITHUB_TOKEN:-}"

# El token nunca se escribe en disco ni en el historial: se usa solo como
# cabecera extra de git durante el fetch.
if [ -n "$GITHUB_TOKEN" ]; then
  auth_header="$(printf 'x-access-token:%s' "$GITHUB_TOKEN" | base64 | tr -d '\n')"
  git_auth() {
    GIT_CONFIG_COUNT=1 \
    GIT_CONFIG_KEY_0="http.https://github.com/.extraheader" \
    GIT_CONFIG_VALUE_0="AUTHORIZATION: basic $auth_header" \
    git "$@"
  }
  echo "Autenticacion del repositorio: token de solo lectura."
elif [ -f /root/.ssh/id_github_deploy ]; then
  install -d -m 700 /root/.ssh
  chmod 600 /root/.ssh/id_github_deploy
  if ! grep -q "identityfile /root/.ssh/id_github_deploy" /root/.ssh/config 2>/dev/null; then
    cat > /root/.ssh/config <<EOF
Host github.com
  HostName github.com
  User git
  IdentityFile /root/.ssh/id_github_deploy
  IdentitiesOnly yes
  StrictHostKeyChecking accept-new
EOF
    chmod 600 /root/.ssh/config
  fi
  git_auth() { git "$@"; }
  echo "Autenticacion del repositorio: deploy key de solo lectura."
else
  # Repositorio publico: fetch de solo lectura sin credenciales ni secretos.
  git_auth() { git "$@"; }
  echo "Autenticacion del repositorio: acceso publico (sin token ni deploy key)."
fi

if ! git_auth fetch --prune origin "$TARGET_SHA"; then
  git_auth fetch --prune origin main
fi
if ! git cat-file -e "${TARGET_SHA}^{commit}" 2>/dev/null; then
  echo "El commit $TARGET_SHA no esta disponible en la instancia." >&2
  exit 1
fi
git reset --hard "$TARGET_SHA"

actual="$(git rev-parse HEAD)"
if [ "$actual" != "$TARGET_SHA" ]; then
  echo "El commit desplegado ($actual) no coincide con el SHA objetivo ($TARGET_SHA)." >&2
  exit 1
fi
echo "Commit desplegado: $actual"

if [ ! -f "$DEPLOY_PATH/.env" ]; then
  echo "::warning::No existe .env en $DEPLOY_PATH: Atlas, Supabase y S3 deben estar configurados en la instancia."
fi

validate_firebase_credentials() {
  local credentials_file="$FIREBASE_CREDENTIALS_HOST_PATH"
  if [ ! -e "$credentials_file" ]; then
    echo "::warning::No existe el archivo de credenciales Firebase; FCM queda desactivado."
    return 1
  fi
  if [ ! -f "$credentials_file" ]; then
    echo "::warning::La ruta de credenciales Firebase no es un archivo; FCM queda desactivado."
    return 1
  fi
  if ! python3 -m json.tool "$credentials_file" >/dev/null 2>&1; then
    echo "::warning::El archivo de credenciales Firebase no contiene JSON valido; FCM queda desactivado."
    return 1
  fi
  local mode
  mode="$(stat -c '%a' "$credentials_file" 2>/dev/null || true)"
  if [ -z "$mode" ] || ! python3 - "$mode" <<'PY'
import sys
mode = int(sys.argv[1], 8)
raise SystemExit(0 if mode & 0o040 else 1)
PY
  then
    echo "::warning::El archivo de credenciales Firebase no es legible por grupo; FCM queda desactivado."
    return 1
  fi
  return 0
}

firebase_credentials_valid=0
if validate_firebase_credentials; then
  firebase_credentials_valid=1
fi

# -----------------------------------------------------------------------------
# Lock de despliegue: dos despliegues simultaneos se contradicen.
# -----------------------------------------------------------------------------
mkdir -p "$DEPLOY_PATH"
exec 9>"$DEPLOY_PATH/.deploy.lock"
if ! flock -n 9; then
  echo "Ya hay un despliegue en curso en la instancia ($DEPLOY_PATH/.deploy.lock)." >&2
  exit 1
fi

# -----------------------------------------------------------------------------
# Swap de 2 GB: el publish de .NET en 1 GiB de RAM puede agotar la memoria del
# host. Idempotente: si ya existe o ya esta activo, no se toca.
# -----------------------------------------------------------------------------
ensure_swap() {
  local swap_file=/swapfile
  local size_mb=2048
  if swapon --show=NAME --noheadings 2>/dev/null | grep -q "$swap_file"; then
    echo "Swap ya activo en $swap_file."
    return 0
  fi
  if [ ! -f "$swap_file" ]; then
    if ! fallocate -l "${size_mb}M" "$swap_file" 2>/dev/null; then
      dd if=/dev/zero of="$swap_file" bs=1M count="$size_mb" status=none
    fi
    chmod 600 "$swap_file"
    mkswap "$swap_file" >/dev/null
  fi
  swapon "$swap_file"
  if ! grep -q "^$swap_file " /etc/fstab; then
    echo "$swap_file none swap sw 0 0" >> /etc/fstab
  fi
  echo "Swap de ${size_mb} MB activo en $swap_file."
}
ensure_swap

# -----------------------------------------------------------------------------
# Imagen: se construye en GitHub Actions y se publica en GHCR por commit.
# La instancia solo descarga y levanta; no compila .NET.
# -----------------------------------------------------------------------------
API_IMAGE="${API_IMAGE:-ghcr.io/utttgo-2026/utttgo-api:$TARGET_SHA}"
export API_IMAGE
echo "Imagen a desplegar: $API_IMAGE"

if [ -n "${GHCR_TOKEN:-}" ]; then
  # El token se envia por stdin a docker login: nunca se imprime ni queda en
  # el historial del script.
  printf '%s' "$GHCR_TOKEN" | docker login ghcr.io -u "${GHCR_USERNAME:-utttgo-2026}" --password-stdin >/dev/null
  echo "Acceso a GHCR configurado en la instancia (token no mostrado)."
fi

# TLS: valores opcionales del .env (si faltan, provision-tls.sh usa
# ${PUBLIC_IP}.nip.io). Un tls.conf sin certificados impide que Nginx arranque:
# se retira antes del up y vuelve a emitirse al final del despliegue.
if [ -f "$DEPLOY_PATH/.env" ]; then
  _tls_domain="$(sed -n 's/^TLS_DOMAIN=//p' "$DEPLOY_PATH/.env" | head -n 1 | tr -d "\"'\r")"
  _tls_email="$(sed -n 's/^TLS_EMAIL=//p' "$DEPLOY_PATH/.env" | head -n 1 | tr -d "\"'\r")"
  [ -n "$_tls_domain" ] && TLS_DOMAIN="$_tls_domain"
  [ -n "$_tls_email" ] && TLS_EMAIL="$_tls_email"
  export TLS_DOMAIN TLS_EMAIL
fi
bash scripts/tls/provision-tls.sh guard

docker compose pull --ignore-buildable redis nginx
if ! docker compose pull api; then
  echo "No se pudo descargar $API_IMAGE. Si el paquete es privado, hay que autenticar GHCR en la instancia." >&2
  exit 1
fi

if [ "$firebase_credentials_valid" = "1" ] && ! docker compose run --rm --no-deps --entrypoint /bin/sh api -c 'test -r "$FIREBASE_CREDENTIALS_PATH"'; then
  echo "::warning::El usuario app no puede leer las credenciales Firebase montadas; FCM queda desactivado."
fi

docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --no-build --remove-orphans --scale api=2

deadline=$(( $(date +%s) + 240 ))
replicas_ok=0
while [ "$(date +%s)" -lt "$deadline" ]; do
  # Se usa el formato JSON de `docker compose ps` (no plantillas de Go): las
  # llaves dobles de un --format de docker inspect las interpreta SSM como
  # referencias a parametros del documento.
  total_replicas="$(docker compose ps -q api | wc -l | tr -d ' ')"
  healthy_replicas="$(docker compose ps --format json api 2>/dev/null | grep -c '"Health":"healthy"' || true)"
  echo "Replicas: $healthy_replicas/$total_replicas sanas"
  if [ "$total_replicas" = "2" ] && [ "$healthy_replicas" = "2" ]; then
    replicas_ok=1
    break
  fi
  sleep 5
done

if [ "$replicas_ok" != "1" ]; then
  echo "Las replicas de la API no quedaron sanas." >&2
  docker compose ps >&2
  echo "Los logs de los contenedores se publican en CloudWatch Logs (grupo /utttgo/api/dev); se consultan con: aws logs tail /utttgo/api/dev --since 10m" >&2
  exit 1
fi

# Limpieza de imagenes huerfanos: el disco de la instancia es pequeño y cada
# version anterior de la imagen se queda occupying espacio.
docker image prune -f >/dev/null 2>&1 || true

# La sonda operativa del backend es GET /health (no /api/v1/health).
health_code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 http://127.0.0.1/health || echo 000)"
echo "HTTP /health -> $health_code"
case "$health_code" in
  200) ;;
  503) echo "::warning::La sonda /health devolvio 503: el despliegue esta activo pero MongoDB o Redis no responden." ;;
  *) echo "::warning::La sonda /health devolvio $health_code; se requiere verificacion manual." ;;
esac

# TLS (Let's Encrypt): emision o renovacion idempotente; nunca rompe el
# despliegue si falla (el stack sigue sirviendo por HTTP).
tls_rc=0
bash scripts/tls/provision-tls.sh provision || tls_rc=$?
case "$tls_rc" in
  0)
    if [ -f nginx/conf.d/tls.conf ]; then
      tls_domain="$(bash scripts/tls/provision-tls.sh print-domain)"
      https_code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 "https://${tls_domain}/health" || echo 000)"
      echo "HTTPS https://${tls_domain}/health -> $https_code"
      case "$https_code" in
        200|503) ;;
        *) echo "::warning::Smoke HTTPS devolvio $https_code; verificacion manual requerida." ;;
      esac
    fi
    ;;
  2) echo "TLS omitido (SKIP_TLS=1)." ;;
  *) echo "::warning::Aprovisionamiento TLS fallo (codigo $tls_rc); el stack sigue en HTTP." ;;
esac

if id "$DEPLOY_USER" >/dev/null 2>&1; then
  chown -R "$DEPLOY_USER":"$DEPLOY_USER" "$DEPLOY_PATH"
fi

echo "SHA desplegado (git rev-parse HEAD): $(git -C "$DEPLOY_PATH" rev-parse HEAD)"
docker compose ps
