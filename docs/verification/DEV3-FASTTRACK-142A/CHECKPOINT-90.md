# FASTTRACK-142A checkpoint90: credential workflow closure and approved FE13 authority

TASK_ID=DEV3-FASTTRACK-142A
CURRENT_HEAD=commit containing this receipt; initial local/remote 9a209b25e57d8d1eb13861598b07418544115f89.
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
NEW_COMPLETE_VERIFIED=[UC002 Logout, UC003 Change Password]
TOTAL_COMPLETE_VERIFIED=21/144
PARTIAL=123/144
SHARED_DB_MUTATION=NO

## Completed business workflows

The actual canonical browser, trusted HTTPS API and GUID-guarded disposable SQL completed temporary-password login -> forced change -> normal profile -> ordinary password change -> Logout -> login with final credential -> Logout. This closes UC002 and UC003 against the SRS sections 1.2/1.3, not UC001 as a whole. [Final credential evidence](checkpoint90-trial4-runtime.json).

| Acceptance | Direct evidence |
|---|---|
| UC002 current session revoked, client returns Login, future access denied | Two canonical UI Logout operations; session endpoint returns 401; SQL persisted revocations |
| UC002 logout audit attribution | Fixed missing ActorUserId for SESSION_REVOKE; SQL verifies actor on committed LOGOUT records |
| UC002 already revoked/expired success | Repeated public Logout and explicitly expired synthetic session return 204, session reuse 401 |
| UC003 temporary and ordinary password change | Both public UI workflows reach profile with MSG08; temporary marker cleared and replacement session unrestricted |
| UC003 new credential effective and other sessions revoked | Final SQL hash verifies final password; old password rejected; old own cookie and parallel session rejected |
| UC003 validation and policy feedback | Missing CSRF, weak, mismatch and same-password API rejection; UI wrong-current feedback and explicit unmet policy items; server remains authoritative |
| UC003 audit without secrets | Two password-change audits persisted; none contain passwords or hashes |
| Atomic failure | Owned synthetic audit-fault trigger causes generic 500, never success; hash, session revocations/replacement sessions and audit roll back; original session remains usable; explicit retry succeeds |

Three product fixes: logout actor attribution, successful password-change confirmation, and concrete unmet-policy feedback. No authentication bypass or password-policy downgrade. Temporary fixture credentials exist only in memory/child stdin; no credential output. Trial1 failed before browser login; browser transport now uses Edge's normal trusted HTTPS rather than Node's separate TLS client. Trial2 failed because the probe expected 503 for a deliberately injected DbUpdateException; middleware correctly returns generic 500. The expectation was corrected without changing middleware; both failed receipts are retained and both cleaned up. Trial4 is the complete final acceptance run.

## FE13 explicit authority decision implemented and SQL verified

Added `department_management_access(user_id,branch_id,department_id,granted_at,revoked_at,revision)` and one alternate unique index on existing departments to support a composite FK. Exact disposable instance/database/GUID/run guard; idempotent install; no inferred mappings/default grants; no generated EF entity changes. No shared/production mutation, cascade or disabled FK/trigger.

`InternalRequestProcessAuthority` requires a caller-owned serializable transaction, active account/employment, current DEPT_MANAGER role on the IR_PROCESS branch grant, current branch access, active branch/department, and an exact nonrevoked mapping to the locked request's current receiving department. IR_VIEW and home/manager department fields cannot grant processing. User/request/mapping locks are retained until caller commit/rollback. The caller still must validate session, version/state, audit and final authority before commit; this service is not a standalone authorization token.

[SQL authority evidence](checkpoint90-authority-trial2-runtime.json): positive mapping, multiple managed departments, missing capability/mapping, wrong branch/current department, inactive actor/department, revoked role/permission, changed receiving department, composite FK rejection, concurrent mapping revocation and routing lock waits, subsequent committed revocation denial, and transaction/audit rollback. Cleanup true. This is service/SQL evidence, **not UC115/116/118 public mutation E2E**; those remain PARTIAL. No public mutation endpoint was opened prematurely.

## Verification and next work

REGRESSION_STATUS=dotnet build HotelOps.slnx PASS (0 warnings/errors); dotnet test HotelOps.slnx exit0 but no tests discovered; actual focused FE13 regression 11 PASS. [Auth UI tests](checkpoint90-auth-tests-final.txt): 62 PASS, 0 FAIL. [TypeScript/production build](checkpoint90-frontend-build-final.txt) PASS with existing use-client and bundle-size warnings. ModelProbe build PASS with 3 EF1002 warnings limited to the synthetic GUID/long-id audit-fault DDL; no user input enters that DDL. Full frontend dependency reinstall and broad regression were not repeated in this batch.

WORKFLOWS_COMPLETED=[UC002,UC003]
UI_API_SQL_E2E_VERIFIED=[forced password change, ordinary password change, logout, old-session denial, audit and fault rollback]
EXACT_REMAINING_GAPS=[FE13 mutation services/controllers/UI still need the verified authority component; PENDING_ACCEPTANCE/ACCEPTED storage contract is absent from current status CHECK and must not be aliased to SUBMITTED; request version/progress/result/link storage and durable notifications remain; UC123 direction/linked work/withdrawal; UC006/010 public lifecycle release gates remain]
NEW_BLOCKERS=[]; approved authority decision is resolved at service/schema level. Existing state/link/lifecycle storage gaps remain explicit.
NEXT_BATCH=[implement narrowly scoped FE13 state/version storage from approved SRS, then UC116 Accept/Reject with audit/notification; UC115 route/assign with current-department revalidation; UC118 operational work links and canonical UI/API/SQL acceptance]
SPRINT_TIME_REMAINING=approximately23h45m at2026-11-15T18:05Z; deadline2026-11-16T17:50Z unchanged.

All runtime evidence here was executed in this batch. Previous 19 completed UCs are inherited checkpoint74 acceptance, not rerun or newly counted here. Sprint remains active.
