# Bàn giao Storekeeper — Project 2 / CompactDB

Đã triển khai giao diện và backend để nhập, xuất theo yêu cầu, trả, chuyển nội bộ, kiểm kê, điều chỉnh, đảo và nhận bổ sung qua chứng từ thật. Kiểm thử ghi dữ liệu chỉ chạy trong database fixture độc lập. Dữ liệu chính chỉ được đọc để đối chiếu role/grant và khả năng phê duyệt.

**Điểm chặn vận hành primary:** mỗi chi nhánh DVBV và DVCT hiện chỉ có một tài khoản active có `STOCK_RETURN_POST`, `STOCK_ADJUST_POST`, `STOCK_REVERSE`. Workflow hiện hành yêu cầu người duyệt độc lập và người ghi sổ khác người duyệt. Không tự thêm tài khoản, đổi grant hay bỏ kiểm soát để vượt điểm chặn này. Các luồng đã được xác minh với hai tài khoản đúng grant trên fixture.

## Kết quả theo yêu cầu

| Hạng mục | Trạng thái | Bằng chứng / giới hạn |
|---|---|---|
| STOREKEEPER_ROLE | VERIFIED | SQL chỉ đọc: role 6, đúng 16 grants; attendance OWN, còn lại BRANCH; một Storekeeper/chi nhánh |
| SOURCE_AUDIT | VERIFIED | Bảng audit trong plan; đối chiếu controller, services, DTO, entities, mappings và SQL guards; không dùng SRS draft để phát minh quyền |
| SIDEBAR_ALIGNMENT | IMPLEMENTED / VERIFIED | 8 nhóm, icon Lucide, nút theo grants; không có quản trị, tài sản, phòng hay phê duyệt MR |
| INVENTORY_DASHBOARD | IMPLEMENTED / VERIFIED | 10 KPI có dữ liệu thật theo grant, chứng từ nhập/xuất 30 ngày, thấp định mức, yêu cầu chờ cấp, giao dịch gần đây |
| ITEM_MASTER | IMPLEMENTED / VERIFIED / PARTIAL | Tạo/sửa, unique code, active/unit/category, drawer chi tiết và lịch sử. Giữ catalog dùng chung V10; schema không có ownership mặt hàng theo branch. Chưa có cách cô lập bản ghi mặt hàng theo branch mà không đổi contract/schema |
| INVENTORY_BALANCE | IMPLEMENTED / VERIFIED | Ledger, đơn vị, định mức, biến động cuối, cặp policy chưa phát sinh tồn 0; không giả reserved/available |
| STOCK_RECEIPT | IMPLEMENTED / VERIFIED | Phiếu nhập tăng đúng 30 đơn vị trong API test, thêm 3 qua browser thật; phiên bản, key và bất biến sau post |
| STOCK_ISSUE | IMPLEMENTED / VERIFIED | Chỉ từ MR được duyệt; cấp 4 rồi 6, thêm 1 qua browser; từ chối vượt tồn/duyệt và rollback |
| STOCK_RETURN | IMPLEMENTED / VERIFIED / BLOCKED | Nguồn ISSUE, phần còn lại, phê duyệt độc lập; primary thiếu người thứ hai |
| STOCK_TRANSFER | IMPLEMENTED / VERIFIED | Xuất nguồn, nhận từng phần/toàn bộ kho đích cùng branch; không chuyển cùng kho hoặc vượt số đã chuyển. Cross-branch bị chặn theo contract |
| MATERIAL_REQUEST_FULFILLMENT | IMPLEMENTED / VERIFIED | Xem đề nghị/duyệt/đã cấp/còn lại theo đơn vị; tạo ISSUE từ dòng nguồn, không sửa phê duyệt MR, không cấp MR chưa duyệt |
| STOCKTAKE | IMPLEMENTED / VERIFIED | Lưu tồn sổ/thực đếm/chênh lệch; hoàn tất kiểm đếm không tự thay tồn; tồn đổi sau đếm phải kiểm lại |
| STOCK_ADJUSTMENT | IMPLEMENTED / VERIFIED / BLOCKED | Đúng toàn bộ chênh lệch, không điều chỉnh lặp; primary thiếu người duyệt độc lập |
| STOCK_REVERSAL | IMPLEMENTED / VERIFIED / BLOCKED | Đúng movement gốc, không đảo hai lần, không đảo chứng từ có giao dịch phụ thuộc đã post; MR/Replenishment gốc được tính lại; primary thiếu người thứ hai |
| REPLENISHMENT | IMPLEMENTED / VERIFIED | Nhận một phần/đủ, đóng yêu cầu, từ chối nhận lặp/vượt/nguồn đã đóng. Chỉ RECEIPT post thay tồn |
| STOCK_DOCUMENTS | IMPLEMENTED / VERIFIED | Tìm/lọc/sort/phân trang chứng từ, dòng nguồn, người tạo/duyệt, related drafts và events; ledger truy đến chứng từ, không cần AUDIT_VIEW |
| PERSONAL_WORKDAY | VERIFIED | Component hiện hữu: xem và start/end OWN, không phân ca hay start tương lai/ca người khác |
| BACKEND_AUTHORIZATION | VERIFIED | Grant/branch ở server; guard nghiệp vụ Storekeeper của source được giữ, không sửa role_permissions |
| BRANCH_SCOPE | VERIFIED | Cả hai phía fixture QA/OTHER kiểm tra 403; kho/dòng/chứng từ nguồn khác branch bị từ chối. Primary DVBV/DVCT chỉ kiểm tra read-only |
| TRANSACTION_INTEGRITY | VERIFIED | Create linked draft + lines + event trong Atomic Serializable; post header/ledger/source status/audit cùng transaction, no-negative và rollback |
| CONCURRENCY | VERIFIED | RowVersion, operation keys cho create/derive/post, source remaining, ledger uniqueness và SQL guards; retry không thêm chứng từ/movement |
| FRONTEND_BUILD | VERIFIED | `npm run typecheck`; `npm run build` exit 0, Vite hoàn tất; log trong artifacts/storekeeper |
| BACKEND_BUILD | VERIFIED | `dotnet build HotelOps.slnx`: 0 warning, 0 error |
| TEST_RESULTS | VERIFIED / NOT_TESTED | 38 kiểm tra fixture, 128 hồi quy API, 27 Storekeeper responsive views, browser/API/SQL thật; .NET 28 passed, 2 skipped do thiếu cấu hình test riêng, không tính là passed |
| FILES_CHANGED | IMPLEMENTED | Danh sách bên dưới; giữ toàn bộ thay đổi sẵn có ngoài phạm vi |
| REMAINING_BLOCKERS | BLOCKED / PARTIAL | Nhân sự duyệt độc lập primary; ownership item theo branch chưa có schema; cross-branch transfer không có workflow được phép; không có approval threshold theo giá trị trong source |
| FINAL_VERDICT | IMPLEMENTED + VERIFIED; primary PARTIAL | Luồng core chạy thật trên fixture. Chưa thể tuyên bố mọi nghiệp vụ trên dữ liệu chính vận hành đủ khi thiếu người duyệt; không thay quyền/dữ liệu để làm test pass |

## Bằng chứng kiểm thử

- `scripts/database/compact-v2/Test-Backend.ps1 -Storekeeper`: **38 passed**, database `HotelOpsCompactApiTestc43ed04f7228`, backup đã verify; `artifacts/compact-v2/backend/storekeeper-verification.json`.
- `frontend/tests/compact-storekeeper-live.mjs`: browser gửi đúng 5 request tạo nhập → thêm dòng → post → derive ISSUE → post, tất cả HTTP 200. SQL cuối xác nhận delta **+2**. `artifacts/storekeeper/live-verification.json`, `live-issue.png`. Token fixture chỉ truyền trong environment và được khôi phục, không lưu vào báo cáo.
- `Test-Backend.ps1 -Extended -Port 5188`: **128 passed / 0 failed**, database `HotelOpsCompactApiTest766b47f1534a`, `artifacts/compact-v2/backend/verification.json`.
- `npm run test:compact:storekeeper`: 8 nhóm, paging/search/filter/sort, source derivation, kiểm tra duyệt độc lập, lỗi tồn 409 không báo thành công, readonly Workday, chi tiết/đơn vị mặt hàng, **27 responsive views** ở 1440/768/375; API mocked để xác minh UI, tách biệt test API thật.
- Hồi quy HK Staff 27 views; Department Head 21; Operations Manager 24: passed. Bộ UX chung kiểm tra navbar, settings/branch, Reception, Workday, pagination/dashboard.
- .NET: **28 passed, 2 skipped**: `WorkerIntegrationTests.RecurrenceAndLowStockAreCommittedOnceAcrossRepeatedTicks`, `AccountManagementTests.ManagementAndResetPreserveSecurityAndRollbackOnEmailFailure`. Hai bài cần environment riêng, không chạy trong task này.
- Rà ảnh desktop/mobile/dashboard và drawer phiếu xuất thật; không overflow viewport. Trình duyệt kiểm tra không có page errors.

## Phạm vi sửa file

Backend:

- `src/HotelOps.Api/Compact/Contracts/Common.cs`: derive DTO, create operation key tùy chọn để giữ compatibility.
- `src/HotelOps.Api/Compact/Controllers/OperationsControllers.cs`: inventory, ledger, derive và list sort/date.
- `src/HotelOps.Api/Compact/Services/InsightService.cs`, `InsightService.Inventory.cs`: stock rows, dashboard và scoped ledger DTO.
- `src/HotelOps.Api/Compact/Services/DocumentService.cs`, `DocumentService.Inventory.cs`: context/capability, atomic linked drafts, source remaining, post/approval/concurrency guards, tính lại yêu cầu sau reversal.
- `src/HotelOps.Api/Compact/Services/CatalogService.cs`: validate base unit, bảo vệ unit có lịch sử.

Frontend:

- `frontend/src/compact/app.tsx`: nav/routing và reuse Workday.
- `inventory-dashboard.tsx`, `inventory-document-model.ts`, `inventory-documents.tsx`, `inventory-document-form.tsx`, `inventory.css`: workspace kho mới.
- `catalog.tsx`, `catalog-specs.ts`: inventory mode, tìm toàn bộ trước paging, nhóm/đơn vị, item drawer.
- `stock.tsx`: policy/unit/filter/sort/ledger drawer; giữ link chứng từ đúng cho role khác.
- `ui.tsx`, `api.ts`: nhãn trạng thái và lỗi nghiệp vụ tiếng Việt.
- `frontend/package.json`, `frontend/tests/compact-storekeeper.mjs`, `compact-storekeeper-live.mjs`: lệnh và kiểm tra UI/real API.

Fixture/docs:

- `scripts/database/compact-v2/Test-Backend.ps1`, `storekeeper-fixture.sql`, `test-storekeeper.ps1`.
- `docs/ui/compact-ux.md`, plan, báo cáo này và journal phiên làm việc.

Không sửa generated entities, mappings, migrations, schema, permission definitions, role grants hay dữ liệu Staffing. Không ghi test primary, không thay Project 1, không commit/push/deploy. Database fixture được giữ để kiểm tra; process API fixture dừng trong finally. API local primary khởi động lại cổng 5768 với worker tắt, FE cổng 5186 được giữ.

## Các lỗi đã sửa trong quá trình xác minh

- Giữ bất biến posted header: tạo chứng từ con chỉ thêm event, không touch header đã post.
- Chặn sửa dòng đã duyệt; kiểm tra kho đích active/cùng branch, cùng kho bị chặn; tổng hợp các dòng cùng nguồn trước kiểm tra số lượng.
- Chặn đảo khi có chứng từ phụ thuộc; sau đảo tính lại nguồn MR/Replenishment chưa đóng; derived transfer status lọc đúng trước paging.
- Sửa React initializer `crypto.randomUUID` để giữ receiver; dùng role locator đúng với select trong bài browser, và số request thực tế là 5.
- Fixture Workday phải có EMPLOYMENT hợp lệ và dùng route/version hiện hữu; không nới eligibility để test pass.
- Một lần test settings chạy chung báo mất branch khi điều hướng; chạy lại riêng và toàn bộ suite xác minh lại, không sửa production chỉ để vượt test.
