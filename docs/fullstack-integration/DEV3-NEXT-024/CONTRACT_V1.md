# UC010 Restricted Revoke V1.1 — DEV3-NEXT-024

Authority: Project Leader's delegation in Issue59 comment6774933897 and current task.
Baseline:458420ea8c1507a803f327cd24feee1b61f40542. Source decision only; no runtime activation.

## Contract

Reuse bodyless DELETE /api/users/{userId:long}/roles/{userRoleId:long}, HTTPS,cookie,CSRF,
strong User eight-byte RowVersion quoted uppercase16hex If-Match. Actor must have effective unbound global
USER_MANAGE AND ROLE_MANAGE; no self-revoke or restricted session. Identity is immutable assignment ID
belonging to target. Exactly one historical assignment, active, already assigned and unexpired, bound to
active primary Branch and matching active Department with a live membership. Only the existing delegated
five-role exact grant envelopes qualify. No system/global/admin/OM/custom role, no role/history deletion.
The role history is retained by setting IsActive=false. User conditional version update, target-session revocation,
audit and LastAdmin poststate must all commit together; final actor/session authority is reread.

This change reuses IntegratedDeactivationResponsibilityQueries in the SAME transaction with retained User locks.
All target responsibilities count, including outside selected role scope, inactive branches and unscoped facts;
this narrow V1 never treats a role-specific empty subset as clearance. Reuse existing accepted cleaning/rework/
inspection/DEV2 predicates unchanged; unresolved vocabulary/model takes precedence over known blockers.
No new cross-module state vocabulary or writer implementation is invented.

New internal statuses: ResponsibilitiesBlocked -> sanitized409 OPEN_RESPONSIBILITIES;
ResponsibilitiesUnresolved -> sanitized503 RESPONSIBILITY_COVERAGE_REQUIRED.
Missing writer participation is unresolved even if every reader is empty. Immutable PendingResponsibilityWriters
lists DEV1 task/cleaning/inspection/request/schedule, DEV2 maintenance/asset/inventory/request and external jobs/imports.
This is a list of missing evidence, NOT an approved exhaustive writer census or a boolean release flag.

Reuse global-first GlobalAdminSerialization -> retained Users -> authority rows -> target assignment range ->
branch/department locks. Dependency reads do not certify range locks or writer participation.
Prewrite target/assignment/expiry reread and second dependency read precede conditional mutation;
third dependency read follows role/session/audit and final authority/LastAdmin checks, before commit.
Any non-success return disposes the uncommitted transaction; live rollback proof remains pending.
Existing PendingReleaseGates stays immutable and BEFORE SQL, controller NonController, production UI unmounted,
client dispatch OFF. No direct successful writer reachable. Do not remove these gates based on classifier tests.

## Frontend

Reuse existing API/UI. Only a matching code/title OPEN_RESPONSIBILITIES with body status409 is definite rejection;
contradictory identifiers stay OUTCOME_UNKNOWN. UI clears protected review data, removes confirm controls,
shows a fixed handover instruction and does not leak dependency details or retry. Other503 responses remain
conservative unknown outcomes; no claim of rollback from transport status alone. Session recovery unchanged.

## Verification

Backend build exit0,0warnings/errors. UserRoleRevocation54/54 exit0 (42 existing +12 new; three source-order
assertions are explicitly NOT executable SQL/concurrency proof). Initial new final-gate test failed because an
existing zero-session fixture was not reset; corrected fixture, retained prior assertions, rerun54/54PASS.
dotnet test HotelOps.slnx exit0 only restore output; no discovered test count, not counted as54 tests.
Frontend revoke API/browser +Auth HTTP/state140/140 exit0, including new matching/conflicting open-work
responses, no replay, no secret output and UI confirmation lock. Typecheck/build exit0; two inherited ReactRouter
use-client build warnings. Scoped Prettier/C# formatting and diff checks recorded in handoff message.
No SQL/HTTPS/trial/migration/deploy executed. No server race/rollback/audit atomicity or whole-UC acceptance claim.

## External acceptance gates and coordination

DEV1: authoritative final task/request/schedule/manager predicates and every assign/reassign/reopen writer,
retained target User/global-first lock participation and final gate ordering. Existing unresolved categories remain.
DEV2: PR25 reconciliation plus complete responsibility/writer coverage (including inactive/unscoped/unknown
facts), same transaction snapshot/fingerprint reread and target User participation. Do not cherry-pick unreviewed PR25.
All owners: external jobs/import/directSQL census; read-to-write races in both lock orders; role expiry/authority
replacement,LastAdmin concurrent loss,exactly-one-version winner,session and audit rollback including fault injection.
Runtime owner: explicit disposable GUID/schema+fixtures/restricted identity/TLS/exclusive lease approval.
No sharedSQL. Source V1 decision does not provide this lease or acceptance.
PR47 sync follows valid PR25 merge; PR51 remains HOLD. UC010 stays PARTIAL / IMPLEMENTED_SOURCE_ONLY.
Next: UC007 same-primary-branch single-department V1, preserving role paths and release gates, separately branched.