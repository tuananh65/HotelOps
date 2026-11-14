# DEV3-FE01-RBAC-001 — IMPLEMENTED_SOURCE_ONLY

Baseline 3ab51777ca7b15af5336eb42aa442666a4264687. Backend SQL-backed read implementation + real-auth frontend routes. UC008/009 remain PARTIAL, not whole-UC acceptance.

## HTTP contracts

- GET /api/roles?page=1&pageSize=25&search=...&isActive=true: CatalogPage<RoleCatalogDto>.
- GET /api/roles/{roleId}: RoleDetailDto {role,grants}.
- GET /api/roles/{roleId}/permissions: RoleGrantsDto {roleId,grants}.
- GET /api/permissions/catalog?page=1&pageSize=25: CatalogPage<PermissionCatalogDto>.

Page range 1..1000000; pageSize 1..100; search <=100 characters with no control characters; search uses database collation and Contains against role code/name. Optional boolean active filter. Stable RoleId/PermissionId ordering. Detail IDs positive Int64; frontend requires positive safe integer. Frontend pages use size 25. Grant list max 10000, over-limit returns sanitized 503 rather than silently truncating.

Role DTO: roleId, roleCode, roleName, nullable description, isSystemRole, isActive, assignedUserCount, permissionGrantCount. Assigned users means DISTINCT user IDs with active assignment, AssignedAt <= one request instant, no expiry or expiry > instant, and ACTIVE account/employment + nondeleted user. This is deliberately NOT effective access: no role-active, branch-membership or runtime permission predicate. Counts are global under global administrative read authority. Grant count means all stored RolePermission rows, not effective grants. No user identities exposed.

Permission DTO: permissionId, permissionCode, permissionName, moduleCode, resourceCode, actionCode, nullable description. No isActive field. Grant DTO: rolePermissionId, permission DTO, stored DataScope. No catalog/role-name inference, user-specific effective-access report or seed mutation.

200 success; 400 invalid query/model; 401 invalid/expired session; 403 restricted or insufficient authority; 404 authorized missing role; 503 database/authorization unavailable or grant limit. Error adapter uses sanitized ProblemDetails, no raw exception/connection data. HTTPS required. No-store on all catalog responses. No Role aggregate ETag, write/replay or atomic guarantee across separate HTTP snapshots.

## Authorization and transaction

Cookie scheme authentication; TypeFilter admission order -3000 precedes automatic model-state action filter. Current request actor/ticket user+session IDs must agree. Restricted actors denied before lookup. Both effective unbound global USER_MANAGE AND ROLE_MANAGE required, preserving stricter existing administration boundary. Branch/OWN grants cannot substitute.

Reuse UserDirectoryReadGuard.RunAuthorizedAsync with a feature-specific authorization delegate; no modifications to evaluator, guard, cookie/session middleware, lock order or Program.cs. Guard retains SQL Serializable Users-universe -> session/grants/projections, performs initial/final SQL session/authorization checks and commits read transaction only on success. Filter replaces ActionExecutedContext.Result on final denial so fetched data is not published. Existing guard can hold broad Users read locks; runtime concurrency/load compatibility remains part of isolated SQL acceptance. No structure/RBAC audit or state write; existing middleware may still touch sessions.

RoleCatalogService holds queries/DTO projection. Controller constructs this stateless service with already-registered request DbContext and TimeProvider to avoid changing shared DI. TypeFilter resolves its already-registered framework/security dependencies. MVC verification discovers four GET actions and no exact route collision.

## Frontend

Real AuthRouteHost routes /roles, /roles/:roleId, /roles/:roleId/permissions and /permissions use new RoleCatalogRoute; dashboard business boundary links Role directory. Existing prototype RoleList/RoleDetail/PermissionMatrix files remain outside this real-auth graph. No mock services, services.can, new auth store, browser storage, role mutations or Save controls. Catalog read permission is never write authority. Matrix groups only current catalog page by module and displays stored grants by permission ID/DataScope. Separate catalog/grant reads are explicitly non-atomic.

Session replacement remounts data scope and aborts old requests; filter/page changes hide old bound data immediately. Loading/empty/401/403/404/503 states; no raw ProblemDetails exposed. Permission and role DTOs are validated and copied to whitelists. Protected data is hidden until identity confirmed; backend is sole authority for administrative access.

## Executed receipts

- dotnet build HotelOps.slnx: exit 0, 0 warnings/errors.
- dotnet test HotelOps.slnx: exit 0, restore output only; no test cases reported, NOT acceptance evidence.
- dotnet run --project scripts/verification/RoleCatalog/RoleCatalog.Verification.csproj: 38 checks PASS, exit 0. Executable unit/admission/DTO/count semantics + MVC discovery + SQL provider translation without connection. No source assertions presented as SQL behavioral evidence.
- node --test tests/role-catalog-api.test.mjs tests/role-catalog-browser.test.mjs: 27/27 PASS, exit 0 (16 API, 11 offline browser).
- Combined new tests + auth-http/auth-state/auth-router-integration: 168 tests, 167 PASS, 1 FAIL, exit 1 (then-new 25 RBAC tests + 143 existing FE01 tests). Inherited failure: auth-router-integration.test.mjs:609 expects Forgot Password instruction immediately; accepted UC001 now shows identifier/instruction form.
- Independently ran the one failed test in unchanged dev3-uc139-scheduled worktree: same failure, exit 1. git diff against target baseline showed no delta in auth module, auth screens or that test in that worktree. No assertions changed to hide this inherited failure.
- npm.cmd run typecheck and npm.cmd run build: exit 0; two inherited React Router use-client build warnings.
- Scoped Prettier and staged git diff --check: exit 0.

Initial build attempt failed because the new DTO/service directories were not created by a malformed shell command; corrected file creation and final builds PASS. No production issue was bypassed to pass verification.

## Remaining acceptance

SQL execution, actual expired-session SQL validation, missing-role SQL lookup, actual grant/branch-bound permission denial, final-gate revocation races, transaction/error concurrency and live HTTPS NOT_RUN: isolated trial approval not provided. Unit tests use injected permission decisions/in-memory entities and provider translation; they do not certify SQL correctness or live authorization. Security/source review required before runtime deployment. No shared database used, merge/deploy performed, mutation endpoints created or canonical grants changed.

Known inherited FE01 regression remains for separate UC001 test maintenance. Full UC008 custom-role lifecycle and UC009 grant mutations remain unimplemented/policy-gated. Source-ready read slice only; not COMPLETE_VERIFIED.
