# Request scope matrix / probe

Created `docs/restructure-v2/REQUEST_SCOPE_MATRIX.md` with per-family policy, actual artifact/test references and remaining gaps. OM ordinary requests are read-only; warehouse replenishment, write-off and transfer approvals are separately authorized domain actions, not exceptions silently granted to ordinary requests.

Created `scripts/verification/v2-request-scope-matrix-probe.mjs`. Caller signature: `{fixture,foreignBranch,request,inspect,directory,ordinaryRequest?,managedRequests?}`. `request` is housekeeping `request(alias,path,method,body)`; `inspect` returns its SQL state; optional ordinary object contains workId/rowVersion. Managed objects contain type, actor, workId, rowVersion. Foreign inbox/types checks cover eight branch-scoped actor aliases. Managed bypass expects403 USE_REQUEST_WORKFLOW as actual source implements, not409.

No existing runner, production source, database or other agent file changed. `node --check` PASS. Runtime NOT RUN; no E2E47 PASS claim. Integrate after fixture setup with no workers/parallel mutations, then preserve artifact and compare SQL state. Does not cover SYS_ADMIN/global assignments or all request families automatically.

Unresolved questions: none. Remaining work is execution/coverage, not an owner decision.
