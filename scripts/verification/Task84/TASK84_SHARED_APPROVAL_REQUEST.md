# Final disposition: APPROVED AND APPLIED

User approved exactly one DI registration and later exactly one new SQL trial/fixtures. See DI_APPROVAL_APPLIED.json, SQL_APPROVAL.json, SQL_SCOPE_REVIEW.json and actual HTTPS receipts. No additional shared source change was applied. The original bounded request below is retained as review history; pending/NOT_APPLIED wording below describes the pre-approval checkpoint, not the final state.

# Task84 — SHARED_APPROVAL_REQUIRED

## A. Exact shared source change — NOT APPLIED

File: `src/HotelOps.Api/Program.cs`, after existing CleaningMonitorService registration (current line24).
Exact patch: `Program.di.patch` in this directory.

```diff
 builder.Services.AddScoped<HotelOps.Api.Services.Housekeeping.CleaningMonitorService>();
+builder.Services.AddScoped<HotelOps.Api.Services.HotelStructure.RoomTypeDirectoryService>();
 builder.Services.AddScoped<HotelOps.Api.Services.WorkTime.IWorkTimeTransactionParticipant, HotelOps.Api.Services.WorkTime.SqlWorkTimeTransactionParticipant>();
```

Only one scoped registration. No change to existing registrations, middleware, authentication,
authorization, configuration, RBAC, schema, entities or DbContext. No existing service is replaced.
Concrete dependencies: same scoped HotelOpsDbContext, ICurrentUserAccessor, IPermissionService,
ISqlSessionService; all already registered. No extra SQL participant or duplicate context.

Feature-owned service/schema guard have been added and current API build PASS (0 errors,2 NU1900).
19 fresh-source verification checks PASS: full real dependency graph ValidateOnBuild/ValidateScopes,
service/controller DI resolution in verification container, scoped DbContext, unchanged same-instance
A1 participant, no cycle, routes, invalid-input boundaries and EF projection translation.
This is NOT production startup or HTTPS/SQL acceptance.

Controller remains `RoomTypeDirectoryController.cs.proposal` here until DI is approved, preventing
exposure of a route whose required service is unregistered. After approval copy to
`src/HotelOps.Api/Controllers/RoomTypeDirectoryController.cs`, preserving the two historical GET routes;
apply ONLY the one registration and rebuild/retest production. No UC018/019 writer included.

The proposed controller uses internal CookieTicket.TryRead, so verification-only compile targets
include it in an isolated API build. Production build does not import those targets. No internal
authentication API was made public and no service is manually constructed to bypass production DI.

## B. Schema/mapping scope

No shared model/schema changes requested for the read service. Existing response DTOs are reused.
EF Database.SqlQuery<Row> reads the approved V1 columns through a private unmapped projection,
after the original schema/constraint/index/deployment guard copied from cf8fb02. Missing legacy-only
or incompatible schema returns SCHEMAREQUIRED503; never manufactures branch ownership.
No ALTER/CREATE/migration or fallback bypass is implemented. EF model does not register the row.

## C. SQL acceptance permission — separate gate, NOT APPLIED

No database has been opened or modified in Task84. Task82 trial must not be reset/reused for fixtures.
Real acceptance needs either an explicitly approved V1 trial with permission for new Task84-only
fixtures, or one new disposable trial. A new trial would use existing Prepare-Database.ps1,
rowversion-forward.sql and historical UC017 V1 deployment/verify scripts, after their guards and
exact hashes are reviewed; this document does NOT authorize creating it or running those scripts.
Do not request schema grants or change role_permissions for tests. Keep canonical grants unchanged.

Until source registration approval and appropriate trial permission exist:
`CURRENT_CHECKOUT_RUNTIME_STATUS = BLOCKED_SHARED_APPROVAL`.
No RESTORED & REVERIFIED claim or progress points.
