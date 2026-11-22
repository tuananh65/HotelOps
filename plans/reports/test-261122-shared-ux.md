# Chuẩn hóa UX dùng chung

## Kết quả

Đã sửa frontend theo các vấn đề lặp lại người dùng chỉ ra: khoảng trống dưới navbar, nút thao tác tách khỏi bộ lọc, hàng tiêu đề trống, nút Làm mới dư thừa. Có component `PageToolbar`, token khoảng cách đầu trang 12px, responsive xuống hàng cho bộ lọc/nút, phân trang hiển thị cả tổng số trang.

Các trang công việc, chứng từ, báo cáo, tồn kho, checklist, tổng quan cá nhân dùng toolbar chung; nút thêm danh mục nằm cùng tìm kiếm. Giữ bố cục chuyên biệt đã chấp nhận của tài khoản, cấu hình, danh sách phòng, lịch và thẻ phòng 15/trang.

Sửa thêm các lỗi có bằng chứng:

- Công việc tìm không khớp trước đây chỉ hiện bảng trắng; nay có thông báo và Xóa tìm kiếm. Đổi loại công việc xóa từ khóa cũ; vẫn ghi rõ tìm trong trang, không giả vờ tìm toàn bộ server.
- Tài sản/tổng quan cá nhân có Thử lại khi tải lỗi.
- Click khoảng trắng bên trong dialog không đóng form; chỉ click ngoài cả lúc nhấn và thả mới đóng. Khóa cuộn nền, giữ Escape/nút Đóng và kiểm tra pending của từng form.
- Hủy form khách sạn/tầng không tải lại danh sách; tải lại chỉ sau lưu thành công. Ngăn việc mất focus nút mở sau khi hủy.
- Skip link chuyển focus vào main thay vì phá hash route. Đổi route/chi nhánh bắt đầu ở đầu trang.
- Menu dài tự xuống dòng ở tablet, không bị cắt. Lịch không có ca hiển thị thông báo trong vùng nhìn thấy thay vì giữa bảng rộng 1060px.
- Chuyển động giao diện tôn trọng prefers-reduced-motion.

## Tệp tạo/sửa trong lượt này

Tạo: `frontend/src/compact/page-toolbar.tsx`, `docs/ui/compact-ux.md`, `plans/261122-shared-ux/plan.md`, báo cáo này.

Sửa trong `frontend/src/compact/`: `app.tsx`, `task-ux.css`, `ui.tsx`, `work.tsx`, `documents.tsx`, `stock.tsx`, `insights.tsx`, `personal-overview.tsx`, `templates.tsx`, `catalog.tsx`, `assets.tsx`, `hotel-workspace.tsx`, `workday.tsx`.

Sửa `frontend/tests/compact-navbar-titles.mjs`, `frontend/tests/compact-workday-filter.mjs`, thêm script `test:compact:ux` trong `frontend/package.json`. Không sửa dependencies/package-lock trong lượt này. Các thay đổi Git khác đã có trước lượt này; không reset/commit/push.

## Kiểm chứng

- Build frontend `npm run build`: **Passed**, 1621 modules, Vite 15.69s ở lượt cuối, không cảnh báo build.
- Prettier các tệp sửa: **Passed**.
- `compact-navbar-titles.mjs`: **Passed** 48 tổ hợp (16 tuyến × 1440/768/375), tiêu đề/navbar/sticky header, không tràn ngang, không hàng tiêu đề trống/nút Làm mới, top padding ≤12px, menu dài không cắt, thông báo lịch rỗng trong viewport, search/action cùng hàng desktop, search không khớp và xóa, retry tài sản, dialog/padding/khóa cuộn/focus, skip link và chuyển trang. Thêm các dashboard theo vai trò. Không lỗi JavaScript, không request ghi.
- `compact-reception-rooms.mjs`: **Passed**; phân trang 48 phòng, drawer, occupied/readiness, metadata, nhận/trả/chuyển phòng qua mock.
- `compact-structure-settings.mjs`: **Passed**; hai chi nhánh, tạo/sửa định tuyến đúng, legacy redirect, responsive, guard thiếu chi nhánh. Chạy lại sau thay đổi callback lưu/hủy.
- `compact-workday-filter.mjs`: **Passed**; chọn đối tượng, chuyển tuần, gán ca đúng payload mock, màn hình cá nhân, lịch rỗng vừa viewport.
- `compact-dashboard-pagination.mjs`: **Passed**; phân trang/lọc tài khoản, sticky filter, dashboard admin/vận hành/owner/cá nhân và responsive.

Các test thành phần đã chạy riêng; script mới gộp đúng năm lệnh trên để chạy lại bằng `npm run test:compact:ux`. Xem `artifacts/navbar-page-titles/verification.json` cùng ảnh `page-*.png`; các bộ chuyên biệt có verification trong `reception-rooms`, `structure-settings`, `workday-filter`, `dashboard-pagination`.

Đã xem trực tiếp ảnh desktop công việc/chứng từ/báo cáo, mobile công việc/báo cáo/khách sạn và tablet lịch. Bố cục/độ dễ đọc/responsive/trạng thái rỗng đạt 2/2 trong phạm vi ảnh đã xem; không chấm toàn bộ luồng nghiệp vụ chưa chạy.

Trong quá trình kiểm tra có lỗi focus thực tế khi hủy form khách sạn và thông báo lịch rỗng nằm ngoài viewport; đã sửa và kiểm tra lại đạt. Fixture cũ dùng kind TASK không đúng enum giao diện hiện tại, được sửa thành OPERATIONAL để kiểm tra nút tạo với quyền có thật. Assertion lịch cũ tìm thông báo bên trong bảng đã được thay bằng kiểm tra thông báo hiển thị và nằm hoàn toàn trong viewport; không giảm yêu cầu kiểm chứng. Có một patch không khớp ngữ cảnh, chưa áp dụng; đã áp dụng lại đúng tệp.

## Giới hạn

Đây là kiểm chứng giao diện bằng trình duyệt thật với API fixtures, không phải kiểm thử end-to-end database thật. API/database thật: **Not Run**; C# build/test: **Not Run** vì không sửa backend. Không thay quyền, schema, trạng thái nghiệp vụ hoặc dữ liệu. Không tuyên bố kiểm hết mọi form, tổ hợp quyền, trình duyệt hay tình huống dữ liệu production. Không có lỗi còn mở trong các kiểm tra đã chạy; Git cảnh báo CRLF của một số tệp có sẵn, không phải lỗi build.

Tái sử dụng dev server 5186; không tạo server mới. Mọi browser kiểm thử đóng trong finally. Không gửi email, không login thật, không deploy/push.
