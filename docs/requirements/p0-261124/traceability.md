# P0 — traceability, khoảng trống và tiêu chí bàn giao

> Cập nhật nghiệm thu 24/11/2026: [báo cáo hiện tại](implementation-report.md) và [bằng chứng thực thi](validation-evidence.md) thay thế các đánh giá NOT_TESTED/PARTIAL tại mốc audit ban đầu bên dưới. Giữ nội dung audit để theo dõi lịch sử; 28 nhóm E2E đã chạy, không suy rộng thành mọi tổ hợp hoặc Production đã triển khai.

Đối chiếu source ngày 24/11/2026. IMPLEMENTED không có nghĩa VERIFIED. Mọi dòng runtime bên dưới là **NOT_TESTED** tại mốc lập tài liệu; Integration Owner cập nhật bằng kết quả thực tế sau cùng. Không tuyên bố SRS mới hoàn tất khi source/kiểm thử còn thiếu.

## A/B → source → acceptance

Đường dẫn service dưới `src/HotelOps.Api/Compact/Services/`, UI dưới `frontend/src/compact/`.

| Nguồn/UC | Source chính | Trạng thái source | Acceptance |
|---|---|---|---|
| A role consolidation; B actors | p0-role-migration.sql; Access.cs; permission-matrix.md; nav.ts | IMPLEMENTED | E2E-01/27; old active assignments=0, eight logins, session revoke, lịch sử/user/password không đổi |
| B branch config; UC-P0-01 | SupplyConfigurationService; p0-supply-defaults.sql; supplies UI | IMPLEMENTED | E2E-02; tám A, version/branch isolation, một kho |
| B start allocation; UC-P0-01 | WorkdayService; StaffSupplyService; DocumentService.Supplies | IMPLEMENTED | E2E-03/04; full issue hoặc SHORT, retry, không stock âm |
| B cleaning; UC-P0-02 | WorkService; StaffSupplyService.Reservations; PaperInspection | IMPLEMENTED | E2E-05…08; reserve capacity, swap/cancel atomic, consume once, rework |
| B refill; UC-P0-03 | RequestWorkflowService.Actions; StaffSupplyService.Refill; requests.tsx | IMPLEMENTED | E2E-09…12; giám sát/Store scope, full issue, reject reason, competing stock |
| User manual end; UC-P0-04 | WorkdayService; ShiftExpiryService; StaffSupplyService.EndBlocker; workday.tsx | IMPLEMENTED | E2E-13/14; overtime existing work, no new refill/assignment, return once |
| B low stock; UC-P0-05 | LowStockService; OperationsWorker; SupplyConfigurationService | IMPLEMENTED | E2E-15; 50/49/50 episode + writer/scheduler race |
| B replenish; UC-P0-05 | RequestWorkflowService; DocumentService.ReplenishmentWorkflow | IMPLEMENTED | E2E-16; OM approval no stock, real partial receipts/retry |
| B durable; UC-P0-06 | WorkService.RoomIssues/Actions/Config; DocumentService.RoomRemediation | IMPLEMENTED | E2E-17…19/23; assigned B only, remaining qty rework, no false remedy |
| B damage; UC-P0-07 | RoomIssueActions; WorkService.Maintenance; room-issues.tsx | IMPLEMENTED | E2E-20…23; HK/Reception, external supervisor, repeated inspection, occupancy |
| B readiness; UC-P0-08 | RoomReadinessService; p0-readiness.sql; RoomService; PaperInspection | IMPLEMENTED | E2E-24; independent blockers, safety hold, cleaning/turnover evidence |
| B unified; UC-P0-09 | RequestWorkflowService/Domain/Visibility; p0-requests.sql; requests.tsx | PARTIAL | E2E-25/26; complete typed/domain links, sorting/display names and historical deeplinks review |
| A cross-branch; UC-P0-10 | DocumentService.Transfers/Inventory; NotificationService | IMPLEMENTED backend; UI coverage must be checked | A TEST09…12; source/destination authority, partial dispatch/receive, conservation |
| A assets; UC-P0-10 | AssetService; assets.tsx; target ASSET grants | Existing implementation retained; full A acceptance NOT_TESTED | A TEST05; move/handover recipient, no cross-branch leak, immutable event history |
| A operational self-review; UC-P0-11 | WorkService; p0-workflow-guards.sql | IMPLEMENTED | A TEST06/07; selfverify OPERATIONAL only, no inspection/repair bypass |
| User internal MR policy | DocumentService.ApprovalAuthority; p0-store-policy.sql | IMPLEMENTED | Store approves another employee request, OM denied, no selfapprove/overissue |
| A dashboard/Action Center | InsightService.Dashboard; dashboards.tsx and existing overview | PARTIAL | A TEST13; actual actionable records, source links, role-correct actions |
| A reports | InsightService.Report and existing reporting UI | Existing subset retained; full A acceptance NOT_TESTED | Branch/permission per domain, transfer participants, export scope |
| B notifications/audit | NotificationService; RequestWorkflowEvents; LowStockService; Work/Document events | IMPLEMENTED, delivery/runtime NOT_TESTED | No duplicate retry notice; removed-role recipient denied; correct queue/branch |
| B concurrency/reconciliation | Atomic, rowversions, SQL guards, supply/request locks; runtime runner | Test scaffolding PARTIAL | E2E-12/28 + all retry/race cases; actual consistent DB snapshots |

## A chưa được B thay thế: không được bỏ sót

### Dashboard và Action Center

Source `InsightService.Dashboard` có phòng, operational task status, stock, scheduling, monitoring/overdue attention, asset condition và pending counts. Phần `attention` hiện đọc work quá hạn của các kind; không tự chứng minh đã có danh sách đầy đủ:

- transfer request chờ OM nguồn duyệt và hàng đang vận chuyển;
- task chờ phân công/chờ xác nhận, ngoài quá hạn;
- asset handover chờ đúng người nhận;
- chênh lệch stocktake và ca cần kiểm tra bàn giao;
- warehouse replenishment chờ OM với link đến typed request gốc.

Đây là **PARTIAL** so với A §17.2. Không tạo sidebar/module/trạng thái Action Center riêng. A từng yêu cầu OM duyệt MR thường; mục đó đã bị quyết định B/người dùng thay bằng Storekeeper, nên Action Center OM không được khôi phục nút duyệt MR thường. Sự cố/IR overdue chỉ là giám sát.

### Cross-branch transfer

Backend/source contract có đường yêu cầu/duyệt/dispatch/partial receipt/cancel và notification visibility của hai bên. Chưa có runtime evidence cho notification delivery, inactive item/warehouse/unit changes, concurrent receipt hay discrepancy handling đầy đủ. `frontend/tests/compact-cross-branch-transfer-live.mjs` là test cần thực thi, không phải kết quả.

UI A §21 cần phân biệt đã gửi/nhận được/lịch sử, nguồn/đích và lượng thực nhận; kiểm tra trên cả OM và hai thủ kho. Một route documents chung không tự chứng minh UX này hoàn chỉnh. Reversal xuyên cơ sở hiện bị chặn, không tự tạo bù trừ chưa được duyệt.

### Assets và reporting

`AssetService` hiện có move cùng branch, condition, handover/accept/reject, retire và events. Quyền mới OM được cấp không tự chứng minh audit trước/sau hay nhận bàn giao đáp ứng mọi scenario A. Ví dụ account/membership/recipient đổi giữa lúc offer và accept phải được thử; không cho Owner ALL-view thành quyền accept.

Reports hiện có assets/rooms/materials/workday/stock/documents và các domain hỗ trợ. Đối chiếu A §18 theo từng nguồn dữ liệu thật; thiếu report transfer hoặc ledger reconciliation phải đánh dấu thiếu, không tái dùng stock snapshot như báo cáo lịch sử có số dư đầu kỳ. Không giả KPI khi API không trả dữ liệu.

## Khoảng trống integration cần kiểm chứng

1. Generic Work/Document bypass guards cho typed roots phải bao trùm action, assignment, link/checklist/evidence và derive/post, không chỉ nút UI.
2. Room backfill/typed metadata cần đúng source stage, parent/correlation và handler cho hồ sơ trước P0. Legacy generic incident không được nhầm room issue khi deeplink.
3. API request list hiện có search/queue/page, chưa có sort parameter; bổ sung contract thật nếu giữ yêu cầu sorting toàn dataset. Không sort cục bộ một trang rồi gọi là server sort.
4. Handler DTO dùng user ID ở snapshot UI handoff; tên hiển thị cần projection được phép, không bịa tên. Kiểm tra filter supply mặc định ẩn, mobile toàn màn và accessibility.
5. Mọi nút End ngoài Workday cũng phải dùng canEnd. Consume/existing-work guard phải cho hoàn tất sau giờ; Reserve/refill vẫn trong giờ. Generic task assignment vs End phải được race-test.
6. Low-stock reconciliation cần mọi writer: supply, receipt, ordinary post/reverse/transfer và policy CRUD; source worker bàn giao không đủ để chứng minh mọi hook cuối đã tích hợp.
7. Group B không được đoán từ catalog; cấu hình và đồ đã gán phải tồn tại trước khi thiếu-vật-dụng test có ý nghĩa. Không mở rộng dropdown sang đồ chưa gán để test chạy qua.
8. Eight role runtime cần tài khoản/ca fixture riêng; tạo fixture không thay credential/historical data thật. Cảnh báo privacy/tooling trong worker fixture phải được giải quyết trước khi gọi bootstrap thành công.

## Ma trận test bắt buộc

Danh sách 28 scenario chính xác ở [continuation acceptance](continuation-acceptance.md#mandatory-real-e2e-27) và [runtime plan](runtime-test-plan.md). Toàn bộ tại thời điểm tài liệu: **NOT_TESTED**; một số source-contract tests đã chạy không thay thế danh sách này.

| Nhóm | Kiểm chứng thực tế cần lưu |
|---|---|
| Migration | Before/after user count/hash, revoked roles/sessions, membership, shifts cũ/mới, rerun no-op |
| Authorization | 8 actor, foreign branch, Owner writes denied, Admin business denied, OM ordinary action denied, HK stock denied |
| Supply | Opening/closing stock, từng ledger/doc/reservation, shortage không partial fake, assignment/retry/return |
| Concurrency | Hai stock issues cùng hàng, hai claims, approve/reject, Start/Complete/End/Inspection retry, config change vs assignment |
| Remediation | B/C exact room objects, wrong report reject, missing shortage/reissue, repair repeat, multiple blockers, occupancy preserved |
| UI | Actual API data, F5 persistence, wrong-role routes, modal validation/error, mobile no horizontal overflow |
| Reconciliation | Cả hai branch: opening + receipt − issue + return đúng cách phân loại = closing; staff issued − used − returned = remaining |

Runner kết thúc nonzero khi còn scenario missing/fail; báo cáo NOT_TESTED phải được giữ nguyên, không convert thành PASS. Một expected business error không phải test đạt trừ khi scenario assert đúng code, không mutation và invariants sau lỗi.

## Bàn giao cuối

Final report cần đủ keys của B §31, phân biệt implementation/build/tests/runtime và liệt kê files chính theo module. Gắn bằng chứng thực tế cho DATABASE_MIGRATION, USER_COUNT_BEFORE_AFTER, ROLE_PERMISSION_MATRIX, CONCURRENCY_TESTS, INVENTORY_RECONCILIATION và E2E_TESTS. Không lấy tên file test làm evidence chạy test.

Hiện chỉ migration TEST, backup/restore và build snapshot đã có xác nhận từ Integration Owner. Source/API docs không đóng các phase runtime. FINAL_VERDICT chưa thể là hoàn thành toàn P0; Production deployment và Git mutation vẫn không được phép trong đợt này.
