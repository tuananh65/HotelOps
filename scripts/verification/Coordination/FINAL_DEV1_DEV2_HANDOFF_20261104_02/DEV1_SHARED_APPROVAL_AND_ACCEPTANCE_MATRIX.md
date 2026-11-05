# Shared diff / acceptance / independence

Trạng thái quyết định lấy từ COORD-FINAL-01. Source evidence không tự cấp quyền sửa shared file. Coordination task chỉ tạo handoff artifacts; build/SQL/HTTPS NOT_RUN, không UC completion mới.

## Approval matrix theo exact change

| Change | Design/business | Exact implementation status | Việc tiếp theo / owner |
|---|---|---|---|
| Notification A01 interface | APPROVE / ACCEPTED theo chủ dự án | PENDING_EVIDENCE trong local snapshot | DEV2 gắn exact accepted source/namespace/signatures/blob vào submission; không mở lại quyết định interface vì local chưa có |
| A02 structural | DEV2 reported completed | PENDING_EVIDENCE; không runtime accepted | DEV2 source/compile evidence; structural success không thay delivery/retry acceptance |
| B01 three-table architecture | APPROVE DESIGN ONLY | executable DDL REVISE | DEV2 revised exact DDL trên actual inbox composite key, stable replay/fingerprint/constraints/retention/retry, rollback/idempotent/legacy tests |
| B02 additive EF | APPROVE architectural SQL FK, không sửa generated NotificationRecipient | EF mapping proposal REVISE | DEV2 entities + DbContext partial exact diff; model/SQL parity and FK negative tests; không thêm HasPrincipalKey vào generated model ngầm |
| C01 operation/commit guard | Fail-closed activated producer requirement APPROVE | Guard/DI REVISE, Program hunk chưa approved | DEV2 context/tx identity + completion/persistence guard; sync/async/raw bypass/rollback/savepoint/ambiguous commit evidence; base current Program hash |
| Room occupancy/readiness/block | APPROVE business model/cardinality/readiness | Room SQL/EF/coordinator/DI NOT APPROVED | Joint exact mapping/default/index/current-cycle/history diff, DEV1 Reception/inspection boundaries, DEV2 incident release adapter |
| Global lock order | Partial source graph only | BLOCK universal-order claim; PENDING_DEV2_CONFIRMATION edges | DEV2 writer sequences + both-order SQL tests, không dùng retry sau SQL1205 để PASS |
| UC139 B1.2 | APPROVE current reason≥10/single eligible primary DH/cross-user read/PauseAll atomic rules | Existing B12 review proposal NOT APPLIED; shared extension/DI/read-scope pending exact approval | DEV1 source adapters/read boundary; DEV2 accepted publisher/replay and Maintenance/PM adapter source/evidence |
| UC012 Branch Deactivation | APPROVE ownership/active-branch cooperation/no-race principles | BLOCK activation; DEV1 endpoint fail closed | DEV2 FE06–10 exact writer guard placements/status/scope; DEV1 conditional deactivation + audit after coordinated acceptance |
| Task86 A + B | A one-line DI approved/applied; B SQL design + separate trial permission | Historical bounded scope accepted, not a Notification/shared DB approval | Preserve current Branch service registration; do not copy trial permissions to new execution |
| RBAC/explicit-user source-read | Không tạo permission mới trong task | New shared evaluator/bindings/grants NOT APPROVED | Exact proposal nếu actual source-read requires new binding; existing policy reuse không là role/grant change |

## DEV2 có thể tạo exact shared diffs ngay

1. B01 revised SQL: lấy actual notification/recipient shape + unique triple từ source excerpts, không chờ DEV1 cung cấp lại schema. Không auto-backfill/purge. Nêu mọi key/status/nullable linkage và negative integrity tests.
2. B02 ba entities/mappings: current Context có `ConfigureWorkTime(modelBuilder)` tại :3611 rồi `OnModelCreatingPartial(modelBuilder)` tại :3612; chưa tìm thấy partial method implementation trong current Data/. Có thể **đề xuất** một additive partial implementation cho Notification model, preserve WorkTime call/DbSets/mappings và không sửa generated NotificationRecipient. Đây là source-ready option cần exact B02 review, không approval tự áp dụng.
3. C01: submission gồm actual new guard classes và Program diff base SHA256 586299FB650342F7389BAECD615DD29ACC94352857021FC4391554FF9372A505. Giữ tất cả scoped A1 aliases và Task82/84/85/86 registrations; không middleware reorder/config/auth/RBAC rewrite. Options/interceptor wiring phải nêu scoped lifetimes, same-instance behavior và raw-commit boundary.
4. Room migration/coordinator: exact defaults + legacy mapping + filtered cardinality indexes + current-cycle/passed/block predicates + history + same tx interface. Không apply Room changes vì business decision đã chốt.
5. Source read/recipient policy: current route permission mismatch và own-only Workday gap đã xác định; DEV2 đưa evaluator/source adapter API cụ thể, DEV1 review shared semantics. Không cần thêm broad evidence round để xác nhận các gaps đã liệt kê.
6. UC012 DEV2 writers: Incident Create/status reopen; Maintenance Create/Start/Return/Assign; Warehouse new/change/reactivation và stock posting/transfer/receipt; MR Save/Command/Issue; Replenishment Create/Receive/receipt-post. Mỗi patch nêu authoritative BranchId, final retained guard, existing lock order, re-read và semantics historical close/reverse. Không tự sửa DEV1 Branch writer.

## Acceptance gate bắt buộc trước integrate/runtime accepted

| Contract | Actual acceptance phải cung cấp |
|---|---|
| B01/B02 | Fresh/legacy schema parity; installer idempotency; transaction DDL rollback/recovery; FK mismatched user/channel/publication reject; delivered-without-inbox reject; EF translation/model without generated model overwrite |
| Operation replay | Same actor/key/same normalized request original result; payload conflict; concurrent replay; response loss/day later ended/source state changed; current authorization recheck; no duplicate business/audit/publication/intents; V1 ledger retained |
| C01 | Activated producer completion required; missing/unsaved mandatory intent fails before commit; mismatched DbContext/physical tx refuses; direct EF/ADO sync+async bypass negative tests; rollback/savepoint cleanup; ambiguous commit resolution by durable replay |
| Delivery/JOB03 | Actual readable inbox; dedup crash/restart; claim concurrency; max-backoff timing; six exact terminal reasons; transient DB/policy dependency failure stays retryable; no business re-execution or latest-recipient recomputation |
| Producers | Real writer/source-read endpoints, SQL-backed actor authorization, branch/ownership/eligibility, immutable event source+recipient identity, atomic audit+lifecycle+intent; UC044/045 authoritative final completer, not assignment inference |
| Room | Current passed inspection + effective block rules, ExpectedReleaseAt non-release, own-incident release only, multiple incident/manual block uniqueness, occupied+blocked, check-in/out/move both-order races and rollback |
| Workday/PauseAll | All supported kinds real pause+history+interval+End atomic; PM same participant/context; unsupported kind fails closed; no fake PM PAUSED; own-only preserved; DH cross-user source read + durable notification + replay |
| Branch deactivate | Independent blocker cases + authoritative PM open-work predicate; no blocker success; stale/no-op/repeat/audit rollback; Create→Deactivate and Deactivate→Create for every relevant writer; inactive branch cannot receive new open work |

SQL/HTTPS chỉ trên disposable DB được duyệt riêng. Framework 0 cases không PASS; test doubles/structural build không chứng nhận delivery. SQL1205 concurrency acceptance FAIL, kể cả retry thành công.

## DEV2-owned feature independence

| Feature | Có thể làm ngay trong ownership | Chỉ cần phối hợp khi chạm shared boundary |
|---|---|---|
| FE06 Incident/Maintenance | DTO/controller/service validation, server actor, approved policy binding, corrective workflow/history/tests | RoomBlock lifecycle, Branch retained lock protocol, WorkInterval/PM PauseAll, mandatory notification participation |
| FE07 Asset | Asset own validation/queries/audit/location workflows/tests | Room/branch/PM shared identity/locks, new schema/RBAC/DI |
| FE08 Inventory | Internal document/quantity/warehouse validation and own service/test implementation | Shared branch-active guards, user eligibility, cross-service posting tx, schema/DI/RBAC |
| FE09 Material Request | Approval/remainder/fulfillment rules, own API/policy/DTO/tests | Authoritative actor/branch, inventory participant/notification contract and shared lock protocol |
| FE10 Replenishment | Coverage/receipt/remainder logic and own APIs/tests | Inventory receipt same tx, material source ownership, branch-active guard and mandatory publications |
| FE14 Notifications/Dashboard | Worker/publisher/replay/inbox services, DTOs/controller/tests theo A01/business decisions | B01/B02/C01 exact schema/EF/DI, source-read evaluator, DEV1 producer/lifecycle integration |
| FE15 Reporting/Audit | Read projections/pagination/export/own audit/report DTO/tests | Common audit contracts, cross-user/branch source policy, shared schema/DI |

Không yêu cầu DEV1 approval cho từng DTO/service/controller/test nội bộ. Việc đổi shared interface/schema/DI/RBAC hoặc DEV1 writer/lifecycle cần file-specific review. Handoff không giao DEV2 khôi phục blind historical DEV1 common partials.

## Thông tin thực sự còn thiếu, không lặp discovery đã đủ

- DEV2: exact accepted A01/A02 source và revised B01/B02/C01 files/evidence; external Joint Review/Addendum originals nếu cần approval provenance ngoài yêu cầu hiện tại. Không có những file đó trong inspected worktrees/refs; không nói chúng chưa tồn tại ở máy DEV2.
- DEV1: current UC029/041/046 restoration plan/runtime; UC037 Cancel, UC044/045 writers/current-cycle final-completer persistence, supported Cleaning/Rework/Task PauseAll adapters, recipient-readable endpoints.
- DEV2: Maintenance/PM occurrence identity/source adapter/locks và UC012 predicates/guards cho own writers.
- Joint: exact room legacy migration, source-read bindings, missing lock edges and cross-feature race evidence. Chủ dự án đã chốt principles; chỉ review implementation-specific diffs, không hỏi lại business decisions đã nêu.

Consolidation không làm tăng tiến độ: 26/75, 60/256, 23.4375%; UC012 PARTIAL, UC037 PARTIAL, UC139 BLOCKED_EXTERNAL. Không bắt đầu Task87.

## Quy tắc song song bổ sung — có hiệu lực cho bản handoff này

Codex B đang làm các UC DEV1 độc lập trong worktree riêng. COORD-FINAL-01 chỉ kiểm chứng current source của worktree DEV1 chính và shared contracts liên quan DEV2. Không bắt đầu implementation thuộc Codex B; không lấy source từ worktree B làm current runtime DEV1.

Danh sách worktrees được khám phá lại bằng `git worktree list --porcelain` tại cả preflight và postflight, không dùng số lượng hoặc danh sách đường dẫn cố định. `evidence/PARALLEL_PREFLIGHT.json` và `evidence/PARALLEL_POSTFLIGHT.json` ghi exact path, HEAD, branch, index SHA256 và status từng worktree. Bản này chỉ đọc metadata Git ở các worktree ngoài DEV1 chính; không capture lại source của Codex B.

Không sửa/reset/clean/checkout/chuyển branch/đồng bộ/di chuyển/xóa worktree Codex B. Không ghi Git config hoặc index. Hoạt động đồng thời của B, nếu có, được ghi riêng; không nhận là thay đổi do COORD-FINAL-01 và không tuyên bố đã kiểm chứng implementation của B.

Trước tích hợp Codex B phải có bàn giao exact diff, base HEAD/branch, danh sách file và SHA256; review conflicts với toàn bộ local patches DEV1/shared contracts hiện tại; xin approval riêng cho phạm vi tích hợp cụ thể. Chỉ tích hợp và chạy verification phù hợp sau approval. Cùng HEAD không có nghĩa cùng working-tree source. Không tự động đồng bộ branch hoặc áp dụng diff của B.

Các kết luận về tìm kiếm ở worktrees trong bản capture trước chỉ là evidence lịch sử. Bản handoff này không khẳng định source của worktree B vẫn sạch hoặc không có publisher tại thời điểm B đang làm việc. Source/runtime được bàn giao vẫn là current DEV1 chính, đã đối chiếu hash trước/sau. Thay đổi source được dựa vào trong DEV1 chính phải dẫn tới `SNAPSHOT_INCONSISTENT`; thay đổi metadata của B chỉ được ghi nhận riêng khi không làm thay đổi source được dựa vào.

Không thay đổi approval boundary: business/architecture decisions đã chốt không tự cấp quyền áp dụng shared DDL/EF/DI/RBAC/coordinator. DEV2 có thể triển khai các thành phần nội bộ thuộc ownership theo contract; shared diff vẫn phải có file-specific review và approval riêng.

