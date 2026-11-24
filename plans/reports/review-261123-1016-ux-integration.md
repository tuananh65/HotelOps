# Kiểm chứng tích hợp UX Project 2 — 23/11/2026

Đã thực hiện phạm vi `plans/261123-ux-role-refinements/ux-review.md`: cập nhật fixture, bổ sung kiểm tra qua shell thật, review chỉ đọc và ghi bằng chứng. Chỉ sửa test và báo cáo; không sửa source ứng dụng, backend, schema hoặc dữ liệu primary. Không commit/push/deploy.

## Phát hiện cần người phụ trách ứng dụng sửa

### P2 — Form liên hệ khách sạn chấp nhận số điện thoại sai định dạng

- Vị trí: `frontend/src/compact/hotel-workspace.tsx:235`, `pattern="[+0-9 ()-]{8,30}"`.
- Tái hiện: mở Cấu trúc khách sạn → chọn khách sạn → Chỉnh sửa khách sạn → nhập `not-a-phone` vào Số điện thoại. Edge trả `checkValidity() === true`, `patternMismatch === false`. `type="tel"` không tự kiểm tra định dạng; pattern hiện tại không có hiệu lực.
- Review backend: `CatalogService.SaveCore` chỉ kiểm tra kiểu và chiều dài field Phone, chưa có kiểm tra định dạng tương ứng. Chưa thực hiện POST/PUT thật để xác minh lưu SQL.
- Đề nghị sửa pattern cho tương thích regex HTML hiện hành và kiểm tra định dạng ở backend. Chạy lại test tích hợp sau khi sửa.
- Test mới **giữ assertion thất bại**, không đổi kỳ vọng cho dữ liệu sai. `artifacts/ux-role-integration/verification.json` ghi 4 nhóm Passed, 1 nhóm Failed; `failure-3.png` lưu form lỗi. Không gửi số điện thoại sai đến API thật hay fixture.

## Kiểm chứng đã chạy

Chạy từ `frontend`, sử dụng Vite hiện hữu tại `http://127.0.0.1:5186`, Edge headless. Browser fixture chặn API; không login hoặc ghi SQL thật.

| Lệnh | Kết quả |
|---|---|
| `npm run build` | **Passed**, TypeScript và Vite build 1657 modules; không có warning compiler/bundler trong output. |
| `node --test tests/compact-*.test.mjs` | **Passed 9/9**: workflow/quyền thực thi, CSV và phân nhóm tình trạng tài sản. |
| `node tests/compact-navbar-titles.mjs` | **Passed**, 48 trường hợp: 16 tuyến × 1440/768/375px, navbar, tiêu đề, sticky, dialog. |
| `node tests/compact-structure-settings.mjs` | **Passed**, menu mới, hai chi nhánh, create/edit đúng scope, redirect cũ, thiếu branch và responsive. |
| `node tests/compact-reception-rooms.mjs` | **Passed**, check-in/out giả lập, drawer/focus, phòng có khách/chặn, 48 phòng/4 trang, lọc và responsive. |
| `node tests/compact-workday-filter.mjs` | **Passed**, chọn đối tượng, giữ lựa chọn khi chuyển tuần, route phân ca, lịch OWN và responsive. |
| `node tests/compact-dashboard-pagination.mjs` | **Passed**, 56 tài khoản, trang 10/20/50 dòng, filter/reset, sticky, 4 biến thể dashboard và responsive. |
| `node tests/compact-operations-manager.mjs` | **Passed**, 9 menu, grant giảm, cấu hình chỉ đọc, GET assets, phân trang không tính heading, 27 trường hợp responsive. |
| `node tests/compact-account-refinements.mjs` | **Passed**, điện thoại/STT, lọc bộ phận, password8, duplicate, payload tạo và tự sửa hồ sơ, stale version, responsive. |
| `node tests/compact-workday-scheduling.mjs` | **Passed**, nhiều người/ngày, tháng, xác nhận/bỏ mốc lễ, 409 giữ form/key retry, cấu hình và khóa lịch sử, OWN, 3 viewport. |
| `node tests/compact-operations-refinements.mjs` | **Passed**, day/week/month query, segmented counts, nhóm/tìm/lọc tài sản trước phân trang, nhãn Việt và responsive. |
| `node tests/compact-ux-role-integration.mjs` | **Failed: 4/5 nhóm Passed**; còn lỗi validation điện thoại nêu trên. Không có pageerror hoặc request ngoài fixture. |
| `npm test` | **Failed, dừng giữa chừng** sau 9 Passed và 12 timeout ở `activate-user-router-integration.test.mjs` của giao diện cũ. Không phải kết quả toàn bộ suite. |
| Prettier các test được sửa | **Passed**, đã format qua bản cài sẵn. |
| `git diff --check` trên bốn test tracked được sửa | **Passed**; Git có thông báo chuẩn hóa LF → CRLF, không phải lỗi code. |
| Backend build / `dotnet test HotelOps.slnx` | **Not Run**, theo giới hạn phân công không build backend; không sửa C#. |
| API thật / database | **Not Run**; fixture không chứng minh authorization SQL, transaction, rollback hoặc concurrency. |

`npm run test:compact:ux`: **Failed ở lượt cuối**. Navbar hoàn tất 48/48, sau đó `compact-structure-settings.mjs:90` timeout 5 giây chờ `.topbar h1` lúc mở trang đầu. Từng script thành phần đã chạy Passed sau cập nhật, nhưng chưa có lượt chạy cả chuỗi cuối thành công. Không tăng timeout tiếp hoặc đổi assertion để che sự bất ổn này.

## Test và fixture thay đổi

- `frontend/tests/compact-structure-settings.mjs`: thêm danh mục dùng chung/quyền hệ thống vào kỳ vọng menu; xác nhận tạo danh mục không nhập/gửi code thủ công.
- `frontend/tests/compact-operations-manager.mjs`: fixture GET `assets` có room/location, taskWindow, 9 menu, loại heading khỏi số dòng; thêm cấu hình vào responsive và kiểm tra không có nút ghi với ROOM_VIEW-only.
- `frontend/tests/compact-reception-rooms.mjs`: nhãn sau checkout là Cần dọn dẹp; chờ shell khởi tạo trước assertion nghiệp vụ.
- `frontend/tests/compact-dashboard-pagination.mjs`: taskWindow theo contract mới; chờ từng lần đổi trang trước khi click tiếp; ghi screenshot/network/navigation khi lỗi; chờ shell khi tải lần đầu.
- `frontend/tests/compact-workday-scheduling.mjs`: giữ nguyên assertion nghiệp vụ; thêm bằng chứng khi lỗi và chờ shell ở startup/reload để tách tải Vite khỏi timeout thao tác. Đây là file đã có trước lượt này, không phải mới tạo trong review.
- Tạo `frontend/tests/compact-ux-role-integration.mjs`: cây lồng tầng/khu/phòng, lọc phòng đúng vị trí, Pager range/total, lưu đủ contact + rowVersion; bật quyền chỉ gửi isActive, phản hồi 409 không đóng form; hồ sơ từ shell với role không có USER_VIEW, keyboard/focus/31px controls ở 3 viewport; Reception gom ba trạng thái chờ/làm lại, tách đang dọn/có khách, không có tạo incident dù fixture có grant; thêm regression điện thoại sai.
- Tạo báo cáo này. Không sửa `compact-workday-filter.mjs` hoặc các test account/operations do phiên khác đã tạo; chỉ chạy chúng.

## Review chỉ đọc

- Hồ sơ cá nhân dùng Actor.Id ở backend; DTO không nhận userId/role/branch/status, có rowVersion và audit. Fixture kiểm tra payload chỉ gồm các trường hồ sơ.
- Bật/tắt permission dùng ROLE_MANAGE global; backend chỉ cho cập nhật IsActive, kiểm tra quyền đang gán cho tài khoản active. Test xác nhận lỗi backend vẫn hiện và không mất form; chưa chứng minh điều kiện SQL trên DB thật.
- Assets GET kiểm tra ASSET_VIEW theo branch, chỉ projection; editor/action tiếp tục dùng detail/version. Thống kê kỳ dùng timezone branch và cận UTC cuối loại trừ.
- Phân ca batch có transaction Serializable, lock dùng chung, kiểm tra version, capacity/candidate trước khi thêm và audit/event. Chưa thử tranh chấp hoặc rollback thật.
- Không phát hiện thêm lỗi bảo mật có bằng chứng trong các đường đã đọc. Đây là review giới hạn theo phần tích hợp, không phải audit toàn bộ hệ thống. Không đề xuất refactor ngoài phạm vi.

## Bằng chứng và giới hạn

- `artifacts/ux-role-integration/verification.json`, `profile-focus-{1440,768,375}.png`, `reception-groups-{1440,768,375}.png`: shell, focus, grouping và kết quả lỗi. Đã xem ảnh focus/mobile; ảnh dùng tắt animation để tránh chụp giữa chuyển động sidebar.
- `artifacts/navbar-page-titles/verification.json`, `structure-settings/verification.json`, `reception-rooms/verification.json`, `workday-filter/verification.json`, `dashboard-pagination/verification.json`.
- `artifacts/operations-manager/ui-verification.json`, `operations-refinements/verification.json`, `workday-scheduling/verification.json`.
- `artifacts/ux-integration-unit-tests.log`: output `npm test` dừng giữa chừng. Test cũ timeout gồm invalid route ID, xác nhận activation, reason boundary; chưa xác định nguyên nhân, không sửa module cũ để làm xanh suite.
- Có các lần Vite/browser trả trang trắng hoặc reload giữa thao tác. Screenshot, network/navigation diagnostics được thêm ở test lịch/dashboard; các lần chạy riêng cuối đã qua. Source chung cũng được ghi lại trong khi review; không coi một lần timeout là bằng chứng lỗi nghiệp vụ. Không che các lần thất bại hoặc suy diễn cả môi trường ổn định hoàn toàn.
- Hook chặn gọi trực tiếp đường dẫn Prettier trong node_modules; đã dùng `npx --no-install prettier` thành công, không sửa hook/skill/config. Kiểm tra tiến trình read-only xác nhận runner npm test đã dừng; không dừng API/Vite.

Việc còn lại tại thời điểm review: người phụ trách source sửa validation điện thoại; xác định nguyên nhân tải shell không ổn định khi chạy chuỗi UX; xử lý riêng 12 timeout của suite cũ; kiểm chứng API/database và backend build theo nhiệm vụ tích hợp chính.

## Cập nhật từ phiên tích hợp

Parent đã sửa pattern điện thoại tương thích browser và kiểm tra định dạng tại CatalogService. Browser integration chạy lại đạt 5/5; API/SQL trên binary mới đạt 30/30, gồm từ chối điện thoại sai và không để lại ghi dở dang. Backend build 0 warning/error; 43 .NET tests Passed, 3 Skipped. Receipt cuối và giới hạn chuỗi UX được ghi trong `implementation-261123-1016-ux-role-refinements.md`; các thất bại lịch sử ở trên được giữ nguyên.
