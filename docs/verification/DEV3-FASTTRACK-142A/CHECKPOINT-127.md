# FASTTRACK-142A checkpoint127 — release regression repair and closure verification

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=cf2467f488173045780a930f0bcca04065f003ff
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

The prior full frontend process completed: 3,434 PASS, 2 FAIL, 1 SKIP (3,437 tests). This is a failed release run, not a pass. Its final diagnostics confirm the change-department fixture attempted to resolve a PUT before it existed; C124 already added a wait for the dispatched PUT. The other failure was reproducible in isolation: the real dashboard now probes report and request access, but the Login/Logout integration fixture did not supply responses for those endpoints.

Updated users-router-integration.test.mjs with explicit 403 responses for those capability probes, their existing same-origin credential contract, assertions that neither is requested before login, and denied links remain hidden. Capability requests must be GET (including fetch's implicit default), without body, CSRF mutation header or Authorization header. Unexpected calls still fail the fixture. No production authorization or routing code changed.

Validation:

- Both affected browser files: 59 PASS, zero failures/skips. This run preceded the extra GET/body/header assertions; the final Login/Logout case with those assertions then passed separately.
- One attempted invocation from the repository root failed module resolution; reran from frontend. An intermediate additional method assertion incorrectly expected explicit GET for a fetch with default GET; corrected the test to model fetch semantics, retaining explicit GET verification for Users calls.
- Full backend regression: 750 PASS, 13 environment-gated SKIP. This is not live coverage for the skipped tests.
- Frontend typecheck and production build PASS. Existing react-router use-client bundle notices and >500kB chunk warning remain.

UC120 direct runtime evidence:

- checkpoint127-completion-prerequisites.json: success=true, all 13 prerequisite checks PASS, including mandatory tasks without canonical state, physical COMPLETED not substituting for CLOSED, unclassified legacy work, pending handover, cleaning/incident completion and transaction rollback. The suspected keyless-query translation issue was not reproduced; no speculative source fix applied.
- checkpoint127-completion-ui.json: success=true. Actual browser/HTTPS/disposable SQL closes a request without linked material, persists result, rejects missing CSRF/ETag/result and stale replay, denies cross-branch/revoked mapping/read access, sends hosted-worker notification once, and rechecks read permission when opening source. Mandatory material remains fail-closed.
- Both independently identity-guarded disposable clones and backups were removed; paired cleanup receipts prove DROPPED_AND_BACKUP_REMOVED. Runtime cleanup=false delegates to whole-clone cleanup.

No newly completed whole UC. UC120 remains PARTIAL for the unresolved material terminal contract and remaining full linked-work acceptance. UC123 remains PARTIAL for material visibility and legacy classification. These successful scoped workflows do not prove all acceptance criteria. Next: corrected full frontend release run, then close remaining FE13 linked-work gaps under approved contracts.
