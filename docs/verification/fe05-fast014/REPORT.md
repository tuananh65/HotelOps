# [HotelOps FE05] FAST014 — PARTIALLY_VERIFIED / PENDING_ACCEPTANCE

Baseline: FAST013 d8d9195e13e0a5edb1edc79050ee56f24525209e.
Worktree: D:\SEP490\Projects\HotelOps-DEV1-FE05-FAST014.
Branch: feature/dev1-fe05-approve-reinspection-accept010.

## Source implementation

### UC044

Reuse FAST013 composition/PASSED receipt/replay seam and FAST012 SQL domain writer unchanged. No duplicate transaction, audit, ledger, issuer module or projection. New focused tests execute original FE04 InspectionRoomProjectionRules: trusted PASSED/mandatory PASS produces READY candidate; effective block preserves original operational status/BLOCKED precedence; wrong cycle, missing mandatory PASS, untrusted receipt, open Rework or incomplete task deny. Optional FAIL flag alone does not override mandatory PASS in the current original rule. This is source/rule compatibility evidence, not approval of optional-criterion policy or positive SQL READY projection.

No exact approved Passed event/recipient/profile/staging contract found: current Fe05NotificationKind/N01Accept07 graph contains InspectionFailed and CleaningReworkRequested only. ClosedInspectionApproveBinding remains closed. Durable UC044 SQL issuer/result/replay and accepted notification participants are not supplied by ACCEPT010. Public Approve stays OFF; original FE04 READINESS_OVERLAY_V1/registered issuer/READY schema/projection acceptance remains required. No production room status mutation. UC044 capability remains 70%; stronger evidence is not an authoritative writer completion.

### UC047

New guarded `scripts/database/fe05-fast014/01_ReinspectionCandidate.sql` and `Install-ReinspectionCandidate.ps1` prepare/install candidate storage ONLY on the newly created owned FAST014 component target. Exact receipt path/server/database/owner UUID guard, no overwrite, empty Rework inventory, purpose marker; no shared SQL, old deficient-lane modification, backfill, generated entity/mapping, migration, production operational/workflow status change or existing FS016 module overwrite.

Candidate stores exact new Inspection→Rework→original Inspection/task/room/branch/cycle relationship, pinned original/assigned checklist versions separately, revisions, reinspector/binding actor/time. Selected-defect results use composite FK to exact Rework defect association, Fixed bit (NULL unanswered), recorded actor/time and rowversion. Result trigger invalidates distinct old/new aggregate binding and Inspection revisions set-based. Binding guards prevent rebind and verify source/cycle/phase/version/inspector identities; result trigger rejects recorded actor mismatch. New-trial-only remediation fields reuse original FS016 field/FK/consistency rule, with Rework invalidation trigger. No synthetic authority proof tables or notification schema installed.

`ReinspectionComponentPersistence.cs` adds internal caller-transaction bind/save/read primitives that reuse existing trusted reader/session/branch/SOD/checklist/current-cycle checks and exact complete Fixed/Not Fixed validator. It requires the isolated candidate marker, locks parent before results, compares Inspection/Rework/binding/cycle revisions, keeps result/audit in the same SERIALIZABLE transaction and never commits or concludes lifecycle. Actor comes from validated session, not client. Return AuthoritativeOrCommitted=false. This component binding is NOT an accepted UC041 assignment setter or responsibility authority. Production API service/controller/frontend candidates remain FAST013 versions with NonController dispatch OFF and unmounted/disabled Conclude; no fake transport or notification authority added.

Missing decisions: exact UC041 trusted binding/reassignment action and original-vs-reassigned checklist version policy; accepted storage/note/photo/retention/audit/invalidation schema; FAILED Rework workflow/status mapping (absent from existing CK); next-round new defect copying/linkage and UC046 source identity/revision; accepted notification/projection. All Fixed→Completed and Not Fixed→Failed remain SRS plans, not installed lifecycle transitions. No automatic latest checklist selection or inferred legacy binding.

### UC045/046 + ACCEPT010

ZIP SHA256 E5EA9DE23BC671C7088BA9325733EC22B2D474326E877A179E9F6D6CE8505EE2; package 116/116, allowlist 25/25 and source identity 9ca9572711d6cc34c532e9530edd4c602bae615215e3a94a3d12eaf80b91fc1e verified. Status LOCAL_ONLY_UNCOMMITTED is NOT a Git commit/runtime acceptance. Original sources are extracted hash-checked to ignored TestResults; no production package import. `Prepare-Accept010Reference.ps1` reproduces verification without overwriting differing cached bytes.

Existing console harness conditionally links original exact interfaces/protocol/executor source; pinned ScriptDom 180.140.0 is test-only. FE05 implements original IAcceptedFe05OwnerCaseDriver and IAcceptedFe05CaseObserver candidate adapters. Real owner-call helpers invoke existing ComposeRejectInspectionAsync/ComposeInspectionReworkAsync using retained scoped context/physical transaction/prelocked Users/staging/guard. Unknown outcome helper calls original observation only, never repeats mutation or generates a new operation identity. SQL observation helper independently queries persisted domain/operation/publication/delivery/issuer/proof identities; structural expected metadata cannot create authority.

Original IDs 01–14 and BOTH orderings for 07/08/10 are preserved via original L2Plan, not FAST013 Oxx scenario renumbering. Original AcceptanceGate immutable ACCEPT010 hold remains before fixture/context/connection factory. No signing-key mock, local override, second notification graph/reservation store, broad permission or producer/worker activation.

Important limit: accepted per-case fixture scopes, authenticated independent observer scopes/protected before/after inventory, case-specific receipt/proof/recipient graph/prelock/projection/replay/concurrency/cleanup execution remain unimplemented/unbound. Actual SQL helper and conditional compile are candidate source, not successful L2 observer/owner execution. No claim B05 CLOSED or driver READY. Refusal tests are unit/contract evidence, NOT SQL acceptance.

## Verification receipts

| Check | Actual result | Credit/limit |
|---|---|---|
| Original ZIP/allowlist/source identity | 116/116 + 25/25 PASS | Integrity only, not accepted runtime |
| Backend solution build | PASS, 0 warnings/errors | Source build |
| Conditional harness build with original ACCEPT010 source | PASS, 0 warnings/errors | Actual original types, no guessed signatures |
| Ordinary harness build without optional references | PASS, 0 warnings/errors | Default build needs neither ZIP nor ScriptDom package |
| New FE04 rule + actual file storage checks | 16/16 PASS | Component; no SQL Ready commit/WORM acceptance |
| Original ACCEPT010 contract/hold compatibility | 10/10 PASS | Refusal before factories, not SQL PASS |
| UC047 initial positive storage scenario | 1/1 PASS | Actual local SQL; explicit rollback |
| UC047 final positive/negative SQL scenarios | 3/3 PASS | Binding/results/audit/revision, stale/foreign rejection, all Fixed, wrong cycle, forged actor; full rollback incl candidate rows |
| N01 L1 | 24 NOT_RUN | Original schema/runtime/observer/hold blockers |
| N01 L2 | 14 NOT_RUN | Original hold before any fixture/context/connection, not 14 PASS |
| Successful authoritative Approve/Reject/Rework commit, durable replay, FE04 different-transaction consumer/projection | NOT_RUN | No accepted runtime/participants |
| UC047 lifecycle/room projection commit and public HTTPS E2E | NOT_RUN | Production dispatch remains OFF |
| Historical backend/frontend suites | NOT_RERUN | No frontend/router/auth source delta; keep FAST013 6 SSR/build receipts without counting them as new FAST014 tests |

Initial in-progress harness builds failed with missing newly added check classes while source was still being assembled; final conditional build includes all files and passes. Preserve this limitation/history; do not claim every build attempt passed. Final ordinary/conditional build receipts and solution test command outcome are separate; API-only solution test exit 0 gives no focused functional credit. Test source failures/SQL failures are not silently omitted.

Fresh target: local DESKTOP-JI8U50T\SQLEXPRESS, HotelOps_Canonical_20261110_091941_b2addabb_Fresh, owner 9b004555-982f-4a78-bee3-f4ae78902a2b. New guarded existing preparer, initially 0 users; synthetic fixtures only. No resets/drop of old INSP/FAST trials. Schema exists before positive test; no patching FAST010's deficient schema. Real session/permission within SQL scenarios; business/session/result/audit/source mutations explicitly rollback and persisted row counts/snapshots checked. Synthetic FAILED/Rework fixtures are NOT a trusted SQL issuer commit or proof. This target is NOT the accepted/restricted N01 SQL lane and receives no L1/L2 acceptance credit.

## Storage / permission / blockers

New actual file negative control: FileShare.Read rejects replacement while lease held; AFTER lease release overwrite succeeds under current writer identity; digest detects replacement on reread. This demonstrates missing durable no-overwrite enforcement. No ACL/WORM/version retention or crash-safe authority-window acceptance is inferred. Durability remains false; no runtime identities/permissions changed, no mutable legacy promotion.

Three exact proposed recipient policies remain unchanged, acceptedConcreteBinding=null and private evidence=false. PERMISSION_PROPOSAL.md is byte-identical to FAST013. No broad INSPECTION_VIEW or MASTER_DATA_MANAGE grants introduced. B01–B08 mapping in ACCEPT010_ALIGNMENT.md and original OPEN_BLOCKERS snapshot: B07 source availability verified by actual commit objects + fresh remote ref readback; B05 implemented candidate portions only; B04/B06 and schema/runtime/provider/GUID approval blockers remain. No new task assigned to other developers, no need to redo R05 or wait FE04 UC029 acceptance.

## Git / progress

Scoped private feature commit/push only after final manifest/source/sensitive checks. FAST013 source/ref preserved. No merge/deploy/force-push/shared SQL, public writer, Notification producer/worker or other-owner worktree modifications. Source manifest distinguishes source files, test-only conditional dependencies and isolated candidate SQL from production acceptance.

Technical estimates: FAST014 75%; UC041 70% unchanged, UC044 70%, UC045/046 80% unchanged, UC047 45% (persisted component evidence added; no accepted full lifecycle/API workflow). These are estimates, not acceptance.

FE04_DEPENDENCY_STATUS: R03/R05_PINNED_R05_INTEGRATED_VERIFIED_ISOLATED / NOT_READY_FOR_AUTHORITATIVE_HANDOFF. R05 95f7422e284bc81bcfcf9f460dace52022de5cc7 retained. No AUTHORITATIVE_INSPECTION_HANDOFF_READY or UC ACCEPTED emitted.
