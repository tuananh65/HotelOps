# FS008 — autonomous cleaning implementation

Status: **PARTIAL**. UC028 full-stack Create is implemented and locally verified on an isolated synthetic SQL database. UC029 independent command/source-read/schema preparation is implemented, but actual assignment+mandatory Notification remains **MISSING_EXECUTABLE_SOURCE** for accepted A01/A02. No competing publisher was created and no successful unnotified assignment is allowed. This is a dependency on exact executable source, not a request for per-step development approval.

Authority: current user FS008 supersedes earlier per-change approval workflow for isolated development. Official DEV2 checkpoint read in full from actual path `docs/external-handoff/DEV2_FS007_UC029_AUTH_01.md`; requested plural path does not exist. Checkpoint authorizes development but forbids inventing accepted A01 signatures/replacement publisher. Ten accessible DEV2/BE2/develop2 source refs searched; see A01_A02_DISCOVERY.json. A01 design remains accepted historically, A02 executable still missing; no source acceptance fabricated.

Baseline ac0780105503291386ffa39858b0fc18ae80dde6, branch feature/dev1-room-ops. Feature tree initially clean except the user-provided handoff arrived during work; preserved separately. No other worktree/branch edited, no main/develop merge, no production/shared DB or config write. New exact target: HotelOps_Canonical_20261106_220000_f080001_Fresh, local SQLEXPRESS, owner DEV1_FS008; FS004 historical trial untouched.

Implementation: new Create/options/source-header controller/DTO/services, two lifecycle sidecars and unique/lineage constraints, real React Create/options/navigation, shared existing UC032 transport extracted without behavior change, narrow assigned header/read UI with explicit-user authority, fingerprint and prepared stable-GUID wire adapter. Initial assign core preserved private, public mutation503 fail-closed until accepted publisher integration. B01 development proposal only has three agreed tables, independently tested constraints; no queue4, no worker/runtime publisher/B02/C01 acceptance claim.

- [UC028](UC028_IMPLEMENTATION.md), [UC029](UC029_IMPLEMENTATION.md)
- [Lifecycle](ROOM_LIFECYCLE_DECISIONS.md), [Notification](NOTIFICATION_INTEGRATION.md)
- [Transaction/idempotency](TRANSACTION_IDEMPOTENCY.md)
- [SQL](SQL_TEST_RESULTS.md), [Frontend/E2E](FRONTEND_E2E_RESULTS.md), [Security](SECURITY_REGRESSION.md)
- [Shared compatibility](SHARED_COMPATIBILITY.md), [module progress](MODULE_PROGRESS.md), [merge readiness](MERGE_READINESS.md), [next actions](NEXT_ACTIONS.md)
- [Exact integration notes](../integration-notes/DEV1_FS008.md)

No UC credit changed. UC028 VERIFIED_LOCAL is not a claim all Room Lifecycle/production acceptance is complete. UC029 delivery/replay/ambiguous-commit/full guard acceptance is NOT_RUN because required accepted interface source is missing; UI Save remains unavailable. Module integration PR threshold80–90% is not reached; no PR/merge/deploy is created automatically.

Final validation: backend build PASS (0 warnings), actual .NET regression116/116, frontend45/45, browser interception41/41, live HTTPS/API/SQL5/5, Notification SQL component6/6. See [commands/warnings](VALIDATION.md), [routes](ROUTE_INVENTORY.md), [source manifest](SOURCE_MANIFEST.json).
