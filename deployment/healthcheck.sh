#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
args=(--silent --show-error --max-time 15)
[[ -z "${HEALTHCHECK_CA:-}" ]] || args+=(--cacert "$HEALTHCHECK_CA")
[[ -z "${HEALTHCHECK_RESOLVE:-}" ]] || args+=(--resolve "$HEALTHCHECK_RESOLVE")
for attempt in {1..30}; do
  if body=$(curl --fail "${args[@]}" "$PUBLIC_URL/health") && printf '%s' "$body" | node -e "let s='';process.stdin.on('data',c=>s+=c).on('end',()=>{const x=JSON.parse(s);process.exit(x.status==='ok'&&x.schema==='3.0.0'&&x.tables===57?0:1)});"; then break; fi
  [[ "$attempt" != 30 ]] || exit 1
  sleep 2
done
curl --fail "${args[@]}" "$PUBLIC_URL/" | grep -q 'id="root"'
status=$(curl "${args[@]}" -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $(printf '%064d' 0)" "$PUBLIC_URL/api/v2/auth/me")
[[ "$status" == 401 ]] || { echo "Unexpected anonymous API status: $status" >&2; exit 1; }
echo 'PASS HTTPS, frontend, schema health and real-database authentication rejection'
