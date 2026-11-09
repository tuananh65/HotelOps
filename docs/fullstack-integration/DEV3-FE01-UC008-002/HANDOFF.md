# UC008 Custom Role Create/Update candidate

Baseline 0bb4ea1f5fc8a8301aa403ecd033174aeae2c1b6. Status IMPLEMENTED_SOURCE_ONLY. No existing source file changed, no production route/flag activation. Update is intentionally incomplete/fail-closed under the task's explicit version-boundary exception.

## Create

Inactive CustomRoleCandidateController: POST /api/roles. Request exactly roleCode, roleName, optional nullable description. Unknown actor/isSystemRole/grant fields rejected by strict DTO. GBR003 trims code/name, uppercases code; max lengths follow current Role mappings (varchar60/nvarchar150). No new ASCII-only business restriction. Description nullable/trimmed with documented technical request bound 4000 characters and control-character validation; HTTP request limit 32KiB. Server creates IsSystemRole=false, IsActive=true, timestamp; no UserRole or RolePermission is created.

Service requires exact cookie/current actor+session identity and unrestricted SQL session. Local ReadCommitted transaction -> existing GlobalAdminSerialization FIRST -> retained Users universe/admin before-snapshot -> SQL session + BOTH effective unbound global USER_MANAGE and ROLE_MANAGE. Then retained UPDLOCK/HOLDLOCK RoleCode lookup using explicit Latin1_General_100_CI_AS comparison (case insensitive, accent sensitive); cooperating writers serialized by existing global administration transaction lock. Role code must roundtrip through varchar within 60 bytes; lossy input rejected rather than silently changed. Duplicate lookup 409; SQL duplicate constraint conflict on Role insert 409. This does not certify privileged external writers or SQL races without acceptance.

Role insert and exact created metadata AuditLog(CREATE, entity roles, global BranchId=null) use the SAME transaction. Entries are detached before existing guard rereads, preserving its clean-context requirement. EnsurePreservedAsync rechecks durable administrators; SQL session/dual global authority revalidated before commit. Invariant failure returns 409, authority/session failure returns 403/401; uncommitted transaction disposal rolls back. Controller sanitizes database/provider failure to 503. 201 is returned only after CommitAsync, with Location /api/roles/{newId} and CustomRoleCreatedDto (RoleId, RoleCode, RoleName, Description, IsSystemRole=false, IsActive=true, AssignedUserCount=0, PermissionGrantCount=0). No ETag invented.

Existing permission evaluation and UC010 exact-envelope checks remain unchanged: custom role name/code carries no administrative authority and new roles are not automatically assignable through Assign V1.

## Update — missing concurrency decision

Inactive PATCH /api/roles/{roleId}, DTO only roleName and description. No RoleCode/system/grant/assignment mutation. The candidate reads current Role inside the same global-first/Users/authority/Role locking boundary. Authorized missing Role ->404; IsSystemRole=true ->409 SYSTEM_ROLE_PROTECTED; ALL custom roles ->503 ROLE_VERSION_CONTRACT_REQUIRED, even those whose IsSystemRole=false but canonical identity may require registry protection. No UPDATE or UPDATE audit is executed.

Role has no RowVersion mapping. A durable monotonic aggregate revision/strong If-Match acquisition+coverage and canonical identity protection strategy are not approved here. Do not replace these with content hash/timestamp/User ETag or assume transaction locking alone prevents stale browser overwrite. No SQL schema, migration or column is invented. TL/security must decide this before Update can have a successful path.

## Frontend

CustomRoleForm standalone supports Create review/confirm and read-backed Edit metadata form. Reuses AuthProvider, RoleCatalog client and existing CSRF bootstrap transport. Dispatch defaults OFF; OFF sends no CSRF/POST. Edit never PATCHes, even in enabled test configuration. Production AuthRouteHost and four read APIs unchanged. No mock services/browser storage/logging. Ref double-submit guard, abort/current-session binding, safe error messages and irreversible per-flow replay lock after any attempt. Network/5xx/malformed 201/late response -> OUTCOME_UNKNOWN; no POST retry. Confirmed Create triggers authoritative detail refresh; refresh failure does not revoke confirmed server creation or permit replay.

## Receipts (local, no SQL execution)

- dotnet build HotelOps.slnx: exit 0, 0 errors/warnings.
- dotnet run --project scripts/verification/CustomRole/CustomRole.Verification.csproj: 22 checks PASS exit 0 (normalization/strict DTO/new-entity defaults/initial identity/HTTPS/cancellation/inactive metadata/update boundary). Not SQL success/rollback proof.
- dotnet run --project scripts/verification/RoleCatalog/RoleCatalog.Verification.csproj: 38 checks PASS exit 0, including MVC discovery preserving four GETs and DTO/count/query translation tests; no SQL connections.
- node --test tests/custom-role-api.test.mjs tests/custom-role-browser.test.mjs tests/role-catalog-api.test.mjs tests/role-catalog-browser.test.mjs: 53/53 PASS exit 0 (26 new +27 read regressions).
- node --test tests/auth-http.test.mjs tests/auth-state.test.mjs: 94/94 PASS exit 0.
- npm.cmd run typecheck, npm.cmd run build, scoped Prettier: exit 0; build has two inherited React Router use-client warnings.
- dotnet test HotelOps.slnx: exit 0, no cases reported; NOT acceptance evidence.
- Existing Forgot Password regression run separately: 0 PASS/1 FAIL exit 1 at auth-router-integration.test.mjs:609, same inherited immediate-instruction expectation previously reproduced on baseline. UC001 files/tests untouched.
- Staged git diff --check: exit 0. An initial frontend write used a wrong frontend-prefixed path; files were then created at correct paths before successful verification.

SQL authorized success, actual duplicate/case-collation behavior, concurrent creates, actual expired SQL sessions, audit fault/rollback, commit uncertainty, SQL1205/deadlock and runtime antiforgery acceptance NOT_RUN: no approved disposable trial lease. Do not infer these from unit tests or build. No FE04/shared runtime/SQL used.

## Gates

Create source candidate ready for independent review, runtime/business activation pending. Update BLOCKED_VERSION_CONTRACT (TL/security owner) and source has no success path. UC008 remains PARTIAL, not COMPLETE_VERIFIED. UC009 not implemented; next preparation requires approved grant/scope ceiling, canonical protection, durable revision/shared-writer protocol and atomic LastAdmin/audit acceptance. No merge/deploy/activation, DEV2 edits, Program.cs/DI/policy/entity/seed/schema changes.
