#!/usr/bin/env bash
set -Eeuo pipefail
[[ "${GITHUB_ACTIONS:-}" == true && "${RUNNER_ENVIRONMENT:-}" == github-hosted ]] || {
  echo 'Run this destructive container lifecycle test only on an ephemeral GitHub-hosted runner' >&2; exit 1;
}
if docker volume inspect hotelops_sql-data >/dev/null 2>&1 || [[ -n "$(docker ps -aq --filter label=com.docker.compose.project=hotelops)" ]]; then
  echo 'Existing HotelOps containers/data detected; CI test refused' >&2; exit 1;
fi
root=$(cd "$(dirname "$0")/.." && pwd)
private=$(mktemp -d)
export ENV_FILE="$private/ci.env"
cleanup() {
  if [[ -f "$ENV_FILE" ]]; then
    docker compose --env-file "$ENV_FILE" -p hotelops -f "$root/docker-compose.yml" logs --no-color api frontend || true
    # Ephemeral GitHub runner only. Never remove persistent production volumes.
    docker compose --env-file "$ENV_FILE" -p hotelops -f "$root/docker-compose.yml" down || true
  fi
  rm -rf -- "$private"
}
trap cleanup EXIT
password="Ci!$(openssl rand -hex 24)"
echo "::add-mask::$password"
mkdir -p "$private/tls/live/hotelops.ap.vn" "$private/acme" "$private/state" "$private/backups"
openssl req -x509 -newkey rsa:2048 -sha256 -nodes -days 2 -subj '/CN=hotelops.ap.vn' -addext 'subjectAltName=DNS:hotelops.ap.vn' -keyout "$private/tls/live/hotelops.ap.vn/privkey.pem" -out "$private/tls/live/hotelops.ap.vn/fullchain.pem" 2>/dev/null
printf 'Server=db;Database=HotelOpsCompactCiTest;User ID=sa;Password=%s;Encrypt=True;TrustServerCertificate=True\n' "$password" > "$private/connection"
chmod 444 "$private/connection"
cat > "$ENV_FILE" <<EOF
IMAGE_REGISTRY=hotelops-ci
IMAGE_TAG=$(git -C "$root" rev-parse HEAD)
MSSQL_PID=Developer
MSSQL_SA_PASSWORD='$password'
DATABASE_NAME=HotelOpsCompactCiTest
DB_CONNECTION_FILE='$private/connection'
TLS_DIRECTORY='$private/tls'
ACME_DIRECTORY='$private/acme'
STATE_DIRECTORY='$private/state'
BACKUP_DIRECTORY='$private/backups'
PUBLIC_URL=https://hotelops.ap.vn
HEALTHCHECK_CA='$private/tls/live/hotelops.ap.vn/fullchain.pem'
HEALTHCHECK_RESOLVE=hotelops.ap.vn:443:127.0.0.1
EOF
chmod 600 "$ENV_FILE"
docker compose --env-file "$ENV_FILE" -p hotelops -f "$root/docker-compose.yml" build api frontend
bash "$root/deployment/init-database.sh" --schema-only
docker compose --env-file "$ENV_FILE" -p hotelops -f "$root/docker-compose.yml" up -d api frontend
bash "$root/deployment/healthcheck.sh"
# An initialized database must never be silently reset on retry.
if bash "$root/deployment/init-database.sh" --schema-only; then echo 'Init unexpectedly accepted an existing/live database' >&2; exit 1; fi
backup=$(bash "$root/deployment/backup-database.sh")
docker compose --env-file "$ENV_FILE" -p hotelops -f "$root/docker-compose.yml" stop api frontend
if bash "$root/deployment/init-database.sh" --schema-only; then echo 'Existing database was unexpectedly accepted' >&2; exit 1; fi
# Exercise the real Linux restore path, not VERIFYONLY alone. Keep the source DB.
sed -i 's/DATABASE_NAME=HotelOpsCompactCiTest/DATABASE_NAME=HotelOpsCompactRestoreTest/' "$ENV_FILE"
bash "$root/deployment/init-database.sh" --restore "$backup"
printf 'Server=db;Database=HotelOpsCompactRestoreTest;User ID=sa;Password=%s;Encrypt=True;TrustServerCertificate=True\n' "$password" > "$private/connection"
docker compose --env-file "$ENV_FILE" -p hotelops -f "$root/docker-compose.yml" up -d api frontend
bash "$root/deployment/healthcheck.sh"
echo 'PASS isolated Linux Docker build, fresh schema, HTTPS, auth, actual backup restore and reinitialization guard'
