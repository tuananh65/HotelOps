# DEV3-NEXT-016 — UC012 metadata integration

UC012_CREATE_UPDATE_SOURCE_STATUS: IMPLEMENTED_SOURCE_ONLY / SOURCE_REVIEW_PENDING.
UC012_DEACTIVATE_SOURCE_STATUS: PARTIAL / BLOCKED_COORDINATION, unchanged.
Branch: feature/dev3-uc012-completion.
Base: e576e33f7d0546227414dacc2598985480df5661 (fresh fetched integration).

## Actual source gap and implementation

Before this change, active BranchMaintenanceController delegated to RoomTypeCreationService.MaintainBranchAsync with quoted SHA-256 document/audit ETag. BranchAdministrationService already implemented SQL RowVersion, fixed timezone, global MASTER_DATA_MANAGE, initial/final SQL-session checks and transactional audit, but BranchAdministrationController was NonController. Existing accepted frontend matched the older active SHA contract, not SQL RowVersion. Do not describe previous frontend tests/Task74 SQL receipts as acceptance for this new wiring.

The existing metadata controller now delegates exactly CreateAsync/ReadAsync/UpdateAsync to the existing BranchAdministrationService, already registered in Program.cs. No new controller/API/business writer or DI change. It retains cookie, HTTPS, no-store, DTO whitelist/limits and adds AutoValidateAntiforgeryToken for unsafe actions alongside unchanged ApiCsrfMiddleware. BranchAdministrationController remains NonController, including its dependency/deactivate actions. No successful deactivation path is added.

Existing routes remain POST /api/branches, GET /api/branches/{id:long}/edit, PUT /api/branches/{id:long}. Create sends only code/name/address/phone/email; server generates identity, Asia/Ho_Chi_Minh timezone and active state. Update sends name/address/phone/email/reason, preserving code/timezone/status. Responses retain EditableBranchDto and 201/200. Existing service maps duplicate 409, denied 403, session 401, not-found 404, stale 412, unavailable 503. Missing If-Match 428 and malformed 400 are controller concerns. ProblemDetails is sanitized without raw validation exception details.

Frontend strongBranchTag now accepts only a quoted canonical eight-byte Base64 SQL RowVersion (14 characters including quotes), rejecting legacy 64-hex documents, weak/list tags, alternate pad bits and wrong lengths. Create's optional ETag and Update's required snapshot ETag share this decoder. API requests otherwise retain reviewed payload, cookies/CSRF, no automatic retry and identity/stale/unknown-outcome guards. Update now preserves CSRF 401/403/503 status; 401 read/preflight/mutation clears fields/review and blocks further reads across branch replacement until explicit successful unrestricted session revalidation. Failure does not unlock; unknown mutation replay stays locked.

## Runtime transition and review gate

This is a coordinated BREAKING ETAG TRANSITION. Integrate/review/deploy the controller and frontend together; do not deploy one side with the old other side. An old SHA If-Match is rejected, never upgraded or automatically replayed. Open browser flows must reload and explicitly read/review fresh state.

BranchAdministrationSchema is unchanged: real dbo.branches.row_version must be non-null SQL rowversion (system type 189, eight bytes); branch_code requires case-insensitive collation and an enabled, unfiltered, single-key unique index. Missing prerequisites return BRANCH_ROWVERSION_SCHEMA_REQUIRED/503. Generated Branch entity intentionally has no invented RowVersion property: the existing service reads/compares raw SQL row_version. No migration, entity or mapping changed. Actual schema availability is NOT_VERIFIED in this task. Do not bypass the schema guard or apply schema changes without separate owner approval.

Existing service ownership/implementation retained: UserEligibilityLock, Serializable caller-owned transaction, initial/final session and global permission admission, feature create applock/unique constraint, row UPDLOCK/HOLDLOCK, conditional UPDATE row_version predicate, no-op preserving version/audit, and audit within the same transaction. Source reuse does not prove live race/atomicity. Required approved trial cases: authorized create/read/update, duplicate code and concurrent create, branch-bound denial, stale/missing/invalid If-Match, two concurrent edits, no-op, fixed timezone, final session/permission revocation, audit-fault rollback and timeout/deadlock outcomes (1205 always FAIL). Legacy Task74BranchCases expects 500 for injected audit fault, while reused Administration service returns sanitized 503: adapt expectations deliberately before future approved trial; no historic receipt is silently carried over.

## Deactivate (GBR-033)

Reused BranchDeactivationDependencyReader observes active rooms, warehouse/item net stock (including negative balances/inactive warehouses), and task/incident/request state groups. It requires retained Serializable transaction. ScopeResolutionComplete and CanDeactivate stay false; unresolved states and empty counts cannot authorize success. BranchAdministrationService.DeactivateAsync still returns confirmed blockers 409 or coordination 503 for active branches. No HTTP deactivate route activated.

Owners/exit criteria: DEV1 task/cleaning/inspection/internal-request current-cycle, terminal/reopen and branch-scope rules; DEV2 stock/warehouse/incident/maintenance/request writer inventory and matching state/lock contracts; DEV3 orchestration/final rereads/atomic audit only after complete coverage; PL/business resolves ambiguous states and notification obligations; PL/trial owner supplies named disposable lease for writer races, rollback and final session checks. No shared writer or dependency predicate was changed.

## Verification (current source, all exit 0)

- dotnet build HotelOps.slnx: PASS, zero warnings/errors.
- dotnet run --project scripts/verification/BranchMetadataRouting/BranchMetadataRouting.Verification.csproj: 36/36 PASS, no SQL. Actual MVC discovery, exact three metadata routes/no Deactivate, cookies/CSRF/HTTPS/no-store, strict DTO overposting, canonical If-Match admission, existing result status/ETag mapping, missing-CSRF middleware rejection. Synthetic mapping/direct negative service calls are not SQL permission/transaction acceptance.
- dotnet test HotelOps.slnx --no-build: exit 0, no framework test cases reported.
- node --test tests/branch-maintenance-api.test.mjs tests/branch-maintenance-browser.test.mjs tests/create-branch-api.test.mjs tests/create-branch-browser.test.mjs: 94/94 PASS (62 API + 32 offline browser). Includes 256 RowVersion final-byte variations within one API case, two explicit recovery/branch-change cases, double-confirm/no retry and unknown/stale/session regression cases.
- node --test tests/auth-http.test.mjs tests/auth-state.test.mjs tests/hotel-api.test.mjs: 164/164 PASS.
- node --test --test-name-pattern="actual entry|branch change|new entry|dashboard|UC023" tests/hotel-router-integration.test.mjs: 8/8 PASS.
- npm.cmd run typecheck and npm.cmd run build: PASS. Two inherited react-router use-client bundling warnings.
- Scoped Prettier and dotnet format whitespace verify-no-changes: PASS.
- Exact allowlist, diff whitespace and secret-pattern scan required before commit.

No live SQL/HTTPS, permission SQL, schema trial, race, audit-fault rollback, migration, deployment or merge performed. Workday writes OFF, old WorkdaysController NonController, UC010 Revoke/UC008 Update gates and other FE01/FE02/FE12 business source unchanged. Only UC012 metadata controller/panel/client/contract/tests and new focused verifier/handoff change.

NEXT_UC: UC138, only an approved independent business slice; public preview/writer admission, HTTP/version, transaction/notification policy must not be invented. UC012 whole-UC completion remains PARTIAL until Deactivate coordination and required runtime acceptance are met.

## Exact source/test manifest and SHA-256 (LF)
- src/HotelOps.Api/Controllers/BranchMaintenanceController.cs: B4F48E6D16E7E371FD020A86480633E62C985303CC3EE849BDAE49C7F1D02867
- frontend/src/branch-maintenance/contracts.ts: 6C4A2BE2271A7140A7D86B6628C2B9FB12C2F7219AFDF20600E0E36ACE6A19AA
- frontend/src/branch-maintenance/branchMaintenanceApi.ts: A3E2BBCF7532988E45C608DFAD874C235D13D9CADBB4AEB3116A7F08C4F90DBF
- frontend/src/branch-maintenance/BranchMaintenancePanel.tsx: 755F9DC298615DACCB7F0E33DF1FAD4F7AEB3CC92561A8A3E7635D25DF0BBFC9
- frontend/src/branch-maintenance/createBranchContracts.ts: 39D155571082EE266735DE29B260D1652AF7DCAEE14C00A55554E25B54A4772E
- frontend/tests/branch-maintenance-api.test.mjs: AF0812A731B97151D945531DA2258B01FD6E6DEF6C79658D65618FBE2E4C313D
- frontend/tests/branch-maintenance-browser.test.mjs: 40BCC3DD45C01A95681B4A9639E52557274240DF429ABE7DE09CE3BC9608ACCE
- frontend/tests/create-branch-api.test.mjs: F3C3F3151C8715E713CB7006866EF90F5C041D5EB5601728F41EDB2948C45057
- frontend/tests/create-branch-browser.test.mjs: BF74395A8FC3B394251A9D1B54FEB7128579F3FFD1B87A615CC874BD3FEA0360
- scripts/verification/BranchMetadataRouting/Program.cs: BC3CC0BC180565B3CB1E253E43E08609DAC4DFECA29C6F8362BC75B0514B0CF0
- scripts/verification/BranchMetadataRouting/BranchMetadataRouting.Verification.csproj: 720478BD353C02A24462E2FE4F00BD713CC9ABD45A42AE42F904BDF140B13A50
