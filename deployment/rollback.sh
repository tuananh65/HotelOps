#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
[[ -r "$STATE_DIRECTORY/previous" ]] || { echo 'No previous verified release recorded' >&2; exit 1; }
IFS='|' read -r old_root old_sha < "$STATE_DIRECTORY/previous"
[[ -f "$old_root/deployment/deploy.sh" && "$old_sha" =~ ^[a-f0-9]{40}$ ]] || exit 1
[[ "$(schema)" == 3.0.0 ]] || { echo 'Rollback blocked by schema incompatibility' >&2; exit 1; }
exec bash "$old_root/deployment/deploy.sh" "$old_sha"
