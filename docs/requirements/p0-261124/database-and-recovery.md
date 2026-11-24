# P0 — database, migration và recovery

> Cập nhật nghiệm thu 24/11/2026: [báo cáo hiện tại](implementation-report.md) và [bằng chứng thực thi](validation-evidence.md) thay thế các đánh giá NOT_TESTED/PARTIAL tại mốc audit ban đầu bên dưới. Giữ nội dung audit để theo dõi lịch sử; 28 nhóm E2E đã chạy, không suy rộng thành mọi tổ hợp hoặc Production đã triển khai.

## Phạm vi và bằng chứng

- Target duy nhất: `HotelOpsCompactWorkdayTestP0261124`, clone TEST biệt lập. Primary `HotelOpsCompactDB` và Production không thuộc cutover này.
- Baseline được đọc: schema 2.1.0, 40 bảng, 160 FK, 82 check, 27 trigger; `artifacts/p0-workflow/preflight.json`.
- Backup: COPY_ONLY, CHECKSUM; RESTORE VERIFYONLY và restore thực tế đã thành công. Metadata tại `artifacts/workday/backup-HotelOpsCompactDB.json`; không sao chép connection string/credential vào tài liệu.
- Integration Owner xác nhận TEST đã commit 3.0.0, 49 bảng, 205 FK, 97 check, 38 trigger, rollback/idempotency migration đạt. `p0-finalize.sql` kiểm đúng số lượng và mọi constraint/trigger đang bật/trusted trước khi đổi schema property.
- Đây là **VERIFIED trong phạm vi migration được Owner báo cáo**, không chứng minh đủ mọi business E2E. Cần giữ transcript/capture commit và rerun cùng báo cáo cuối; không có artifact kết quả runtime giả định.

## Bảng mới và trách nhiệm

Mapping: `Compact/Data/HotelOpsCompactDbContext.Supplies.cs` và `.Requests.cs`; entities: `Compact/Entities/Supply.cs`, `RequestWorkflow.cs`. Không đổi thành kiến trúc nhiều project; không EF migration tự sinh ngoài SQL được rà soát.

| Bảng | Khóa/liên kết quan trọng | Ý nghĩa |
|---|---|---|
| supply_configurations | configuration, branch, item hoặc asset, warehouse, unit, revision | Cấu hình A/B/C theo chi nhánh, revision lịch sử và current |
| staff_supply_allocations | allocation, branch/user/workday assignment | Một lần cấp theo ca, SHORT/OPEN/CLOSED |
| staff_supply_balances | allocation + configuration/item/warehouse/unit | Snapshot định mức và issued/reserved/used/returned |
| supply_reservations | balance + work + room | Giữ chỗ theo sức chứa và snapshot per-guest; trạng thái xử lý |
| supply_stock_documents | document + allocation/warehouse/request | Chứng từ INITIAL/REFILL/RETURN có provenance, operation key |
| staff_supply_ledger | balance + optional work/reservation/document line | Lịch sử tăng/giảm cá nhân, actor/time/key |
| request_workflows | work PK/FK, branch, queue role/handler, optional allocation/document/parent | Điều phối typed request trên WorkItem hiện hữu |
| request_workflow_lines | request + item/unit, optional source document line | Lượng yêu cầu/duyệt và snapshot stock/minimum |
| request_workflow_events | request/branch + actor/role, optional document | Append-only chuyển trạng thái/queue/handler, version/key/hash/correlation |

WorkItem, InventoryDocument, inventory ledger, memberships, user roles, workday và room events vẫn là dữ liệu nghiệp vụ gốc. Không lưu toàn bộ mô hình vật tư/request mới trong JSON tự do. Một số event room issue cũ còn snapshot JSON phục vụ lịch sử; typed coordination bổ sung không viết lại lịch sử đó.

## Bất biến database/service

Rowversion ở các aggregate thay đổi; unique operation/provenance và trigger bảo vệ số lượng, liên kết, bất biến lịch sử. Writer chạy transaction và khóa điều phối, không bật success trước commit. Không vô hiệu hóa trigger/check/FK để cho workflow đi qua.

- Stock writer là đường duy nhất phát sinh ledger; SUPPLY_ISSUE/SUPPLY_RETURN có writer giới hạn, không phải endpoint generic cho nhân viên.
- Balance không âm; available không âm; reservation giải phóng/consume chỉ một lần; cấu hình ca không bị đổi theo revision mới.
- One-active-warehouse unique filtered index bảo vệ concurrent create/activate; service chặn tắt kho cuối.
- Room blocks được đổi từ unique một block mở/phòng sang index cho nhiều block độc lập. Release chỉ block đúng nguồn; không xóa block sự cố khác.
- Readiness view là chính sách chung cho đọc và admission, giữ occupancy/turnover, kiểm tất cả blocker; checklist giấy có kết quả kiểm tra thật.
- Ngoại lệ OM self-review chỉ OPERATIONAL có audit trước; không bỏ nguyên tắc verifier độc lập ở các loại khác.

## Thành phần migration

Các script ở `scripts/database/compact-v2/`; runner `Apply-P0Test.ps1` dùng User Secrets và chỉ nhận tên database TEST P0. **Không chạy runner với danh sách mặc định để coi là toàn bộ P0**: mặc định chỉ gồm ba script đầu, integration phải chỉ định đủ danh sách đã rà soát theo phụ thuộc.

| Script | Phụ thuộc/tác động |
|---|---|
| p0-role-migration.sql | Allowlist tám role, revoke role/grant cũ, chuyển assignments/membership/future shifts, revoke sessions, audit |
| p0-supplies.sql | Sáu bảng supply, guards và document kinds chuyên dụng |
| p0-store-policy.sql | Storekeeper MR_APPROVE_L1; một kho hoạt động; điều kiện trigger MR hẹp |
| p0-readiness.sql | Nhiều room blocks và policy view thống nhất |
| p0-workflow-guards.sql | Điều kiện room-remediation và ngoại lệ OM operational self-review có evidence |
| p0-requests.sql | Ba bảng coordination/lines/events, checks/triggers |
| p0-room-request-backfill.sql | Attach typed metadata vào room roots lịch sử được hỗ trợ, giữ nguồn/lịch sử |
| p0-supply-defaults.sql | Metadata tám fill, policy, cấu hình thiết bị/type báo cáo thiếu; không tạo stock |
| p0-finalize.sql | Sau toàn bộ: validate catalog counts/trust và schema version 3.0.0 |

Các script cùng sửa trigger phải giữ marker/điều kiện của nhau; kiểm thứ tự trên clone và chạy lại idempotent. Room backfill cần request schema; defaults cần supplies, role, store policy. Không chạy backfill trước prerequisite hay finalize trước các bước còn lại.

Role migration giữ ID, account status, mật khẩu và history. Session liên quan bị revoke để quyền cũ không còn được dùng; không bật lại token cũ. Future chưa bắt đầu được chuyển template đúng role; quá khứ giữ FK cũ. Ca role cũ đang chạy, trùng lịch/capacity hoặc trách nhiệm bảo trì chưa rõ phải chặn cutover có dữ liệu chỉ rõ, không tự xóa.

Kho phụ chỉ được ngừng nếu hoàn toàn không có tham chiếu và xác định duy nhất kho còn lại. Nếu cả hai có dữ liệu, rollback và cần phương án đối soát; không gộp stock bằng tên kho. Default catalog chỉ match tên/unit đã kiểm tra, dừng khi ambiguous; không ghi đè cấu hình đã sửa/tắt. Nhóm B không tự đoán từ tên đồ.

## Validation cần giữ bằng chứng

1. Backup metadata và restore thực tế; xác minh database target trước kết nối writer.
2. Snapshot users/count/hash đối chiếu bảo toàn, active role assignments, membership, sessions, future/past shift, warehouse references, open work.
3. Apply trong outer transaction `-Rollback`, đọc mọi batch, DBCC CHECKCONSTRAINTS; không có violation, disabled hoặc untrusted guards.
4. Commit trên clone; chạy lại để chứng minh không thêm allocation/grant/membership/event migration trùng; chụp schema contract 3.0.0.
5. Build host, khởi động riêng với clone, chứng minh PID/port/SQL session target. Không chỉ tin field database trong fixture JSON.
6. E2E/concurrency/authorization và đối soát stock/staff; [test plan](runtime-test-plan.md). Không đánh đồng SQL migration chạy được với nghiệp vụ chạy đúng.

Fixture thêm actors/branches/items TEST phải ghi provenance rõ, không đổi mật khẩu/tài khoản thật. Hàng nhận mô phỏng qua API fixture phải được gắn TEST; không trình bày như hàng thực nhận hay tạo stock cho primary.

## Recovery

Nếu migration lỗi trước commit: outer transaction rollback, giữ output lỗi, kiểm lại target/schema/count/trust, sửa script trên source và thử clone lại. Không chữa lỗi bằng tắt constraint hoặc xóa dòng lịch sử.

Nếu cần quay lại sau commit: dừng **đúng host TEST** đang trỏ clone, lưu capture hiện trạng và kết quả kiểm thử, phục hồi từ backup pre-migration đã kiểm chứng vào target clone được duyệt. Không restore đè primary. Xác minh tên database tuyệt đối, connection target và process trước mọi restore. Khởi động lại với source/schema tương ứng; session cũ không được phục hồi thành quyền dùng lại như một shortcut.

Rollback schema tùy tiện trong clone đã có giao dịch P0 mới có thể mất quan hệ lịch sử; ưu tiên restore clone và chạy lại fixture có provenance. Không có script downgrade Production được phê duyệt ở đây. Source recovery không dùng git reset/discard vì worktree chứa thay đổi chung của nhiều người.

## Kết quả chưa có ở mốc tài liệu

NOT_TESTED: API tám role, đầy đủ 28 E2E, concurrent writers, inventory reconciliation hai chi nhánh, browser mobile. Build cuối sau mọi sửa đổi và nhật ký dữ liệu before/after cuối vẫn thuộc Integration Owner. Không tuyên bố user count/hash giữ nguyên đã VERIFIED nếu chưa có output so sánh.
