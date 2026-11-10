# [HotelOps FE05] FAST013 — PARTIALLY_VERIFIED / IMPLEMENTED_PENDING_DEPENDENCIES

Worktree: `D:\SEP490\Projects\HotelOps-DEV1-FE05-FAST013`.
Branch: `feature/dev1-fe05-approve-reinspection-composition`.
Parent baseline: `96b1681e86bd41677972ca67410af446a62b3cfc`.

## Recovery và bảo toàn source

AUTH_STATUS: approval capability RECOVERED. Elevated read-only Git/remote check thành công sau khi Project Leader đổi tài khoản. Trước thay đổi cuối, 14 tracked/untracked source/document files được ghi SHA256 vào ignored `TestResults/fast013/recovery/preserved-before-final-review.json`. Không reset, clean, discard, checkout đè, xóa source/receipts/test databases hoặc sửa worktree khác. Không xuất account token/credentials. GitHub push được kiểm tra riêng khi finalize.

## UC044 implementation thực tế

`InspectionApproveComposition.cs` reuse FAST012 `ApproveDomainInCallerTransactionAsync`. Thứ tự: require clean caller SERIALIZABLE + attached existing NotificationCommitGuard; canonical UC044 identity; require accepted Passed binding; authorize và lock exact source; reserve/read completed; completed replay verify trước Pending Conclusion/stale eligibility; fresh domain/audit step; required staging; trusted SQL receipt completion; exact poststate + receipt/current SQL writer transaction; original FE04 projection; bind/recheck authorization/source/receipt/transaction trước guarded caller commit. Bất kỳ participant lỗi poison commit guard và caller phải rollback. Physical connection/transaction identity được giữ sau mỗi participant, kể cả final Verify.

PASSED receipt structural checks: FE05_INSPECTION_CONCLUSION_V1, FE05.UC044.UC045 version 1, exact actor/operation/canonical fingerprint, branch/Inspection/room/task/checklist version, current Inspection ETag, PASSED và positive writerTransactionId; fresh operation phải khớp current SQL transaction. Structural JSON không cấp authority hoặc chứng minh receipt đã commit. Accepted binding phải kiểm chứng registered SQL issuer/module hash, persisted result/replay, required graph/recipients, retained Users và persisted projection; không được implement chỉ bằng JSON parsing.

`ClosedInspectionApproveBinding` là implementation duy nhất trong API source, mọi method fail-closed. Không DI/public route/config toggle nào activate composition. Chưa có concrete Passed staging/durable SQL binding; không tạo ledger, framework transaction hoặc notification graph thứ hai. Không sửa/invent SQL issuer Passed action hay event chỉ để tạo successful receipt.

## SRS / Passed projection decision

SRS `docs/requirements/srs_sections/02_use_case_specifications.md` UC044 (section 5.6): mandatory PASS, assigned inspector, Pending Conclusion, task Completed, Ready unless Blocked, cùng audit transaction và notify original cleaner. Existing ApproveInspectionValidator và original `InspectionRoomProjectionAdapter.RequiredCriteriaPassed` đều kiểm tra active mandatory criteria. Adapter chỉ loại outcome khác PASS đối với `is_required=1 AND is_active=1`; optional FAIL có described defect không tự làm mandatory rule thất bại. Không sửa FE04 adapter.

Existing `Fe05NotificationKind`, `Fe05OperationIdentity.EventType`, N01Accept07 publication CK chỉ có `inspection.failed` và `cleaning.rework.requested` cho Inspection. Không có approved Passed event/profile/staging. Missing decision: exact Passed notification event/recipient/source-read binding và same-graph durable UC044 receipt/replay/SQL issuer registration. Existing FE04 projection yêu cầu READINESS_OVERLAY_V1 marker, registered receipt issuer/module digest và READY-capable schema; current FS016 readiness CK không cho READY. Cần accepted schema/runtime cho adapter nguyên bản; không tự ghi READY, bỏ Blocked precedence hoặc suy luận acceptance từ domain PASS.

## UC047 persisted lifecycle candidate

- `ReinspectionPersistenceCandidate.cs`: read-only preparer reuse FAST012 exact source reader/Fixed validator. Chuẩn bị binding exact Inspection/Rework/originalInspection/task/room/branch/cycle revision, original và assigned versions riêng, cả hai source revisions, actor/time; exact selected per-defect Fixed/Not Fixed rows và SRS lifecycle plan. `PersistedOrAccepted=false`, không SQL write/backfill.
- `ReinspectionLifecycleCandidate.cs`: All Fixed => PASSED / Rework COMPLETED / task COMPLETED; any Not Fixed => FAILED / Rework FAILED / task REWORK_REQUIRED. Đây là SRS plan, `SchemaAndLifecycleAccepted=false`, không DB transition.
- `proposals/UC047_PERSISTENCE.sql.review.txt`: review-only DDL đề xuất hai bảng binding/result, PK/FKs tới exact selected `(rework_id,defect_id)`, source/cycle revisions, pinned versions, recorded actor/time và rowversions. Đề xuất trigger set-based invalidates binding + Inspection cho distinct inserted/deleted parents. KHÔNG installer/migration, KHÔNG executed hoặc SQL-verified; batching, lock-order, recursion, FK/ownership consistency, audit, immutable binding/reassignment và recovery phải được review trước khi tạo approved installer. Không sửa existing schema/CK hay cấp permission.
- `ReinspectionReviewCandidateService.cs` + DTO + `[NonController]` controller: candidate read adapter reuse existing BatchAsync/session/branch authorization; HTTP errors có mapping. MVC discovery test xác minh KHÔNG dispatch production. Không public mutation method.
- Frontend `ReinspectionReviewCandidate.tsx`: unmounted review component hiển thị exact source/version/defect remediation và choices Fixed/Not Fixed; Conclude luôn disabled, không fetch hoặc mutation transport/mock authority. Remount trên source/revision change để không carry decisions sang resource khác. Không shared router/auth changes. SSR tests không thay cho browser/live integration.

Missing decisions: explicit authorized UC041 setter + durable exact Reinspection→Rework relationship; original-vs-reassigned checklist-version policy; storage/notes/photo field/retention/audit/invalidation policy; FAILED Rework workflow/status mapping và continuation/new defects/next UC046 round; accepted notification/projection participants. Current CK_rework_workflow_v1 không có FAILED pair. Không tự thêm status production. Positive UC047 reader/persistence SQL NOT_RUN: approved owned component lane thiếu remediation schema; không sửa lane để ép PASS, không dùng FE04-owned DB hoặc dựng lane mới thay acceptance.

## UC045/046 executable owner driver

`InspectionOwnerFourteenDriver.cs`, CLI `owner-14 <output-directory>`: đã có executable case registry O01–O14, gate trước connection/fixture, exact owner entry-point references, fail/NOT_RUN receipts và evidence checks (real SQL, no synthetic authority; các ca cần committed source/different-transaction consumer phải có positive evidence). Blocked run trả exit 2, không giả PASS từ process exit 0. Default CLI dùng original InspectionClosedTrialExecution; không configuration bypass.

Đã hoàn thiện phần dispatcher/gates độc lập; 14 concrete accepted fixture action bindings vẫn chưa có. Case callbacks phải gọi actual existing coordinator/issuer/prelock/staging/replay/FE04 projection/consumer trên approved lane; metadata booleans/receipt path trong harness không phải SQL proof verifier hay authority. Không lấy synthetic tests hoặc callback-supplied evidence làm business acceptance. Không đổi N01 graph, pin R03/R05 hoặc tạo replay framework khác. Successful authoritative Reject/Rework/full replay/proof/projection và positive consumer vẫn NOT_RUN.

## Verification

- Before recovery: focused backend 40/40 PASS; frontend SSR 6/6 PASS; frontend build PASS. Vite warning: ignored react-router `use client` directives. `npm ci` báo các dependency deprecated theo lockfile hiện có, không tự upgrade.
- SQL component trước recovery: 3/3 PASS. Closed Passed binding không mutate và poison commit; negative Stage fault sau actual domain update rollback root/task/audit; second physical writer gặp lock timeout trên retained root lock. Test StageFaultOnly chỉ negative fault-injection, không thể Complete/Verify receipt, KHÔNG authority participant hoặc successful acceptance.
- Final review bổ sung explicit server/database/owner guard trên contender connection trước mutation. Cùng local synthetic FAST010 DB `HotelOps_Canonical_20261110_031050_3a074f28_Fresh`, server `DESKTOP-JI8U50T\SQLEXPRESS`, owner `2f2502b8-87cc-4185-a56a-2fcae4000c78`. Mỗi business fixture/session/mutation trong tests rollback; không shared SQL/schema install. Lock-contention test không chứng minh two concurrent service approvals/one committed winner/deadlock-free.
- Final build/SQL/driver receipts được lưu riêng trong `receipts/` khi finalize. Historical UC044 domain duplicate/stale 10 checks FAST012 và backend/frontend suites không bị ảnh hưởng giữ nguyên credit, không rerun.
- UC047 positive schema-dependent SQL, authoritative N01 14-case execution, trusted committed Passed/Reject/Rework result, positive replay/projection/FE04 consumer, live HTTPS/E2E: NOT_RUN. Component/SSR/structural fixtures không thay acceptance.

## Permission / storage

`PERMISSION_PROPOSAL.md` copy byte-for-byte FAST012 (SHA256 D2F4102BCF45F7914349EAC612E7530FFABEFC2B41A380465CCD41E90A09F6F4). Cả ba descriptors giữ exact existing grants, source relationships/summary field allowlist/OWN semantics, `acceptedConcreteBinding=null`, `private evidence=false`. Không flag hoặc frontend role/grant nào mở policy. Chờ Project Leader quyết định narrow business/security bindings và same-transaction Rework staging policy, sau đó mới có accepted concrete evaluator integration.

Existing FAST011 evidence version/content-digest/read-lease boundary giữ nguyên; không rerun unchanged file tests. Không có enforceable durable WORM/ACL/storage acceptance mới. Reread SHA256, D4 revision và local file locks không đảm bảo immutable bytes sau lease release, stable namespace/retention/no-replace-delete hoặc SQL/filesystem crash safety. Chưa approve namespace/runtime identity/retention contract nên không activate upload/proof candidate hoặc promote legacy mutable files.

## Delivery / progress

Source manifest ghi exact Added/Modified paths, working-tree và staged Git blob SHA256 (CRLF normalization được phân biệt). Chỉ scoped FE05 source/tests/docs, không Program.cs shared API, router/auth, generated entities/mappings, notification graph, FE04/DEV2/DEV3 logic hoặc release SQL. Commit/push chỉ feature branch, không force/merge/deploy. Worktree cũ không thay đổi.

Ước tính kỹ thuật: FAST013 75%; UC041 70% (unchanged), UC044 70% (composition candidate, chưa durable accepted implementation), UC045 80%, UC046 80% (không nâng từ component checks), UC047 35% (persistence/read/UI candidates, chưa persisted full workflow). Không UC ACCEPTED.

FE04_DEPENDENCY_STATUS: R03/R05_PINNED_R05_INTEGRATED_VERIFIED_ISOLATED / NOT_READY_FOR_AUTHORITATIVE_HANDOFF. R05 `95f7422e284bc81bcfcf9f460dace52022de5cc7`; DEV2 source reference `d60022b487b5b59e84d6551a0ee5162c9fd32164` giữ nguyên. Không đợi/đánh đồng FE04 UC029 với FE05 N01 acceptance, không giao task mới cho DEV khác và không phát AUTHORITATIVE_INSPECTION_HANDOFF_READY.
