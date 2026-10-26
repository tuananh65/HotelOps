# DEV1 Backend Scope & Implementation Gap Audit — Task 16

Ngày audit: 26/10/2026, múi giờ Asia/Saigon. Đây là hồ sơ phân tích; không triển khai API, không thay đổi database hoặc Permission Matrix.

## Kết luận

Authentication UC-001/002/003 có controller/service/DTO. Authorization Foundation là hạ tầng kiểm tra quyền, **chưa phải** API quản trị tài khoản, role, assignment hoặc workflow Housekeeping. Canonical Database Trial xác minh cấu trúc 82 bảng; không chứng minh đã có API nghiệp vụ tương ứng.

SRS có **144 UC**, gồm UC-141 View User List, UC-142 View Room Types List, UC-143 View Incidents List, UC-144 View Maintenance List. [Coverage matrix](USE_CASE_COVERAGE_MATRIX.md) có một dòng cho từng UC, không tự đặt UC mới. Actor và mô tả lấy từ phần đặc tả chi tiết; permission mapping được đánh dấu là đối chiếu catalog, không phê duyệt quyền mới.

Theo ownership trong AGENTS.md: **75 UC DEV1**, **67 UC DEV2**, **2 UC shared (063/064)**. Trong 75 UC DEV1, chỉ UC-001/002/003 có implementation endpoint tương ứng; **72 UC còn lại chưa có API chuyên biệt**. Một số có primitive/adapters tái dùng được; con số 72 là endpoint coverage gap, không có nghĩa cả 72 UC đều sẵn sàng coding hoặc cần 72 endpoint riêng.

Task đầu tiên đề xuất: **UC-141 — danh sách users có phân trang, tìm kiếm và bộ lọc, chỉ đọc, dùng USER_VIEW**. Phần này chưa hoàn tất toàn bộ UC-141 vì bước mở account/effective permissions liên quan UC-005/011 còn ở task sau. Xem [kế hoạch đầu tiên](FIRST_IMPLEMENTATION_PLAN.md), [backlog](IMPLEMENTATION_BACKLOG.md), [ownership](MODULE_OWNERSHIP.md).

## Git/source preflight

- Branch: `feature/BE1-database-integration`; HEAD `5299b5e9e8dd917abef327ab9f9bd5494064ccf9`.
- Trực tiếp `git ls-remote --heads origin develop`: cùng SHA ở thời điểm preflight; không có commit mới trên origin/develop cần đối chiếu. Không fetch/merge/chuyển nhánh. Không suy đoán thay đổi chưa push hoặc đang nằm trên branch riêng DEV2.
- Main checkout không có tracked/index diff. Task 15 vẫn untracked và giữ nguyên.
- Worktrees được giữ nguyên: main; `obj/task11-dev2-rehearsal` tại `3688ba9`; `obj/task12-pr7-resolution` tại `d6ef057`. Không chỉnh sửa staging hoặc working tree của rehearsal.
- Bảo toàn 538 file tracked/untracked có từ trước audit bằng so sánh SHA256 trước/sau, bao gồm WAV, src/main.cs và hồ sơ Task 15. Không đọc hoặc lưu credentials vào hồ sơ.

## Source inventory

Đếm trực tiếp các HTTP attributes trong 10 controller: **117 action declarations**, không phải 117 Use Cases hoặc bằng chứng test pass.

| Controller | HTTP actions | Service / DTO hiện tại | Nhận định |
|---|---:|---|---|
| AuthController | 4 | AuthenticationLoginService, AuthenticationCredentialWorkflow; AuthenticationDtos.cs | login, session, logout, change-password |
| CsrfController | 1 | Antiforgery infrastructure | Hỗ trợ auth; không tự tạo UC mới |
| HealthController | 1 | HotelOpsDbContext.CanConnectAsync | Readiness; không phải business UC |
| WeatherForecastController | 1 | Template WeatherForecast | Scaffold mẫu; không tính là tính năng SRS |
| AssetsController | 18 | AssetService partials; DTOs/Assets | DEV2, có API; chưa chứng nhận full SRS/security |
| IncidentsController | 5 | IncidentService; DTOs/Incidents | DEV2 |
| MaintenanceWorksController | 13 | MaintenanceWorkService partials; DTOs/Maintenance | DEV2 |
| InventoryController | 49 | InventoryService partials; InventoryContracts.cs | DEV2; Adjustment/Reversal đang chờ contract riêng |
| MaterialRequestsController | 15 | MaterialRequestService partials; MaterialRequestContracts.cs | DEV2 |
| ReplenishmentsController | 10 | ReplenishmentService partials; ReplenishmentContracts.cs | DEV2 |

Không tìm thấy controller/service/DTO riêng cho user administration, role/assignment administration, hotel structure, rooms, cleaning, inspection, operational tasks, work-time, internal requests, notifications/dashboard/report APIs. Có entity hoặc permission code không đồng nghĩa chức năng đã triển khai.

## Nguồn và giới hạn thẩm quyền

1. Yêu cầu Task 16, AGENTS.md và quyết định Task 15 (canonical branches, empty personnel, PRIORITY theo GBR-085).
2. `docs/requirements/SRS_CHANGE_NOTES.md`: Reception và DH/OM/matrix còn pending; DH quản lý trong branch, không phải một department.
3. `docs/requirements/HotelOps_SRS_DRAFT.md` và `srs_sections/02_use_case_specifications.md`: 144 UC; `03_functional_requirements.md`: field/screen requirements; `05_requirement_appendix.md`: business rules/parameters.
4. `srs_sections/01_overall_requirements.md` §4.3 diagrams, §4.4 draft Permission Matrix; đã xem các ảnh OM, HK Supervisor, HK Staff, Maintenance Lead/Staff, Storekeeper, DH, Owner và User. Phần Administrator §4.3.1 không có ảnh nhúng; không tự bổ sung diagram. Không có Reception diagram đã chốt.
5. Source `Program.cs`, Controllers, Services, DTOs, Entities, HotelOpsDbContext; release `03_HotelOps_RBAC_Catalog_Seed.sql`; AuthorizationHandoff guide/outstanding work; hồ sơ CanonicalDatabase.

Các link ảnh tương đối trong bản chia section dùng `../assets/...`, nhưng ảnh thật ở `docs/requirements/assets/media/`. Audit xem file ảnh thật; không sửa tài liệu nguồn. Mũi tên extend trong diagram không tự cấp write permission.

## Verification

- `dotnet build HotelOps.slnx`: **PASS, 0 warnings, 0 errors**, đã chạy trong Task 16.
- `git diff --check`: **PASS**. Kiểm tra riêng năm file mới: không trailing whitespace; 144 UC độc nhất, đủ UC-001–144; source inventory 10 controller/117 HTTP declarations và catalog 62 permission codes đã đối chiếu.
- SHA256 trước/sau: **538/538 file có sẵn không đổi**, không thiếu file hoặc phát sinh file ngoài thư mục audit. Main checkout tracked/index diff rỗng.
- Không chạy SQL integration tests, không kết nối hoặc thay đổi HotelOpsDB trong Task 16. Không kiểm thử runtime endpoint trong audit này.
- Automated tests: **Not Run** vì chỉ thêm tài liệu; không tuyên bố các business flows hoặc API permissions đã test pass.
- Chỉ tạo năm file Markdown trong thư mục này. Không commit/push/merge; không chỉnh sửa hồ sơ Task 15.
