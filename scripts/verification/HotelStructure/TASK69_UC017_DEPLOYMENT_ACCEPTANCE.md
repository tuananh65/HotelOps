# Task69 — UC017 deployment integration and final acceptance

31/10/2026, Asia/Saigon. **COMPLETE_VERIFIED_V1** after deployment/HTTPS/SQL/regression acceptance.
Scope: existing UC017 implementation and deployment compatibility; no production deployment.

## A. Migration review

Reviewed UC017 SRS §2.6, change notes, GBR003/004/007/008/036, actual RoomType/Room/
InspectionChecklist/version/item mappings, database release/regeneration procedures,
[Task68](TASK68_UC017_IMPLEMENTATION.md), and its unchanged forward/rollback candidate.
All six SRS inputs remain implemented, including branch, cleaning minutes and a real default
inspection checklist; capacity is not substituted for cleaning time.

The deployable module [05](../../database/release/uc017/05_UC017_Branch_RoomTypes.sql) retains
Task68's nullable branch_id/standard_cleaning_minutes/default_checklist_id, trusted shape CHECK,
non-cascading Branch/checklist FKs, filtered branch-code/legacy-code uniqueness and exact original
unique-constraint identity. It adds the default-checklist FK supporting index and two SQL triggers:
Room INSERT/UPDATE rejects scoped type/Room branch mismatch; RoomType UPDATE rejects any change
of branch ownership, including NULL legacy ownership. Neither creates/copies/reparents rows.

[06](../../database/release/uc017/06_UC017_Verify.sql) checks actual column types/nullability,
index keys/filters/availability, FK columns/principals/actions/trust, CHECK definition, exact trigger
definitions/enabled state, version markers and data invariants. Unknown/partial/damaged state fails;
no silent repair, disabled guard, destructive conversion or schema mutation at startup.
API's read-only module compatibility gate also rejects an installed package with disabled triggers.

## B. Database First mapping

New [RoomTypeBranchV1](../../../src/HotelOps.Api/Entities/RoomTypeBranchV1.cs) and
[controlled partial mapping](../../../src/HotelOps.Api/Data/HotelOpsDbContext.RoomTypes.cs) map all
nine V1 columns, identity key, nullable legacy operational fields, two filtered unique indexes,
one checklist supporting index and Branch/default-checklist FK navigations.

This is a read-only view projection of the existing SQL table, not a new SQL view/table or a second
DbContext. It is accessed explicitly only after BranchV1 detection. SQL writes remain the working
parameterized UC017 service INSERT on the existing scoped context transaction. The generated
six-column RoomType remains the legacy-compatible table projection. The partial override removes
its obsolete global index by named index metadata, avoiding a false post-migration global UQ.
No generated file, unrelated entity/navigation or DEV2 mapping is overwritten.

EF view metadata does not expose physical table constraint names through GetConstraintName;
the test checks explicit relational-name annotations and independently verifies real SQL catalog
constraints. Actual V1 scalar and navigation materialization and write refusal are tested.
Rooms/legacy RoomType disable bare SQL OUTPUT so EF works with deployed triggers and legacy DBs.
This is the direct shared mapping impact; no authorization/business behavior is otherwise changed.
[Regeneration guidance](../../../docs/database/DATABASE_FIRST_REGENERATION.md) preserves this design.

## C. Deployment integration

[Run-Canonical.ps1](../../database/release/canonical/Run-Canonical.ps1) has an explicit opt-in
-Uc017 switch. Default canonical execution remains unchanged. Trial applies UC017 through
[Run-Uc017.ps1](../../database/release/uc017/Run-Uc017.ps1) to fresh, existing-RBAC and synthetic
Authentication-upgrade targets after the existing foundation/seed/verification. Verify uses the
same module entry point read-only. Source hashes are included in the canonical receipt.

The module entry point accepts Preflight/Apply/Verify/Rollback, requires explicit server/database/
unused receipt, checks exact SQL identity and disposable naming/Task17 marker, uses integrated local
authentication without credentials in source, and bounds SQL/lock waits. Apply executes 05 and 06
inside one outer SQL transaction; postflight failure rolls back the migration too. Source changes
during execution fail before commit. Receipts record source SHA256, before/after state and success;
provider details remain local console logs. Contract branch-v1/Deployment Task69-v1 track state.

This is a repository-integrated trial deployment, not a production-ready authorization to run on
a shared DB. Entry point and SQL guards reject HotelOpsDB. No startup migration runner is added.
[Release procedure and recovery](../../database/release/README.md) describe pausing writers,
preflight/postflight, fresh receipts and rollback/restore decisions.

The existing canonical foundation intentionally retains its historical schema verification scope.
Current API requires the previously approved Task26 users.row_version prerequisite; API trials
explicitly apply its unchanged guarded trial patch before startup. UC017 does not migrate auth.
This dependency is not hidden by claiming the canonical schema-only run starts the entire backend.

## D. Legacy data policy — ASSUMED_V1

Changed-source inventory (Task68 implementation is retained):

| Files | Change |
|---|---|
| [RoomTypeBranchV1](../../../src/HotelOps.Api/Entities/RoomTypeBranchV1.cs), [partial context](../../../src/HotelOps.Api/Data/HotelOpsDbContext.RoomTypes.cs) | New controlled V1 mapping and legacy/trigger compatibility |
| [RoomTypeSchema](../../../src/HotelOps.Api/Data/RoomTypeSchema.cs) | Installed revision/trigger availability check |
| [RoomTypeCreationService](../../../src/HotelOps.Api/Services/HotelStructure/RoomTypeCreationService.cs), [RoomDirectoryService](../../../src/HotelOps.Api/Services/Rooms/RoomDirectoryService.cs) | Documentation comments aligned with deployed/dual schema; business logic retained |
| [05](../../database/release/uc017/05_UC017_Branch_RoomTypes.sql), [06](../../database/release/uc017/06_UC017_Verify.sql), [07](../../database/release/uc017/07_UC017_Rollback.sql), [entry point](../../database/release/uc017/Run-Uc017.ps1) | New explicit trial deployment package |
| [Canonical runner](../../database/release/canonical/Run-Canonical.ps1), [release README](../../database/release/README.md), [Database First guide](../../../docs/database/DATABASE_FIRST_REGENERATION.md) | Opt-in workflow and recovery/regeneration instructions |
| [Verification Program](Program.cs), [existing UC017 cases](Task68RoomTypeCases.cs), [new deployment cases](Task69DeploymentCases.cs) | Reused HTTPS/SQL harness with actual deployment, metadata, failure and Room invariants |
| [Delivery plan](../DEV1_21_DAY_DELIVERY_PLAN.md), this report | Classification, evidence and progress |

- Existing global RoomTypes remain valid with all three added fields NULL; IDs, scalar values,
  Room/checklist FKs and cross-branch legacy use remain unchanged.
- New API RoomTypes require an authorized explicit branch, positive cleaning minutes and a real
  active generic checklist with an effective PUBLISHED version and active criteria.
- No legacy duplication, branch guessing, reassignment or irreversible conversion. Legacy codes
  remain application-reserved; new scoped codes may repeat across different branches.
- New scoped types cannot be assigned to foreign-branch Rooms. Multirow SQL rejection is atomic.
  Existing Room Directory/Branch Structure defensive filters remain as defense in depth.
- Branch ownership is immutable in V1; later conversion/movement requires a separately reviewed
  migration. A future Room writer should lock/recheck the chosen type before the Room; this task
  does not certify UC020 or arbitrary external writer lock order.
- Rollback restores the original exact UQ/legacy columns only while no scoped/operational row exists.
  With scoped data it refuses before DDL; forward-fix or reviewed restore is required. No row deletion.

## E–H. Executed acceptance and regression

| Verification | Actual result and scope |
|---|---|
| [Final canonical workflow](../CanonicalDatabase/evidence/HotelOps_Canonical_20261030_181243_928419f1.json) | PASS: Fresh, existing-RBAC Upgrade and synthetic Authentication Upgrade through actual opt-in canonical workflow; all three UC017 entry-point receipts PASS, schemaComparison PASS, CHECKDB and CHECKCONSTRAINTS PASS. This final package includes the checklist supporting index |
| Fresh database deployment | PASS: new canonical install, existing patches/catalog/seed, explicit UC017 Apply + strict postflight, no sample RoomType/checklist/personnel fabricated |
| Legacy business-data upgrade | PASS in [UC017 receipt](task69-accepted.local.json): real legacy RoomType/Room created before migration; scalar IDs/values/Room FKs preserved after forward, replay, rollback and recovery. Legacy global type remains usable across branches; no branch backfill |
| Actual entry-point schedules |12 invocations in UC017 receipt: successful preflight, apply, replay, verify, empty-scoped rollback and recovery; expected rejections for unknown marker, real mid-DDL fault, populated rollback, disabled unique index and disabled trigger. All rejection cases preserve data. An additional populated reapply preserves all RoomType/Room/CREATE-audit rows (obj/task69-populated-preservation.local.json) |
| [UC017 HTTPS/SQL](task69-accepted.local.json) | **163 UC017+2 startup=165 PASS**,10 DMV-observed waits,7 post-INSERT audit/1222/-2/1205/cancel/session/grant fault schedules. Production Program/login/cookie/CSRF/SQL authorization; real V1 successful creation, branch/code/checklist validation, duplicate race, authoritative reread and atomic audit/rollback. Explicit fresh retry commits once; replay409 |
| Cross-branch assignment | PASS: actual EF Room INSERT rejected with SQL51069/no row; UPDATE rejected/preserves FK; one bad row rolls back the complete multirow INSERT; scoped branch move and silent legacy conversion rejected. Real other-branch Room detail404. Global legacy RoomType remains usable in both branches |
| Database First | PASS: all9 fields and real Branch/checklist navigation materialize; two filtered unique indexes and supporting index/FK annotations match the independently verified catalog; obsolete global-index metadata removed; read-only V1 write rejected. Generated files unchanged |
| Startup compatibility | PASS: main UC017 API starts on legacy schema/read200/create503, then operates after deployment; consumer APIs start independently on migrated schema. Unknown/damaged schema fails closed |
| [Legacy UC023 SQL](task69-legacy-read.local.json) |117 PASS/11 waits, unpatched schema; all mapped table rows restored; no regression of existing credentials/catalog/session/audit/assignment data |
| [Migrated UC023 HTTPS](task69-scoped-rooms.local.json) |227 UC023+2 startup=229 PASS/4 waits, actual deployed module and production HTTPS list/detail/reason/source/history/auth/cancel/timeout/expiry behavior |
| [Migrated UC013/014/015](task69-scoped-structure.local.json) |590 PASS=216 UC014+159 UC013+215 UC015;53 waits,14 post-UPDATE UC015 fault schedules. Existing harness verifies the shared Rooms/trigger mapping change; creation audit assertion count varies with fixture race winner |
| AuthN/AuthZ |174 PASS=43 PermissionService+61 Authorization+49 ResourceAuthorization+21 Hardening, rebuilt against final API. Their documented doubles/loopback scope is retained; live SQL authority is independently tested in HTTPS receipts |
| Build/test/diff | Exact dotnet build HotelOps.slnx PASS,0 errors,2 NU1900 warnings from unreachable NuGet vulnerability feed. Targeted harness builds PASS. dotnet test exit0/no test project; relevant console suites executed explicitly. Final diff/link/PowerShell syntax/source-preservation proof: obj/task69-validation.local.json |

Final API trial: HotelOps_Canonical_20261030_180508_1a0ca8b3_Fresh. Canonical final run:
HotelOps_Canonical_20261030_181243_928419f1. Each final main entry-point receipt records the same
final package SHA256. No successful receipt is inferred from build or SQL-only probing.

Reproduction: build HotelOps.slnx and the existing HotelStructure verification console. Provision a
new disposable canonical personnel trial with the unchanged approved rowversion prerequisite, then
invoke the console with trial name, unused receipt and --task69. It starts actual production Program
over pinned local HTTPS, uses real login/cookies/CSRF/SQL permissions and invokes the real PowerShell
deployment entry point; no fake checklist/session/permission provider is substituted.
The receipt includes deployment-receipt paths, actual wait evidence and post-INSERT fault schedules.
--task67 and --task65 retain the existing targeted consumer regression suites.

Earlier failed attempts are retained: the initial AuthUpgrade SQL guard rejected its suffix;
the first DDL-fault fixture compared an index event's ObjectName to a table name and did not inject
the intended fault; EF view metadata assertions initially used table-only accessors; named index
removal initially targeted an unnamed index; branch-move fixture initially hit an existing code's
unique constraint before the intended trigger. These were corrected and are not counted as PASS.
The one intermediate compile error during named-index removal is also retained in its build log.
One redundant canonical Verify waited on SQL Express RESOURCE_SEMAPHORE during CHECKDB; it completed
with PASS without increasing timeout. No failed log/trial/evidence was removed or relabeled.

## I–L. Classification, progress and next task

**UC017 PARTIAL → COMPLETE_VERIFIED_V1**. All mandatory V1 fields/authorization/atomic audit work;
deployment is integrated explicitly, controlled Database First mapping and legacy/fresh deployment
acceptance pass, real HTTPS/SQL and relevant regressions pass. No mandatory UC017 V1 blocker remains.
This classification covers the requested repository/backend trial acceptance, not production rollout.

Gain **+2 points**, **10→11/75** verified UCs, **25→27/256=10.55%** (previous9.77%).
Remaining229 points; taxonomy **11 COMPLETE_VERIFIED (including V1),5 PARTIAL,39 NOT_IMPLEMENTED,
20 BLOCKED_EXTERNAL=75**. UC016 stays PARTIAL, UC023 stays verified; no denominator reduction.
The [delivery plan](../DEV1_21_DAY_DELIVERY_PLAN.md) records this final snapshot separately from Task68.
Preflight backs up475 original dirty/untracked files with SHA256; final validation checks all copies,
all original files, exact HEAD/index/upstream and all four worktrees. Only the listed Task69 files
change; previous SQL candidates, receipts, DEV2/auth implementations, generated files and UC006 remain.

No production/shared deployment, HotelOpsDB access, DEV2 source change, RBAC catalog edit, UC006
release-gate change, UC016 completion, Git mutation, artifact deletion or worktree change is claimed.
Real deployment still requires target-specific review, backup/restore proof, existing authentication
prerequisites and eligible real checklist data. No sample checklist or personnel data is deployed.
Recommended next feature after this acceptance: UC018 Update Room Type on the branch/legacy/checklist
contract, with concurrency/audit and reference protections; do not infer branch conversion permission.
