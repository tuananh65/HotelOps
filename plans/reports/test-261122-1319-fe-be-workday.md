# Kiểm chứng HotelOps Compact 2.1 và FE Workday

Ngày 22/11/2026. Nhánh giữ nguyên `integration/dev1-dev2-dev3-unblock-20261114`. Thực hiện single-agent, có sử dụng AgentKit 3.2.0; không commit, push hay deploy.

## Kết quả triển khai

Backend Compact và React FE đã nối với dữ liệu thật. Workday gồm lịch Thứ Hai–Chủ nhật, phân người theo nhóm/ca, sức chứa, ứng viên hợp lệ, lịch sử, hủy ca chưa bắt đầu và Start/End ca cá nhân. Giờ và số người tuân theo câu trả lời trực tiếp của người dùng; hai ca kỹ thuật/kho được biểu diễn thành 08:00–11:30 và 13:00–17:00. Start chỉ kiểm tra ngày của chi nhánh, không ép đúng giờ. End ca độc lập với timer công việc.

`HotelOpsCompactDB` hiện có schema **2.1.0, 40 bảng, 160 FK, 82 CHECK, 27 trigger, 6 view và 30 định nghĩa ca**. Không có FK/CHECK bị tắt hoặc không trusted. Không thực hiện thao tác sửa database legacy. Bản backup COPY_ONLY/CHECKSUM đã được RESTORE VERIFYONLY trước nâng cấp và trước xóa bảng sơ đồ rỗng.

Sau nâng cấp, `dbo.sysdiagrams` xuất hiện thêm, không có dòng dữ liệu, khiến tổng số bảng lên 41 và schema gate chặn startup. Đã thông báo và nhận xác nhận riêng của người dùng lúc 13:19, backup lại rồi xóa đúng bảng này. Không nới schema gate để bỏ qua giới hạn. Bằng chứng cuối: `artifacts/workday/primary-verification.json`; health API trả `ok / 2.1.0 / 40`.

FE dùng bearer session, chi nhánh và quyền thật; không có nút giả lập vai trò. Các màn hình vận hành gồm phòng, công việc, kiểm tra, lịch tuần, chứng từ, tài sản, báo cáo, thông báo và nhân sự. Tải module theo màn hình; JS khởi đầu khoảng **246.95 KB, gzip 77.64 KB**. Khi tải lại cùng nguồn, giữ nội dung và tạm khóa tương tác; khi đổi nguồn/chi nhánh, không hiển thị dữ liệu cũ.

## Kiểm chứng thực tế

| Hạng mục | Kết quả | Bằng chứng |
|---|---|---|
| `dotnet build HotelOps.slnx` (serial, không shared compiler) | Passed, 0 warning / 0 error | Log thực thi trong phiên; DLL được dùng chạy regression |
| `dotnet test HotelOps.slnx --no-build` | 26 Passed, 0 Failed, 1 optional worker Skipped | `artifacts/workday/dotnet-tests/workday-final.trx` |
| Worker integration, chạy riêng có SQL Server | 1 Passed; recurrence, replay, low-stock deduplication | `artifacts/compact-v2/backend/worker-verification.json` |
| Hồi quy backend mở rộng trên bản build checklist mới | 127 Passed, 0 Failed | `artifacts/compact-v2/backend/verification.json`, DB `HotelOpsCompactApiTest1b687091f30c` |
| Workday và các guard tích hợp | 14 nhóm Passed | `artifacts/workday/api-results.json`, DB `HotelOpsCompactWorkdayTestFinal20261122` |
| FE logic | 4 Passed | `npm run test:compact` |
| FE browser / DB thật | 8 nhóm Passed, không có pageerror | `artifacts/workday/browser/results.json`, DB `HotelOpsCompactWorkdayTestBrowser20261122D` |
| Render checker AgentKit sau đăng nhập | 30 tổ hợp, 0 error / 0 warning | `artifacts/workday/visual/results.json`, ảnh cùng thư mục |
| Hover, tìm kiếm rỗng, lỗi mạng/thử lại, Escape | 4 trạng thái được kiểm chứng | `artifacts/workday/states/results.json` và ảnh |
| FE build | Passed | `npm run build` |
| Format phần Compact và test mới | Passed | Prettier check trong phiên |

Bộ browser đi qua phân ca → Start/End; Reception check-in/check-out; giám sát tạo/giao việc; HK nhận/bắt đầu/gửi hoàn thành; inspector chấm checklist/kết luận để phòng READY; thủ kho tạo phiếu/thêm dòng/ghi sổ; 4 vai trò tại 375/768/1440px và bàn phím. Các nhóm đạt trước đó trên cùng fixture D được giữ lại khi tiếp tục sau sửa lỗi; đây **không phải tuyên bố một lượt chạy liền mạch không lỗi từ đầu**. Các lần thất bại đã được điều tra, không tính là pass. Lần kiểm tra trạng thái và render cuối dùng database chính; các nghiệp vụ ghi dữ liệu thử nghiệm chạy trên bản sao riêng.

Workday API kiểm tra giới hạn sức chứa bằng hai yêu cầu đồng thời, overlap, ca kế tiếp liền giờ, khác chi nhánh, OWN, ngày tương lai, stale version, một ca hoạt động, lý do hủy, lịch sử riêng tư, timer công việc không bị End ca thay đổi, và ngăn vô hiệu hóa nhân viên/thu hồi quyền còn lịch.

## Lỗi tìm thấy và đã sửa

1. Inspection FE cho tạo phiếu thiếu checklist nhưng SQL không cho kết luận PASS. API nay trả 400 `INSPECTION_CHECKLIST_REQUIRED` ngay khi tạo; FE bắt chọn mẫu đúng loại và gửi phiên bản từng mục khi chấm. Checklist công bố được lấy qua workspace lookup có kiểm tra quyền.
2. Người dùng có timer đang mở không biết cần tạm dừng công việc nào. FE thêm nút mở việc đang chạy; kiểm thử đã tạm dừng việc cũ trước khi bắt đầu việc dọn mới.
3. Đăng xuất giữ hash màn hình của vai trò trước. Đã xóa route khi logout; màn mặc định theo quyền thực tế.
4. Danh sách công việc và bàn giao thiếu phạm vi người nhận. Bổ sung lọc của tôi, đọc/nhận/từ chối bàn giao theo quyền OWN và kiểm tra người đang giữ việc khi lấy ứng viên.
5. Nút ghi sổ vẫn cho mở hộp xác nhận trong lúc lưu dòng vật tư; mutation hoàn tất đóng hộp này. Đã khóa thao tác trong lúc lưu và tách form thêm dòng với form hành động.
6. Refetch làm trắng nội dung hộp thoại. Giữ dữ liệu khi tải lại cùng URL, khóa tương tác bằng `inert`, bỏ kết quả request bị hủy; đổi URL không giữ dữ liệu cũ.
7. Báo cáo mặc định không phù hợp quyền của Department Head; đã chọn loại báo cáo từ quyền thực tế. Tìm phòng không có kết quả nay có thông báo và nút xóa bộ lọc.
8. Render checker phát hiện cảnh báo nhãn ẩn/skip link. Đã sửa nhãn và liên kết bàn phím; lần cuối 0 cảnh báo.

Lỗi công cụ/test gặp trong quá trình: build từng bị DLL đang chạy khóa file (đã dừng đúng API thuộc phiên rồi build lại sạch); test browser từng chạy trước readiness, đếm DOM khi đang refetch, chọn nhầm option “Đạt”, và bị Vite reload khi formatter sửa source. Helper startup chờ health; driver chờ HTTP/state và chọn badge cụ thể. Không sửa source của skill AgentKit.

## File tạo/sửa trong phạm vi lượt này

- Backend mới: `Compact/Entities/Workday.cs`, `Compact/Data/HotelOpsCompactDbContext.Workday.cs`, `Compact/Services/WorkdayService.cs`, `Compact/Controllers/WorkdayController.cs`, `Compact/Controllers/WorkspaceController.cs` (các đường dẫn tương đối dưới `src/HotelOps.Api`).
- Backend cập nhật: `Compact/CompactHost.cs`, `Compact/Data/HotelOpsCompactDbContext.Runtime.cs`, `Compact/Infrastructure/SchemaGate.cs`, `Compact/Security/PermissionCatalog.cs`, `Compact/Services/AuthService.cs`, `UserService.cs`, `WorkService.cs` và route work trong `Compact/Controllers/OperationsControllers.cs`.
- SQL/công cụ: `scripts/database/compact-v2/06_Workday.sql`, `Backup-Workday.ps1`, `Upgrade-Workday.ps1`, `Remove-EmptyDiagram.ps1`, `Start-WorkdayApi.ps1`, `test-workday-api.mjs`, cập nhật `Test-Backend.ps1`.
- FE: `frontend/src/compact/{api,auth,ui,app,workday,rooms,workflow,work,documents,assets,admin,insights}.tsx/ts`, `compact.css`, `frontend/src/main.tsx`, `frontend/index.html`, `frontend/public/duc-vuong-logo.png`, `frontend/package.json`.
- Kiểm thử: `frontend/tests/compact-workflow.test.mjs`, `compact-browser.mjs`, `compact-visual.mjs`, `compact-states.mjs`; `tests/HotelOps.Compact.Tests/WorkdayTests.cs` và cập nhật số entity trong `CompactV2Tests.cs`.
- Tài liệu: `docs/requirements/project2-v1-extracted.txt`, `docs/database/compact-v2/WORKDAY-INTEGRATION.md`, ghi chú phiên bản tại `README.md`/`BACKEND.md`, plan, báo cáo này và journal local.

Repository đã có nhiều file Compact/untracked và thay đổi `.gitignore`, solution, csproj, `Program.cs` trước lượt này. Không nhận toàn bộ `git diff` là thay đổi mới của lượt Workday, không reset hoặc ghi đè thay đổi của người khác. Không thay cấu trúc single-project hoặc tạo EF migration.

## Review UI bằng AgentKit

Đã dùng `ak-agentkit`, `ak-cook`, `ak-docx`, `ak-frontend-design`, `ak-web-testing`, `ak-debug`, `ak-code-review` và `ak-journal`. Review code thực hiện inline theo yêu cầu single-agent; không có reviewer độc lập. Bộ kiểm tra pixels sử dụng nguyên hàm `pageChecks` của skill, không bỏ bớt cảnh báo.

| Tiêu chí | Điểm / 2 | Bằng chứng / giới hạn |
|---|---:|---|
| Đúng brief | 2 | Lịch tuần và room grid, sắc teal, cảm giác gọn/rõ/thực dụng |
| Phân cấp | 2 | Tiêu đề, bộ lọc, hành động chính rõ trong ảnh rooms/workday |
| Khoảng cách | 2 | Thẻ phòng và form có nhóm/gap thống nhất |
| Căn chỉnh | 1 | Lưới rõ; một số bảng số liệu vẫn căn trái |
| Typography | 2 | Một họ system font, phân cấp bằng cỡ và độ đậm |
| Màu | 2 | Màu trạng thái kèm nhãn chữ, không phụ thuộc riêng màu |
| Shape/depth | 2 | Border/radius nhất quán, overlay có backdrop |
| Trạng thái | 2 | Ảnh hover/empty/error/overlay; retry và Escape chạy thật |
| Responsive | 1 | Trang không tràn; lịch tuần/bảng rộng còn cuộn trong container |
| Không trang trí thừa | 2 | Không hero/gradient quảng cáo trong workspace |
| Nội dung | 1 | Dữ liệu mẫu thật từ DB; một số event/status ít gặp còn mã gốc |
| Chi tiết | 1 | Có logo/favicon/icon/focus; date/select vẫn dùng native control |

Tổng **20/24**. Các điểm chưa đạt 2 giữ nguyên trong báo cáo. Đã cải thiện ba điểm yếu thực tế: trạng thái refetch, nhãn trạng thái phòng và empty/retry; không nâng điểm bằng cách bỏ qua hạn chế cuộn bảng trên điện thoại.

## Chạy lại và giới hạn

Preview local: **http://127.0.0.1:5178** → API **http://127.0.0.1:5768**, database `HotelOpsCompactDB`. API helper tắt worker nền để tránh thay đổi tự động trong lúc xem thử; worker được kiểm thử riêng. API PID được ghi trong `artifacts/workday/api-5768-process.json`. Server kiểm thử 5769 đã dừng. Giữ nguyên server/IDE do người khác chạy.

Các lệnh FE mới: `npm run test:compact`, `test:compact:e2e`, `test:compact:visual`, `test:compact:states`. E2E ghi dữ liệu: chỉ chạy khi proxy trỏ tới bản sao test sạch, không chạy trên database chính. Script API tương tự cần `WORKDAY_API` trỏ fixture riêng. Tài khoản/mật khẩu nằm trong file local ignored `artifacts/compact-v2/seed/HotelOpsCompactDB/credentials.json`, không chép bí mật vào báo cáo hay source.

**Chưa xác minh đầy đủ:** không tuyên bố toàn bộ frontend legacy đạt. `npm test` legacy đã gặp timeout ở các ca route ID không hợp lệ trong lượt baseline và dừng; suite này còn gắn API/UI cũ, được giữ lại cùng source legacy. Các màn quản trị/catalog nâng cao, mọi biến thể chứng từ và bàn giao chưa có end-to-end browser coverage đầy đủ dù hồi quy backend đã qua. Thiết lập ca hiện seed cho hai chi nhánh DEMO; không có màn sửa định nghĩa ca cho chi nhánh mới. Những quyết định OPEN trong SRS, báo cáo lương và yêu cầu bàn giao ca mới chưa được suy diễn thành nghiệp vụ.

Không có lỗi build hay lỗi trong các bộ Compact đã hoàn tất. Đây là bản tích hợp local, chưa phải xác nhận sẵn sàng production. AgentWiki publish skipped.

`git diff --check` không có lỗi whitespace; Git còn thông báo chuẩn hóa LF sang CRLF ở `frontend/package.json` và `frontend/src/main.tsx`. Các database fixture được giữ để đối chiếu, không có server kiểm thử còn chạy. Journal được tạo bằng CLI AgentKit tại `plans/journals/2026-11-22-hotelops-compact-workday-fe-be-integration.md`.
