# Bàn giao UX và nghiệp vụ Project 2 — 23/11/2026

## Kết quả triển khai

- **Dữ liệu:** đã backup và áp dụng phương án người dùng duyệt lên `HotelOpsCompactDB`: xóa 28 công việc DEMO, 10 chứng từ DEMO cùng dữ liệu phụ thuộc; 96/96 phòng Sẵn sàng. Giữ 57 tài khoản, 131 grants, 56 role assignments và 6 tài sản. Checksum grants/assignments khớp clone trước reset. Không có trigger bị tắt hoặc tồn âm sau ghi. Audit được giữ và bổ sung sự kiện khởi tạo; không tạo inspection giả. Block vẫn ưu tiên và checkout trở lại Cần dọn.
- **UX dùng chung:** logo đăng nhập trong suốt; focus textbox không che chữ, vẫn có dấu focus bàn phím; thông báo validation tiếng Việt; phân trang dùng chung hiển thị khoảng/tổng bản ghi; giữ kích thước compact và room grid 15 phòng/trang. Chi tiết công việc mở drawer. Mọi role có nút hồ sơ cá nhân ở tên tài khoản, sửa họ tên/email/điện thoại của chính mình với version/audit.
- **Admin:** mã nhân viên tự sinh ở backend, mã các danh mục/tài sản/checklist/kế hoạch tự sinh khi tạo; số phòng tự chọn theo tầng và tránh trùng trong chi nhánh. Mật khẩu tối thiểu 8 ký tự với tiêu chí đánh dấu trực tiếp; giữ yêu cầu hoa/thường/số/ký tự đặc biệt. Tạo tài khoản gán chi nhánh và tùy chọn bộ phận trong cùng transaction; kiểm tra trùng username/email. Bổ sung điện thoại, STT, số bản ghi và sửa lọc bộ phận. Không cấm hai người có cùng họ tên.
- **Cấu hình:** danh mục quyền được bật/tắt theo quyền quản trị; backend từ chối tắt quyền đang gán cho tài khoản active. Bổ sung danh mục dùng chung, STT, mô tả của thực thể có trường tương ứng và kiểm tra trùng tên theo scope. Không sửa grants hiện có. Các mức priority/severity cố định tiếp tục chỉ đọc.
- **Cấu trúc khách sạn:** cây chi nhánh → tầng/khu vực → phòng. Có địa chỉ, điện thoại, người đại diện và form chỉnh sửa. Database hiện có địa chỉ/điện thoại; người đại diện còn trống và có thể nhập. Dữ liệu đại diện dùng metadata `reference_values.settings_json` hiện hữu, không thêm cột/generated entity. Metadata này được loại khỏi danh mục và workspace lookup chung.
- **Reception:** gom Cần dọn/Chờ kiểm tra/Cần làm lại thành Cần dọn dẹp; tách Đang dọn dẹp, Sẵn sàng và Có khách. Phòng bị chặn vẫn được hiển thị đúng để tránh hiểu nhầm có thể giao khách. Bỏ tạo sự cố ở trang phòng và công việc của Reception; không sửa grants.
- **Department Head:** chọn nhiều nhân viên, áp dụng các ngày trong tháng, chọn thứ/ngày, xem sức chứa và xác nhận mốc lễ. Ghi batch nguyên tử, kiểm tra branch/candidate/version/capacity và chống ghi lặp. Có cấu hình ca theo `SHIFT_MANAGE`, khóa các thay đổi làm sai lịch sử. Mốc Tết/Giỗ Tổ được đối chiếu cho 2026–2027; không tự áp dụng lịch nghỉ bù khối hành chính cho khách sạn. [Nguồn mốc Tết 2026](https://baochinhphu.vn/bo-noi-vu-thong-bao-lich-nghi-tet-am-lich-va-nghi-le-quoc-khanh-nam-2026-102251017095507785.htm), [nguồn Giỗ Tổ 2026](https://xaydungchinhsach.chinhphu.vn/lich-nghi-le-gio-to-hung-vuong-30-4-1-5-quoc-khanh-2-9-nam-2026-119260222115822151.htm).
- **OM:** cấu hình theo quyền thực tế, drawer công việc, tài sản nhóm theo phòng, nhãn tình trạng tiếng Việt và một thanh Tốt/Hỏng/Khác. Thống kê công việc mặc định ngày, chọn tuần/tháng theo timezone chi nhánh và thời điểm tạo công việc. Các số liệu hiện trạng khác không bị trình bày như số phát sinh trong kỳ.

## Kiểm chứng

- Backend build: **Passed, 0 warning, 0 error** sau sửa cuối; log `artifacts/ux-backend-build.log`.
- .NET tests: **43 Passed, 3 Skipped, 0 Failed**. Ba test SQL/email/worker opt-in chưa chạy trong lệnh mặc định; không coi chúng đã Passed.
- API/SQL fixture bản build cuối: **30/30 Passed**, database `HotelOpsCompactApiTest68060844b9da`, receipt trong `artifacts/compact-v2/backend/ux-HotelOpsCompactApiTest68060844b9da/ux-verification.json`. Bao gồm concurrent account codes, password8, branch assignment, hồ sơ OWN/stale, contact/invalid phone rollback, permission guard, assets scope, thống kê kỳ, phân ca cả tháng và rollback sau lỗi insert event.
- Reset lifecycle clone: **Passed** cho baseline Ready, block ưu tiên, occupancy độc lập và checkout phải dọn; rollback toàn bộ test. Primary chỉ nhận thao tác quản trị đã duyệt, không nhận dữ liệu test.
- Workday pure checks: **17/17 Passed**, gồm ngày lễ 2026–2027.
- Browser tích hợp: **5/5 Passed**, gồm cây/phòng/liên hệ, quyền đang dùng, điện thoại sai, hồ sơ/focus/responsive và Reception grouping/no incident. Lỗi pattern điện thoại phát hiện trong review đã sửa cả FE và BE.
- Compact logic tests: **9/9 Passed**. Các browser suite account, workday, operations, navbar, structure, reception và pagination đã có lượt chạy Passed; xem báo cáo review và receipt của từng suite.
- Frontend build: **Passed**, TypeScript và Vite. Chuỗi UX trên Vite dev server còn timeout tải shell. Trên bản build preview, navbar 48/48, structure, Reception và Workday Passed; bước pagination có một lần fail kiểm tra overflow. Chạy lại riêng pagination trên Vite đạt đầy đủ desktop/mobile và bốn dashboard. Chưa có lượt chuỗi cuối hoàn toàn xanh; không gộp các lượt riêng thành Passed cho cả chuỗi.
- `git diff --check`: Passed. Có thông báo Git chuẩn hóa LF/CRLF; không phải lỗi biên dịch.

## Lịch sử lỗi và giới hạn

Các lượt build ban đầu lỗi khóa DLL do API còn chạy; đã dừng đúng tiến trình, build lại và khởi động lại sau khi build kết thúc. Một lượt API dùng binary cũ chưa có validation điện thoại thất bại; receipt cuối nêu trên đã chạy lại binary mới và đạt. Các lượt browser trong lúc tích hợp gặp reload/timeout; giữ log thất bại và receipt chạy lại, không đổi assertion để che lỗi.

`npm test` toàn repository chưa xanh: suite giao diện cũ `activate-user-router-integration.test.mjs` có 12 timeout, đã dừng giữa chừng. Suite này dùng `src/auth`/`/api/auth`, khác Compact `/api/v2`; không sửa Project 1 để ép test qua. Chưa xác minh toàn bộ kiểm thử cũ.

Lịch âm ngoài 2026–2027 chưa có mốc đã xác minh, UI hiển thị rõ giới hạn. Chưa có cấu hình lịch nghỉ bù riêng theo chi nhánh trong schema; người phân ca chọn/bỏ ngày trong form. Thông tin đại diện chưa được cung cấp được để trống, không điền giả. Việc kiểm thử không chứng minh mọi trường hợp tải lớn hoặc mọi race condition.

## Tệp tạo/sửa trong đợt này

Frontend trong `frontend/src/compact`: `account-filters.tsx`, `account-policy.tsx`, `admin.tsx`, `api.ts`, `app.tsx`, `auth.tsx`, `personal-profile.tsx`, `form-validation.ts`, `compact.css`, `ui.tsx`, `catalog-specs.ts`, `catalog.tsx`, `hotel-workspace.tsx`, `branch-tree.tsx`, `structure-tree.tsx`, `rooms.tsx`, `work.tsx`, `templates.tsx`, `maintenance-plans.tsx`, `housekeeping-checklists.tsx`, `assets.tsx`, `asset-presentation.ts`, `asset-condition-bar.tsx`, `insights.tsx`, `operations-metrics.tsx`, `operations-settings.tsx`, `operations.css`, `workday.tsx`, `workday-assignment.tsx`, `workday-scheduling.ts`, `workday-settings.tsx`, `workday.css`. Asset: `frontend/public/duc-vuong-logo-transparent.png`.

Backend trong `src/HotelOps.Api/Compact`: Contracts `Common.cs`, `AccountRequests.cs`, `UserDirectory.cs`, `WorkdayRequests.cs`; Controllers `AuthController.cs`, `UsersController.cs`, `OperationsControllers.cs`, `WorkdayController.cs`, `WorkspaceController.cs`; Services `UserService.cs`, `AuthService.cs`, `AccountManagementService.cs`, `CatalogService.cs`, `TemplateService.cs`, `AssetService.cs`, `InsightService.cs`, `InsightService.Operations.cs`, `WorkdayService.cs`, `WorkdayService.Scheduling.cs`, `WorkdayCalendar.cs`.

Tests/scripts: `AccountRefinementTests.cs`, `OperationsWindowTests.cs`; browser `compact-account-refinements.mjs`, `compact-operations-refinements.mjs`, `compact-operations-refinements.test.mjs`, `compact-workday-scheduling.mjs`, `compact-ux-role-integration.mjs` và các fixture structure/operations/reception/dashboard/workday-filter; `Test-Backend.ps1`, `test-ux-refinements.ps1`, `WorkdaySchedulingChecks.cs`, `test-workday-scheduling-pure.ps1`, `inspect-ux-reset.ps1`, `Reset-DemoReady.ps1`, `reset-demo-ready.sql`, `Verify-DemoReady.ps1`, `Test-DemoReadyLifecycle.ps1`, `04_Level2.sql`. Tài liệu reset: `docs/database/compact-v2/ADMINISTRATIVE_RESET.md`.

Các thay đổi HKS từ nhiệm vụ trước được giữ nguyên. Không commit/push/deploy. Không có câu hỏi bắt buộc còn chờ người dùng.
