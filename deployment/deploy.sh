#!/usr/bin/env bash
set -Eeuo pipefail
requested=${1:?Provide full immutable commit SHA}
source "$(dirname "$0")/common.sh"
[[ "$requested" =~ ^[a-f0-9]{40}$ ]] || exit 1
deployment_lock
export IMAGE_TAG="$requested"
[[ "$(schema)" == 3.0.0 ]] || { echo 'Migrate separately before deployment; never auto-reset the database' >&2; exit 1; }
previous=''; [[ ! -r "$STATE_DIRECTORY/current" ]] || previous=$(cat "$STATE_DIRECTORY/current")
dc pull api frontend
bash "$DEPLOY_ROOT/deployment/backup-database.sh"
restore_previous() {
  if [[ -n "$previous" && "$(schema)" == 3.0.0 ]]; then
    IFS='|' read -r old_root old_sha <<< "$previous"
    if [[ -f "$old_root/docker-compose.yml" && "$old_sha" =~ ^[a-f0-9]{40}$ ]]; then
      echo 'Release failed; restoring previous application images without changing database' >&2
      IMAGE_TAG="$old_sha" docker compose --env-file "$ENV_FILE" -p hotelops -f "$old_root/docker-compose.yml" up -d --no-deps api frontend || true
    fi
  fi
}
if ! dc up -d --no-deps api frontend || ! bash "$DEPLOY_ROOT/deployment/healthcheck.sh"; then
  restore_previous
  exit 1
fi
[[ -z "$previous" ]] || printf '%s\n' "$previous" > "$STATE_DIRECTORY/previous"
printf '%s|%s\n' "$DEPLOY_ROOT" "$requested" > "$STATE_DIRECTORY/current.tmp"
mv "$STATE_DIRECTORY/current.tmp" "$STATE_DIRECTORY/current"
echo "Deployed $requested"
