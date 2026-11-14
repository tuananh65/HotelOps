# UC007 Change Department V1.0 — restricted role-less correction

Authority: Project Leader DEV3-NEXT-025, Issue57 comment6775216478 and delegated V1 decision mandate.
Source pin:4f95da1d51b36806a6ce31bb5abc7462d0ed43b3. Not production release or whole-UC acceptance.

## Authoritative schema and lawful success

User.DepartmentId and User.PrimaryBranchId are nullable single FKs; UserBranchAccess stores Branch membership,
not multiple Departments. HotelOpsDbContext departments_index_4 uniquely constrains (BranchId,DepartmentCode).
CreateUser V1 maps HK_STAFF/HK_SUPERVISOR->HK, MAINTENANCE_STAFF/LEAD->MT, STOREKEEPER->WH, RECEPTION->FO.
Consequently a distinct Department inside the same Branch cannot keep the same canonical code. V1 does not
move any of these six-role accounts, delete/rewrite roles, or invent another department membership table.

Chosen narrow lawful state: an EXISTING active employed user with zero UserRole history, PrimaryBranchId1,
DepartmentId2, exactly one valid STANDARD primary membership in Branch1, old Department2 and destination
Department3 both active inside Branch1. The scalar Department changes2->3; primary branch/membership remains
unchanged, zero roles remain zero, target sessions are revoked. No grants are added. Pure validation admits this
state and tests its invariants. We do not provision role-less users or assert any such real fixture exists.
A named role-less fixture and owner confirmation of administrative correction eligibility are explicit release gates.
This does NOT solve changing departments for normal six-role CreateUser accounts; those return ROLE_SCOPE_CONFLICT.
If no lawful role-less fixture exists in the approved trial, business activation remains blocked; do not manufacture
one by deleting assignments. Broader valid role-envelope transfer needs a separate contract.
No full GBR031 multi-department compliance claimed. No inactive/revoked/expired history is ignored.

## HTTP whitelist (controller NonController, no DI or production mount)

GET /api/users/{userId:long}/department ->200 {userId:number,branchId:number,departmentId:number}+ETag.
PUT /api/users/{userId:long}/department with exactly {departmentId:positive long}; unknown fields rejected.
User ETag:one quoted uppercase16hex representing exactly8bytes; mandatory If-Match, no wildcard/weak/list.
PUT200 {userId,branchId,previousDepartmentId,departmentId}, fresh User ETag.
Both use HTTPS, existing cookie/session, no-store. Only PUT requires CSRF. No branch/role/permission input.
IDs must be JS-safe positive integers at the frontend; backend long route validation remains server-side.
400 invalid body/tag/HTTPS;401 invalid session;403 insufficient global USER_MANAGE/self/restricted;
404 missing User;409 ROLE_SCOPE_CONFLICT/INVALID_DEPARTMENT_SCOPE/DEPARTMENT_UNCHANGED/
OPEN_RESPONSIBILITIES/LAST_DURABLE_ADMIN;412 stale;428 missing If-Match;503 acceptance/coverage unavailable.
GET returns only whitelist; invalid existing scope409, unavailable authority503. No role/membership details emitted.
Unhandled storage faults may be500; frontend treats5xx/ambiguous outcomes as unknown, not confirmed rollback.

## Admission and transaction

Choose effective GLOBAL unbound USER_MANAGE only. It covers both old/destination scope in the same primary Branch;
branch-bound ALL/BRANCH/OWN is not sufficient. No role-name inference or frontend permission authority.
Read uses owned SQL transaction, retained User-first authority locks and initial/final session+global checks.
Write uses owned Serializable transaction, global administrator guard FIRST, retained sorted actor/target Users,
authority rows/membership ranges, primary Branch, sorted old/destination Departments, target role key range.
Revalidate active account/employment, old+new branch relation, exact primary STANDARD membership,
zero complete role history, expected User version and current session/authority. Nothing trusts client branch.
Retained rows/ranges protect prior facts; fresh target and responsibility reads precede conditional update.
UPDATE includes User ID, expected rowversion, old Department, original primary Branch and active status.
Atomic Department/UpdatedAt update + target SessionRevocationService + AuditLog + durable-admin poststate;
final session/authority and dependency reread before commit. Early returns/errors dispose uncommitted transaction.
SQL lock plans, shared writer races and rollback are NOT verified by the no-SQL tests.

GBR028 uses unchanged IntegratedDeactivationResponsibilityQueries for the WHOLE target, not just destination.
Cleaning/rework/inspection and approved DEV2 facts are reused; unresolved task/request/shift/manager models and
unsupported references remain DENY. Known blocker ->409 only when coverage complete; unknown or absent writer
census ->503. Empty facts NEVER certify all participating writers. Read does not promise write eligibility.
Immutable PendingReleaseGates stops direct mutation BEFORE SQL. Existing readers' unresolved categories remain;
there is no successful reachable production mutation until independent acceptance. No configurable bypass.

## Frontend and tests

New ChangeDepartmentFlow reuses AuthProvider and cookie/CSRF transport, binds review+ETag from ONE response,
validates destination, performs Review->Confirm, synchronously excludes duplicate PUT, freezes target/branch
through preflight, and checks current identity/unmount before publishing results. Unknown/failed outcomes clear
review data and confirmation controls; no auto replay.401 offers explicit session revalidation.412 requires a new
review. Default dispatch OFF; component is not imported by production router. No storage/secrets/raw errors.
No edits to accepted Add/Auth/Create/Assign/FE02/FE12, Program.cs, shared schema or other writers.

Verification receipts are developer offline evidence only. Backend harness exercises real policy, strict JSON,
controller admission/MVC discovery and immutable pre-SQL service boundary. It does NOT test successful SQL
transaction, global permission query translation, audit/session atomicity, FK/index/range locking or races.
Frontend tests use synthetic responses and offline Edge, including synthetic successful change. Build PASS does
not prove SQL translation or whole-UC completion. LiveSQL/HTTPS NOT_RUN; UC credit delta0.

## Pending acceptance / next UC

DEV1: final task/cleaning/rework/inspection/IR/shift/manager responsibility predicates + all reopen/reassign writers.
DEV2: PR25 reconciled facts/writer locks, full inactive/unscoped coverage. External imports/jobs/directSQL census.
All owners: global-first/User-first/feature lock protocol, final rereads, no-orphan work in both race orders,
LastAdmin poststate and role-path preservation under concurrent Assign, atomic session+audit rollback faults.
Runtime owner: disposable GUID/schema/fixtures/least-privilege identity, verifiedTLS/HTTPS, exclusive trial lease;
role-less target existence/eligibility proof, conditional update/concurrency+audit acceptance and source review.
No sharedSQL, DDL, migration, TLS bypass or activation. PR47 follows valid PR25 merge; PR51 stays HOLD.
Next UC:UC138 scoped preview using existing SHIFT_MANAGE mapping after identifying concrete resource resolver;
no arbitrary employee roster visibility or Save/notification writer.
## Exact delta and local receipts

Ten new files only:
- src/HotelOps.Api/DTOs/Users/ChangeDepartmentContracts.cs
- src/HotelOps.Api/Services/Users/UserDepartmentChangeService.cs
- src/HotelOps.Api/Controllers/UserDepartmentChangeController.cs
- frontend/src/users/changeDepartmentApi.ts
- frontend/src/users/ChangeDepartmentFlow.tsx
- frontend/tests/change-department-api.test.mjs
- frontend/tests/change-department-browser.test.mjs
- scripts/verification/DepartmentChange/Program.cs
- scripts/verification/DepartmentChange/DepartmentChange.Verification.csproj
- docs/fullstack-integration/DEV3-NEXT-025/CONTRACT_V1.md

Executed commands (no live connections):
- dotnet build HotelOps.slnx:exit0,0 warnings/errors, final service source.
- dotnet test HotelOps.slnx:exit0,restore-only output; no test-case count claimed.
- dotnet run --project scripts/verification/DepartmentChange/DepartmentChange.Verification.csproj:45/45,exit0.
- dotnet run --project scripts/verification/UserRoleRevocation/UserRoleRevocation.Verification.csproj:54/54,exit0.
- node --test tests/change-department-api.test.mjs tests/change-department-browser.test.mjs:60/60,exit0 (final new tests).
- Earlier combined new Department +AuthHTTP/state +MembershipAPI/router +CreateAPI +AssignAPI:796/796,exit0;
  that run included52 new Department cases, before eight additional negative tests; do not add repeated tests as unique credit.
- node --test tests/create-user-router-integration.test.mjs tests/assign-user-role-router-integration.test.mjs tests/auth-router-integration.test.mjs:181/181,exit0.
- npm run typecheck and npm run build:exit0. Two inherited ReactRouter use-client warnings.
- Scoped Prettier/C# format checks, allowlist, staged whitespace and secret scan are publication gates.
- Existing tracked files unchanged at this baseline; new files only. No Program.cs/DI/production routes edited.