# FASTTRACK-142A checkpoint41 — UC035 receive rework verified

Newly COMPLETE_VERIFIED: **UC035 Receive Rework Task**. Total **5 COMPLETE_VERIFIED (UC035, UC044, UC046, UC109, UC110), 139 PARTIAL**. Sprint continues; original deadline is 2026-11-16 17:50 UTC. No production deployment or shared database mutation.

## Direct acceptance evidence

SRS section4.8 defines this UC as receiving an assigned Pending Acceptance/Accepted rework, reviewing defects/photos/due time, accepting and starting with a work interval. Completion/remediation is UC036; reinspection is UC047.

| UC035 criterion | Current evidence |
| --- | --- |
| Logged-in current assignee opens from delivered notification | `checkpoint41-receive-evidence-browser.json`: real browser login, inbox, current-source resolver, restricted HTTPS detail200, both normal/blocked fixtures |
| Room, failed inspection, selected defects/photos and due time | Canonical assigned-source DTO/UI; real selected defect and scoped private photo link. Browser fetch verifies exact synthetic PNG bytes; anonymous401 and unrelated attachment404. Persisted source/proof binds inspection/version/defect/evidence |
| Pending Acceptance → Accepted | Real UI Accept, HTTPS200 committed receipt, no interval; read refresh exposes Start; MSG08 success text displayed |
| Accepted → In Progress | Real UI Start, HTTPS200, reload shows In Progress and removes receive actions. SQL independently asserts exactly one open interval and two task audits |
| Workday/busy guards | Same run invokes public SQL service under restricted user: no Working workday and preexisting open interval rejected with unchanged task revision. Working workday is a synthetic precondition, not a claim of UC workday-start completion |
| Atomic task/interval/audit, version and retry | Missing-CSRF400 and missing-ETag428 via browser HTTPS; explicit Start replay preserves receipt/interval. SQL stale and changed-request operation key rejection, concurrent identical Start creates one interval, injected final audit failure rolls back task and interval |
| Authorization and trusted source | Existing proof DML-denial gate remains. Privileged host still503; restricted host succeeds. Current assignee/branch/access revocation, issuer disablement, source tamper and foreign evidence checks remain in the same verifier |
| Room readiness | Accepted/started rework preserves REWORK_REQUIRED for unblocked room, BLOCKED with effective block; no physical room status mutation |

`checkpoint41-receive-evidence-browser.json` is the final PASS for both fixtures. This is new runtime evidence, not historical handoff. Existing UC044/046/109/110 classifications are not counted as newly completed.

## Implementation and security boundary

Added opt-in Development-only isolated SQL application-role activation before EF application queries and the raw startup schema check. Configuration requires encrypted nonpooled, non-enlisted connections, explicit server/database GUID/ownership, a disposable database name and complete secret/role settings. Activation uses direct parameterized RPC with no reversal cookie. Wrong identity/secret closes the connection; it never retries under login privileges. Effective principal and denied database CONTROL are checked. Existing authority proof additionally requires denied proof INSERT/UPDATE/DELETE.

Temporary HTTPS role has SELECT/metadata plus only required user/session, rework, audit and interval writes; proof DML explicitly denied. Random secret remains in process memory/environment. No server login, contained-auth setting, global trace flag, shared database, generated entity, migration or production configuration change. See decision41 in the approved-decision log and the Microsoft references linked there. This trial is not a production identity provisioning plan.

Canonical UI adds MSG08 success feedback. Verification now creates synthetic private defect evidence before authority issuance and removes only its owned files. The current schema's Accepted/Pending CHECK extension remains disposable and is restored exactly.

Changed application files: `Data/IsolatedSqlApplicationRole.cs`, `Data/StartupSchemaValidator.cs`, `Program.cs`, and frontend `features/inspectionExecution/AssignedReworkRead.tsx`. Added configuration safety regression tests, application-role and restricted HTTPS probes, browser receive script; updated existing rework fixture/authority/read/execution/creation probes to use the same private evidence root. No dependency change.

## Validation and limitations

Backend and ModelProbe builds PASS, zero warnings; `checkpoint41-role-config-tests.txt`: 513 PASS/11 SKIP. Solution test command succeeds but contains no test project. `checkpoint41-receive-client.txt`: 10 PASS. Frontend typecheck and production build PASS; existing Vite directive/bundle warnings and EF unmapped-index/Take warnings remain. Npm ci not repeated because lockfile/dependencies are unchanged.

`checkpoint41-application-role-runtime.json`: actual wrong secret/identity rejection and closed connections, EF reopening under restricted principal, real DML denial, transaction support, role cleanup. Configuration tests reject pooling, ambient enlistment, unencrypted transport, shared database name, non-Development host and incomplete settings.

Retained failed attempts: activation through parameterized text was rejected by SQL15422 and corrected to direct RPC; first readiness poll encountered startup timeout and was retried; Node APIRequestContext rejected the local certificate although the real browser trusted it. Private evidence verification now uses the browser's native fetch and retains TLS validation; no ignore-certificate setting was introduced. Later PASS does not erase these receipts.

`checkpoint41-cleanup.txt` independently confirms exact disposable server/database GUID, zero users/reworks/attachments/workdays/intervals, absent temporary roles/proof/fault and execution marker. Owned private evidence directory contains zero files. Both owned HTTPS hosts are stopped. Full Reject → Complete Rework → Reinspection remains PARTIAL.

Initial remote: d04cac93614ab78e9b2196f71322f93c0a8e05e9. Commit/push identities are recorded by Git. Next highest impact: UC036 per-defect remediation plus atomic interval closure and durable inspection handoff; UC047 current-cycle reinspection/results/effective readiness; then FE11 lifecycle gaps. FE13 and UC006/010 remain within the same sprint scope.
