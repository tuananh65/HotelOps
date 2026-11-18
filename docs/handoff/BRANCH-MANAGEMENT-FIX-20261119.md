# Bàn giao sửa quản lý chi nhánh — 19/11/2026

## Kết quả

Đã sửa giao diện danh sách, tìm kiếm, lọc trạng thái, phân trang, tạo mới, chỉnh sửa và kiểm tra/xác nhận ngừng hoạt động chi nhánh. Popup chỉnh sửa chứa cả form và phần xác nhận; thông báo thành công màu xanh. Có trạng thái tải, báo lỗi, tải lại; chống gửi trùng, xử lý dữ liệu đã thay đổi và khóa gửi lại khi chưa biết kết quả ghi. Khóa này được giữ khi tải lại chi tiết trong cùng phiên trang.

Đã khắc phục cấu trúc database thiếu khiến các service quản lý trả 503. Không thay đổi quyền, entity sinh tự động hoặc C# ứng dụng. Không commit/push hoặc đổi nhánh.

Production local đang phục vụ bản build mới tại https://127.0.0.1:5080/branches. HTTP cổng 5080 chuyển sang HTTPS.

## Nguyên nhân

- `dbo.branches` thiếu `row_version`; toàn bộ chức năng quản lý bị chặn bởi kiểm tra schema.
- Phần xác nhận chỉnh sửa nằm ngoài popup, bị lớp phủ che.
- Thiếu bảng vật tư bổ sung và cấu trúc kiểm soát phụ thuộc chi nhánh; kiểm tra ngừng hoạt động lỗi SQL 208.
- Giao diện chưa thể hiện rõ lỗi, trạng thái xử lý, dữ liệu cũ và kết quả ghi chưa xác định.

## Các tệp thay đổi trong nhiệm vụ này

Thư mục `frontend/src/branch-maintenance/`:

- `BranchManagementPage.tsx`: danh sách, bộ lọc, phân trang, tải và lỗi.
- `CreateBranchRoute.tsx`: form tạo tiếng Việt, xác nhận, kiểm tra và kết quả.
- `BranchMaintenancePanel.tsx`: chỉnh sửa và xác nhận trong cùng popup, quản lý phiên bản/kết quả.
- `BranchMaintenanceRoute.tsx`: bỏ phần ngừng hoạt động render tách biệt.
- `BranchDeactivationReview.tsx`: popup kiểm tra phụ thuộc, xác nhận và cập nhật trạng thái.
- `branch-ui.tsx` (mới): nhãn, thông tin tóm tắt và thông báo lỗi dùng chung trong chức năng.
- `branch-management.css` (mới): bố cục và responsive của chức năng.

Kiểm thử:

- `frontend/tests/branch-management-workspace.test.mjs` (mới).
- `frontend/tests/branch-maintenance-api.test.mjs`: sửa fixture ETag theo SQL rowversion 8 byte.
- `frontend/tests/create-branch-api.test.mjs`: cập nhật fixture ETag và kiểm tra múi giờ theo contract.

Tài liệu và bằng chứng: báo cáo này, các đề xuất, runner sao lưu/cài schema, probe chỉ đọc, log và ảnh trong `artifacts/branch-management-fix/`. Thư mục artifacts là dữ liệu kiểm chứng local. Các thay đổi UI P1/V2 khác vốn có trong working tree được giữ nguyên; không tính là thay đổi của nhiệm vụ này.

## Database đã cập nhật theo hai lần phê duyệt

1. `scripts/database/development/BranchVersionV1.sql`.
2. Gói 5 script theo thứ tự:
   - `database/patches/20261021_replenishment_contract.sql`
   - `scripts/verification/CanonicalDatabase/PreventiveSchedule.sql`
   - `scripts/verification/CanonicalDatabase/PreventiveOccurrence.sql`
   - `scripts/database/development/LocationGuardsV1.sql`
   - `scripts/database/development/BranchDeactivationV1.sql`

Sao lưu COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY đều thành công. Gói thứ hai đã chạy thử toàn bộ trong transaction rồi rollback trước khi commit. Hai lần kiểm tra xác nhận dữ liệu nghiệp vụ của các chi nhánh không đổi. Không chủ động đóng/ngừng hoạt động bản ghi nghiệp vụ để kiểm thử.

Hai bản sao lưu trên máy SQL Server:

```text
C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\HotelOpsDB_before_branch_version_20261119_032735.bak
C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\HotelOpsDB_before_branch_dependencies_20261119_034628.bak
```

Các guard định danh database trong runner được giới hạn vào HotelOpsDB và định danh thực tế, theo đề xuất đã duyệt; không dùng định danh máy của lập trình viên khác. Trigger mới kiểm tra chi nhánh/vị trí khi ghi dữ liệu liên quan. Không bật tác vụ tự sinh bảo trì.

## Kiểm chứng

| Hạng mục | Kết quả | Bằng chứng trong artifacts/branch-management-fix |
|---|---|---|
| Frontend typecheck và production build | Passed | `build.txt` |
| API client/contract | Passed: 58/58 | `api-tests.txt` |
| UI trình duyệt với API fixture | Passed: 15/15 | `browser-tests-final.txt` |
| Bổ sung kiểm tra khóa gửi lại sau reload | Passed: 1/1, thuộc ca đã có | `uncertain-reload-test.txt` |
| Responsive 390px và 1280px | Passed, đã xem ảnh | `screenshots/edit-390.png`, `screenshots/edit-1280.png` |
| Prettier các tệp liên quan | Passed | `format-check.txt` |
| SQL/service thật, chỉ đọc | Passed | `database-probe-final.txt` |
| Production smoke | Passed: 13/13 | `production-smoke-final.txt` |
| .NET solution build/test | Not Run trong nhiệm vụ này; không sửa C# ứng dụng | Probe C# riêng đã biên dịch và chạy thành công |

SQL probe dùng dữ liệu quyền thật, nhưng mô phỏng bước xác thực phiên để gọi trực tiếp service. Options, chi tiết và phụ thuộc của hai chi nhánh đều trả 200; ETag đúng định dạng SQL rowversion. `lan.dang` bị từ chối quản lý với 403. Đây không phải kiểm thử đăng nhập HTTP rồi ghi nghiệp vụ trên database thật.

Production smoke kiểm tra HTTP chuyển HTTPS, form đăng nhập, asset build mới, SQL health, truy cập ẩn danh bị từ chối, CSRF cookie Secure/HttpOnly và không có lỗi JavaScript ở các bước smoke. Development vẫn hoạt động. Asset xác minh: `index-ChMf8-h4.js`, `index-BpvjPiIu.css`.

## Giới hạn và điểm cần theo dõi

- **Ngừng hoạt động vẫn bị chặn đúng cơ chế bảo vệ ở hai chi nhánh hiện có**: chi nhánh 1 có 15 phòng hoạt động và 4 kho có tồn; chi nhánh 2 có 4 phòng hoạt động. Đồng thời bộ đọc phụ thuộc trả `DEPENDENCY_PROVENANCE_UNRESOLVED` với một số dữ liệu cũ.
- Những điểm chưa xác định gồm trạng thái `COMPLETED`, `RESOLVED`, `DRAFT` ở một số luồng; dữ liệu vệ sinh/kiểm tra thiếu bằng chứng; `COMPLETED_WITHOUT_VERIFICATION_CAPABILITY` và `PROVENANCE_CAPABILITY_REQUIRED`. Chi tiết số lượng nằm trong `database-probe-final.txt`. Không tự xem những bản ghi này là đã đóng hoặc chỉnh sửa dữ liệu để mở nút xác nhận. Nếu cần ngừng một chi nhánh hiện có, phải xử lý phòng/kho/công việc và làm rõ dữ liệu nguồn trước.
- Luồng tạo/sửa/ngừng hoạt động thành công được kiểm chứng trên trình duyệt với API fixture. Chưa thực hiện HTTP ghi thật bằng tài khoản quản trị trên database đang sử dụng, chưa chứng minh thao tác ngừng một chi nhánh thật đủ điều kiện từ đầu đến cuối.
- Không chạy toàn bộ `npm test`; các bộ browser cũ ngoài bộ kiểm thử nêu trên chưa được chạy lại.
- Build còn cảnh báo React Router `use client` bị bỏ qua và bundle lớn hơn 500 kB. Build vẫn thành công; tối ưu bundle ngoài phạm vi lần sửa này.

## Cách kiểm tra thủ công tiếp

1. Đăng nhập tài khoản quản trị có quyền quản lý dữ liệu chung, mở **Quản lý chi nhánh**.
2. Kiểm tra danh sách, tìm tên/mã, lọc trạng thái và phân trang.
3. Tạo chi nhánh theo dữ liệu thử được phép sử dụng; rà soát trong popup rồi xác nhận.
4. Mở chi tiết, sửa thông tin và nhập lý do; xác nhận ngay trong popup. Kiểm tra thông tin mới và thông báo xanh.
5. Mở **Ngừng hoạt động**, kiểm tra các phụ thuộc. Chi nhánh còn phòng, tồn kho, công việc hoặc dữ liệu chưa rõ phải bị chặn và hiện lý do. Chỉ xác nhận khi máy chủ xác định đủ điều kiện.
