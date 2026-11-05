# Shared contract decisions — scope và quyền quyết định

Mọi 'đã chốt' dưới đây dẫn COORD-FINAL-01 tại28f0a5e4f582bc56ba101b48915d33baadeb7b95, path scripts/verification/Coordination/FINAL_DEV1_DEV2_HANDOFF_20261104_02/DEV1_SHARED_CONTRACT_FINAL_DECISIONS.md. Không mở lại accepted business decision chỉ vì runtime chưa triển khai. Exact changes cần approval riêng theo AGENTS và user FS007.

| ID | Đã chốt / source fact | Một V1 proposal hoặc next decision | Owner/group | Approval boundary |
| --- | --- | --- | --- | --- |
| S01 |3dim room model approved, no current source reducer | Explicit pointer + derived projection/events, giữ legacy unchanged; two additive sidecars proposal | D joint; DEV1 feature, DEV3 platform, DEV2 block protocol | User/joint exact schema/EF/DI/current-cycle/history diff |
| S02 |Room legacy/default and history CHECK conflict verified SQL_METADATA | Không sửa legacy default/CHECK chỉ để nhét readiness; additive typed contract separates semantics | D | Approve history sidecar vs old SRS single-state persistence; no auto mapping |
| S03 |Multi-incident/manual cardinality approved, actual unique room too narrow | DEV2 revised filtered indexes/data compatibility separately; Create reads EXISTS current unreleased blocks, no block mutation | B/D | Exact index migration only before activating producers needing multi-block; not fake mandatory gate to read EXISTS |
| S04 |A01 accepted; A02 reported structural completion | DEV2 provide exact accepted API/source/ref; UNKNOWN availability ≠ reject/reapprove | B | Evidence delivery + integration review, not new competing interface |
| S05 |B01 three-table architecture and rollback/delivery split approved | Revised DDL/EF/guard exact package, actual inbox FK/replay/claims/retention/DI | B/D | B01/B02/C01 executable packages REVISE_REQUIRED; business architecture not pending again |
| S06 |Notification does not grant source permission; actor≠recipient | Narrow own-assignment header read + explicit recipient authority, current grants remain mandatory | C/D | Exact resource/read lifetime/history evaluator permission binding; no OWN→branch expansion |
| S07 |UC032 accepted, initializer only at IN_PROGRESS + actual start | Create preview, freeze at first Start; no nested Batch and no fake progress revision at create | A/D | User approves timing; DEV1 reviewed feature extraction at Start, regression required |
| S08 |Assign workload is displayed counts, actual writer rechecks shift | Preserve advisory workload; no capacity maximum/Working day condition invented | A | Existing rule preserved, no platform rewrite |
| S09 |A1/B1.1 source exists; WorkdaysController inactive here | Exact own scheduled route review only if enabling execution | C | No reapproval primitive business design; no broad recovery activation |
| S10 |UC138 rowversion/roster package proposed; B1.2 integration pending | Defer from Create/Assign; execution must have valid normal Working day | C/D | Not initial blocker, no SHIFT_MANAGE/unscheduled/PauseAll expansion |
| S11 |UC012/GBR033 deactivate fail closed; active-Branch reads/locks already in feature boundary | Retain Branch lock + feature producer cooperation; don't implement deactivate in cleaning | C/D | Only exact overlapping writer lock change; don't wait for all inventory/maintenance modules |
| S12 |Current Assign POST can commit without publication; disabled frontend does not guard server | Producer-scoped dependency/commit gate before new consumption; do not label existing route production-ready | A/B/D | Exact existing Batch/hook/Program implications require shared approval; no blanket guard all SaveChanges |

## Quyền quyết định

A: DEV1 tự code DTO/controller/service/helper/UI/tests thuộc cleaning sau khi contract đã chốt; không mở rộng role/branch/schema/other-owner partials ngầm. B: DEV2 own publisher/store/worker/replay/inbox and source-specific adapters của mình; DTO/service nội bộ không cần DEV1 duyệt từng file nếu không shared impact. C: DEV3 auth/resource/effective-user/Room platform/Workday exact integration; chỉ yêu cầu minimal actual binding. D: Chủ dự án + owners liên quan duyệt shared schema/EF/DI/lock/guard/permission semantics theo exact file/hunk/base hash.

Stable operation key fingerprint/replay, mandatory pre-commit rollback vs post-commit delivery retry, no retention purge/backfill, recipient facts immutable, no Ready from old PASS/ExpectedReleaseAt đều **đã chốt**. V1 event code/DTO sidecars/machine readiness codes/route/read policy/executable indexes vẫn **PROPOSED hoặc REVISE_REQUIRED**, không tự thành approval khi request được commit/push.

No universal lock graph is proven. Existing Batch user/session→Branch→catalog; A1 users/time→source; global-admin serialization/recipient-grant and notification reservation/worker claim edges must be jointly reconciled for exact producer. Do not require unrelated entire-system refactor or accept deadlock retry as race PASS.
