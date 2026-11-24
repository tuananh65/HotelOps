#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
deployment_lock
require_stopped_api
[[ "${1:-}" == '--p0' ]] || { echo 'Only the reviewed --p0 migration is supported' >&2; exit 1; }
[[ "$(schema)" == 2.1.0 || "$(schema)" == 3.0.0 ]] || { echo 'Unexpected schema baseline' >&2; exit 1; }
bundle=$(mktemp); trap 'rm -f "$bundle"' EXIT
node "$DEPLOY_ROOT/deployment/render-migration.mjs" p0 "$DATABASE_NAME" > "$bundle"
digest=$(sha256sum "$bundle" | cut -d ' ' -f 1)
if [[ ! "$DATABASE_NAME" =~ Test ]]; then
  [[ "${2:-}" == '--apply-reviewed' && "${3:-}" == "$digest" ]] || {
    echo "Rehearse this release on an isolated restored TEST database, then explicitly use --apply-reviewed $digest" >&2; exit 1;
  }
fi
bash "$DEPLOY_ROOT/deployment/backup-database.sh"
sql -d "$DATABASE_NAME" < "$bundle"
[[ "$(schema)" == 3.0.0 ]] || exit 1
printf 'Applied reviewed P0 migration %s\n' "$digest"
