#!/usr/bin/env bash
# =============================================================================
# UTTT-GO — despliegue del stack en la instancia EC2.
#
# Se ejecuta en la instancia mediante el documento SSM "${project}-${env}-deploy"
# (infra/terraform/main.tf), invocado por CD-1 con `aws ssm send-command`.
# Sustituye al despliegue por SSH: el puerto 22 esta cerrado y la autenticacion
# del repositorio es de solo lectura. El repositorio es publico, asi que el
# fetch no necesita credenciales; si en el futuro pasa a privado, se usa el
# token de solo lectura del job (GITHUB_TOKEN) como cabecera extra de git o una
# deploy key de solo lectura ya instalada en la instancia.
#
# Entradas (parametros del documento SSM, expuestos como variables de entorno):
#   SHA          commit exacto a desplegar
#   REPOURL      URL del repositorio privado
#   DEPLOYPATH   ruta del checkout en la instancia
#   GITHUBTOKEN  token de solo lectura del job (opcional): si falta, se usa la
#                deploy key de solo lectura instalada en la instancia
# =============================================================================
set -euo pipefail

# Los parametros del documento SSM se inyectan por sustitucion textual {{ ... }}.
# Si las variables ya vienen del entorno (ejecucion manual), se respetan.
[ -n "${SHA:-}" ] || SHA='{{ sha }}'
[ -n "${REPOURL:-}" ] || REPOURL='{{ REPOURL }}'
[ -n "${DEPLOYPATH:-}" ] || DEPLOYPATH='{{ DEPLOYPATH }}'
[ -n "${GITHUBTOKEN:-}" ] || GITHUBTOKEN='{{ GITHUBTOKEN }}'

DEPLOY_PATH="${DEPLOYPATH:-/opt/utttgo}"
TARGET_SHA="${SHA:-}"
REPO_URL="${REPOURL:-https://github.com/utttgo-2026/utttgo-backend.git}"
DEPLOY_KEY="${DEPLOY_KEY:-/root/.ssh/id_github_deploy}"
DEPLOY_USER="${DEPLOY_USER:-ubuntu}"
GITHUB_TOKEN="${GITHUBTOKEN:-}"

if [ -z "$TARGET_SHA" ]; then
  echo "SHA vacio: no se puede desplegar." >&2
  exit 1
fi

if ! command -v git >/dev/null 2>&1; then
  echo "git no esta instalado en la instancia; instalando."
  apt-get update -y
  apt-get install -y git
fi

# El comando SSM se ejecuta como root y el checkout pertenece al usuario
# `ubuntu` (el script devuelve la propiedad al final). Git rechaza por
# seguridad operar sobre un repositorio de otro propietario ("dubious
# ownership"), asi que se declara este checkout como seguro.
git config --global --add safe.directory "$DEPLOY_PATH"

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
  echo "Autenticacion del repositorio: token de solo lectura del job."
elif [ -f "$DEPLOY_KEY" ]; then
  install -d -m 700 /root/.ssh
  chmod 600 "$DEPLOY_KEY"
  if ! grep -q "identityfile $DEPLOY_KEY" /root/.ssh/config 2>/dev/null; then
    cat > /root/.ssh/config <<EOF
Host github.com
  HostName github.com
  User git
  IdentityFile $DEPLOY_KEY
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

mkdir -p "$DEPLOY_PATH"
if [ ! -e "$DEPLOY_PATH/.git" ]; then
  if [ -n "$(ls -A "$DEPLOY_PATH" 2>/dev/null)" ]; then
    echo "$DEPLOY_PATH no es un repositorio Git y no esta vacio; no se puede clonar encima." >&2
    exit 1
  fi
  echo "Clon inicial del repositorio en $DEPLOY_PATH"
  git_auth clone "$REPO_URL" "$DEPLOY_PATH"
fi

cd "$DEPLOY_PATH"
git remote set-url origin "$REPO_URL"

if ! git_auth fetch --prune origin "$TARGET_SHA"; then
  git_auth fetch --prune origin main
  git reset --hard FETCH_HEAD
else
  git reset --hard "$TARGET_SHA"
fi

actual="$(git rev-parse HEAD)"
if [ "$actual" != "$TARGET_SHA" ]; then
  echo "El commit desplegado ($actual) no coincide con el SHA objetivo ($TARGET_SHA)." >&2
  exit 1
fi
echo "Commit desplegado: $actual"

if [ ! -f "$DEPLOY_PATH/.env" ]; then
  echo "::warning::No existe .env en $DEPLOY_PATH: Atlas, Supabase y S3 deben estar configurados en la instancia."
fi

docker compose pull --ignore-buildable redis nginx
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build --remove-orphans --scale api=2

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

http_code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 http://127.0.0.1/api/v1/auth/me || echo 000)"
echo "HTTP /api/v1/auth/me -> $http_code"
case "$http_code" in
  200|401|503) ;;
  *)
    echo "Smoke test HTTP fallido (codigo $http_code)." >&2
    echo "Los logs de los contenedores se publican en CloudWatch Logs (grupo /utttgo/api/dev); se consultan con: aws logs tail /utttgo/api/dev --since 10m" >&2
    exit 1
    ;;
esac

health_code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 http://127.0.0.1/health || echo 000)"
echo "HTTP /health -> $health_code"
case "$health_code" in
  200) ;;
  503) echo "::warning::La sonda /health devolvio 503: el despliegue esta activo pero MongoDB o Redis no responden." ;;
  *) echo "::warning::La sonda /health devolvio $health_code; se requiere verificacion manual." ;;
esac

if id "$DEPLOY_USER" >/dev/null 2>&1; then
  chown -R "$DEPLOY_USER":"$DEPLOY_USER" "$DEPLOY_PATH"
fi

docker compose ps
