#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
name="${DATABASE_NAME}_$(date -u +%Y%m%dT%H%M%S)_${RANDOM}.bak"
dc exec -T db mkdir -p /var/opt/mssql/backup
sql -d master -Q "BACKUP DATABASE [$DATABASE_NAME] TO DISK=N'/var/opt/mssql/backup/$name' WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=N'/var/opt/mssql/backup/$name' WITH CHECKSUM;" >&2
dc cp "db:/var/opt/mssql/backup/$name" "$BACKUP_DIRECTORY/$name" >&2
chmod 600 "$BACKUP_DIRECTORY/$name"
sha256sum "$BACKUP_DIRECTORY/$name" > "$BACKUP_DIRECTORY/$name.sha256"
if [[ -n "${BACKUP_S3_URI:-}" ]]; then
  [[ "$BACKUP_S3_URI" =~ ^s3://[a-z0-9.-]+/[A-Za-z0-9_/-]*$ ]] || exit 1
  aws s3 cp "$BACKUP_DIRECTORY/$name" "${BACKUP_S3_URI%/}/$name" --sse AES256 >&2
  aws s3 cp "$BACKUP_DIRECTORY/$name.sha256" "${BACKUP_S3_URI%/}/$name.sha256" --sse AES256 >&2
fi
printf '%s\n' "$BACKUP_DIRECTORY/$name"
