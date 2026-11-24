#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
deployment_lock
require_stopped_api
mode=${1:-}
[[ "$mode" == '--schema-only' || "$mode" == '--restore' ]] || { echo 'Use --restore /absolute/verified.bak or --schema-only (CI TEST only)' >&2; exit 1; }
dc up -d --wait db
exists=$(sql -d master -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name=N'$DATABASE_NAME';" | tr -d '\r\n ')
[[ "$exists" == 0 ]] || { echo 'Existing database refused; no restore/replace/reset permitted' >&2; exit 1; }
if [[ "$mode" == '--schema-only' ]]; then
  [[ "$DATABASE_NAME" =~ ^HotelOpsCompact.*Test ]] || { echo 'Schema-only is TEST-only' >&2; exit 1; }
  sql -d master -Q "CREATE DATABASE [$DATABASE_NAME];"
  # Even the empty database is backed up before installing schema.
  bash "$DEPLOY_ROOT/deployment/backup-database.sh"
  node "$DEPLOY_ROOT/deployment/render-migration.mjs" schema-only "$DATABASE_NAME" | sql -d "$DATABASE_NAME"
else
  backup=$(realpath -- "${2:?Provide backup path}")
  [[ -f "$backup" && "$backup" == *.bak ]] || exit 1
  name="restore_$(date -u +%Y%m%dT%H%M%S)_${RANDOM}.bak"
  dc exec -T db mkdir -p /var/opt/mssql/backup
  dc cp "$backup" "db:/var/opt/mssql/backup/$name"
  dc exec -T -u 0 db chown 10001:0 "/var/opt/mssql/backup/$name"
  sql -d master -Q "RESTORE VERIFYONLY FROM DISK=N'/var/opt/mssql/backup/$name' WITH CHECKSUM;"
  files=$(sql -d master -h -1 -W -s '|' -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK=N'/var/opt/mssql/backup/$name';")
  moves=''; index=0
  while IFS='|' read -r logical _physical type _rest; do
    [[ "$type" == D || "$type" == L ]] || continue
    [[ "$logical" =~ ^[A-Za-z0-9_]+$ ]] || { echo 'Review nonstandard logical filename before restore' >&2; exit 1; }
    index=$((index+1)); suffix=mdf; [[ "$type" != L ]] || suffix=ldf
    moves+=", MOVE N'$logical' TO N'/var/opt/mssql/data/${DATABASE_NAME}_${index}.$suffix'"
  done <<< "$files"
  [[ "$index" -ge 2 ]] || { echo 'Invalid backup file list' >&2; exit 1; }
  sql -d master -Q "IF DB_ID(N'$DATABASE_NAME') IS NOT NULL THROW 51900,'Target already exists',1; RESTORE DATABASE [$DATABASE_NAME] FROM DISK=N'/var/opt/mssql/backup/$name' WITH CHECKSUM$moves;"
fi
[[ "$(schema)" == 3.0.0 ]] || { echo 'Restored database needs a reviewed schema migration before API startup' >&2; exit 1; }
echo 'Database ready; no existing database was replaced.'
