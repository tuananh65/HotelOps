# DEV3-NEXT-015 — UC026 Department deactivation candidate

UC026_SOURCE_STATUS: PARTIAL / IMPLEMENTED_SOURCE_ONLY. Not COMPLETE_VERIFIED.
Baseline: e1bf0fb9fcd1fe2b4c1e7c5104d3e0e815e6f36b.
Branch: feature/dev3-uc026-department-deactivation.

## Business presentation delivered

Standalone, unmounted DepartmentDeactivationCandidate reads the real scoped Department endpoint and quoted 64-hex Department ETag, then independently reads ACTIVE accounts via the accepted User Directory client. It presents scoped account links, known visible active-account blockers, four explicitly unverified dependency categories, and reason -> review -> confirm. Scope-filtered empty or denied account results never imply complete dependency clearance. USER_VIEW is independently enforced by its server endpoint; it is not inferred from Department GET or session status. Users 403 preserves the Department view; 401 clears protected state and requires explicit session revalidation.

API candidate supplies cookie/CSRF, strong If-Match, one synchronous mutation guard, no automatic retry, status preservation, safe runtime Department validation, identity/branch binding, cancellation and unknown-outcome handling. Synthetic success tests exercise future transport/UI behavior only. The public mutation contract is a technical candidate, not owner/business signoff.

## Backend boundary

DepartmentDeactivationCandidateController is NonController. Proposed POST /api/branches/{branchId:long}/departments/{id:long}/deactivate is absent from actual MVC discovery. It reuses DeactivateStructureRequest (reason, confirmation), existing cookie, HTTPS, CSRF, no-store, If-Match and result conventions. Direct service rejects invalid identity/input and unconditionally remains unavailable before SQL under an immutable release gate. There is no successful writer, audit insert, role/session change, transfer, delete or guessed dependency predicate.

Dormant source reuses RoomTypeCreationService.BatchAsync and DepartmentTag/DepartmentDto, including scoped lookup, initial/final SQL-session/permission checks and the existing transaction boundary. It still returns unavailable even beyond the gate; removing a release gate alone cannot create success. This is not proof that SQL/dependency/concurrency acceptance has run.

## Source evidence and unresolved requirements

Requirements: SRS UC026 section 2.15 and GBR-042; existing DepartmentBatch2Service.cs, RoomTypeBatchService.cs, RoomTypeBatchController.cs; Departments/User/Warehouse/Asset/InternalRequest EF relationships were inspected without modification. Department ETag is the existing quoted SHA-256 document/audit revision, not Workday rowversion.

Issue49: https://github.com/tuananh65/HotelOps/issues/49. Focused owner coordination comment submitted (comment id 6769680265). No owner approval existed in the retrieved comments. Required inputs: exact active-user binding/eligibility and all user writers (DEV1/DEV3); warehouse/asset custody current/history and transfer writers (DEV2); task current-cycle/reopen and internal-request requester/receiving semantics (respective module owners); complete writer census, lock ordering, final rereads and atomic audit/rollback (DEV3 with writer owners); named approved disposable SQL trial/lease and race acceptance (PL/trial owner). No shared SQL or live authentication was used.

## Verification receipts

All commands below exit 0, current source, local/offline only:
- dotnet build HotelOps.slnx: PASS, 0 warnings, 0 errors.
- dotnet run --project scripts/verification/DepartmentDeactivationCandidate/DepartmentDeactivationCandidate.Verification.csproj: 25/25 PASS, no SQL.
- dotnet test HotelOps.slnx --no-build: exit 0; solution contains no reported framework test cases; do not count this as SQL/business acceptance.
- node --test tests/department-deactivation-api.test.mjs tests/department-deactivation-browser.test.mjs: 47/47 PASS (31 API + 16 browser).
- node --test tests/auth-http.test.mjs tests/auth-state.test.mjs tests/hotel-api.test.mjs tests/users-api.test.mjs: 241/241 PASS.
- node --test --test-name-pattern="actual entry|branch change|new entry|dashboard|UC023" tests/hotel-router-integration.test.mjs: 8/8 PASS.
- npm.cmd run typecheck: PASS.
- npm.cmd run build: PASS; two inherited react-router use-client bundling warnings.
- scoped Prettier check: PASS for four new frontend/test files.
- dotnet format whitespace API project, absolute include two new C# files, --no-restore --verify-no-changes: PASS.

Browser tests execute actual bundled React/AuthProvider candidate with synthetic fetch responses, block external requests and browser storage. They are not live API acceptance. Tests cover independent 403, 401 recovery, foreign/INACTIVE record rejection, default OFF, inactive/restricted/anonymous, confirmation, safe failure and late branch/session response protection. API tests cover malformed/cross-identity success, ETag, CSRF, exact payload, synchronous BUSY and no retry.

## Preservation and release

Nine new allowlisted files only: eight source/test/project files below plus this handoff. No existing tracked file changed; accepted FE01/FE02/FE12, UC012/UC013, WorkdaysController NonController, Workday writes OFF, UC010 revoke fuse and shared writer/security/schema remain byte-identical to baseline. No production router imports candidate (asserted by bundle test). No live SQL, migration, API trial, deployment or business activation. SOURCE_ONLY candidate ready for review; API/BUSINESS activation NOT_READY.

NEXT_UC: UC012 Branch Deactivation, assess only its remaining approved business operation; retain coordination gates rather than invent dependency semantics. UC026 writer resumes when Issue49 owner inputs and isolated acceptance gates are satisfied.

## SHA-256 of source/test artifacts (LF)
- frontend/src/hotel/DepartmentDeactivationCandidate.tsx: 2C383AD8EAE8B7E516E9B0E65498B7237234B05F52B46A118E621976B8DFEECC
- frontend/src/hotel/departmentDeactivationApi.ts: 34FC5DCBB66E79D004F2D5CE10DEC751B255365C4FB024D99F39226C7350558A
- frontend/tests/department-deactivation-api.test.mjs: 639AA761439640630712734D64B8E2B239109AE408046F31B5AB666704693723
- frontend/tests/department-deactivation-browser.test.mjs: A4568072727819829D95825A633B2B88E12D52007DED23A2CEC94F2B5E47E465
- scripts/verification/DepartmentDeactivationCandidate/DepartmentDeactivationCandidate.Verification.csproj: 720478BD353C02A24462E2FE4F00BD713CC9ABD45A42AE42F904BDF140B13A50
- scripts/verification/DepartmentDeactivationCandidate/Program.cs: 76AB33AC2D82CF2B9CD9630A10AB659A03445FFAF67E8E9E38D997483E294149
- src/HotelOps.Api/Controllers/DepartmentDeactivationCandidateController.cs: 2585CD1961B715882B228C0669000396DC55B9229E8385E7A08E172B5763C90B
- src/HotelOps.Api/Services/HotelStructure/DepartmentDeactivationCandidateService.cs: 9E68665988FBDF76316B4AF62A3CC3A9648F101CA48DA339E8A081F525CD83DF
