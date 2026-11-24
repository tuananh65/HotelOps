#!/usr/bin/env bash
set -Eeuo pipefail
umask 077
DEPLOY_ROOT=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
ENV_FILE=${ENV_FILE:-/opt/hotelops/shared/production.env}
[[ -r "$ENV_FILE" ]] || { echo 'Missing private ENV_FILE' >&2; exit 1; }
# This is an administrator-owned, private Bash-compatible file, never user request input.
set -a
# shellcheck disable=SC1090
source "$ENV_FILE"
set +a
DATABASE_NAME=${DATABASE_NAME:-HotelOpsCompactDB}
[[ "$DATABASE_NAME" =~ ^HotelOpsCompact[A-Za-z0-9_]{0,90}$ ]] || exit 1
[[ "$IMAGE_TAG" =~ ^[a-f0-9]{40}$ ]] || { echo 'IMAGE_TAG must be a full commit SHA' >&2; exit 1; }
: "${STATE_DIRECTORY:?}" "${BACKUP_DIRECTORY:?}" "${MSSQL_SA_PASSWORD:?}"
mkdir -p "$STATE_DIRECTORY" "$BACKUP_DIRECTORY"
dc() { docker compose --env-file "$ENV_FILE" -p hotelops -f "$DEPLOY_ROOT/docker-compose.yml" "$@"; }
sql() { dc exec -T -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -r 1 -f 65001 "$@"; }
schema() { sql -d "$DATABASE_NAME" -h -1 -W -Q "SET NOCOUNT ON; SELECT CONVERT(varchar(20),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOpsCompactSchema';" | tr -d '\r\n '; }
require_stopped_api() {
  [[ -z "$(dc ps --status running -q api)" ]] || { echo 'Stop API writers before changing the database' >&2; exit 1; }
}
deployment_lock() { exec 9>"$STATE_DIRECTORY/deployment.lock"; flock -n 9 || { echo 'Another deployment/database operation is running' >&2; exit 1; }; }
