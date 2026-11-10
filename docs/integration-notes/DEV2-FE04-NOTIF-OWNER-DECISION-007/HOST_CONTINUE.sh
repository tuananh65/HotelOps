#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../../.."
expected_branch='feature/dev2-fe04-notif-owner007'
implementation='4733f6318be79d3deb07fde0fa19b325fde9ea6c'
[ "$(git branch --show-current)" = "$expected_branch" ]
[ -z "$(git status --porcelain)" ]
git merge-base --is-ancestor "$implementation" HEAD
git fetch --no-tags origin d60022b487b5b59e84d6551a0ee5162c9fd32164 54d75a634005afb8d42704f761a820ed16b1b9d4
set +e
remote_before=$(git ls-remote --exit-code --heads origin "refs/heads/$expected_branch")
remote_status=$?
set -e
if [ "$remote_status" -ne 2 ] || [ -n "$remote_before" ]; then
  echo 'STOP: remote destination exists or cannot be verified. Review exact remote ancestry before continuing.' >&2
  exit 1
fi
git push origin "HEAD:refs/heads/$expected_branch"
local_head=$(git rev-parse HEAD)
remote_head=$(git ls-remote --exit-code --heads origin "refs/heads/$expected_branch" | cut -f1)
[ "$local_head" = "$remote_head" ]
printf 'SOURCE_PUBLISHED=true\nLOCAL_HEAD=%s\nREMOTE_HEAD=%s\n' "$local_head" "$remote_head"
