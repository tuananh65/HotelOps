# Frontend change register V2

Phạm vi `frontend/src/compact`, giữ PageTitle, branch context, popup và menu đã được chấp nhận; thay đổi nghiệp vụ theo Master và D01–D12.

| Nhóm file | Hành vi mới | Kiểm chứng / phần còn thiếu |
|---|---|---|
| work.tsx, room-supply-assessment.tsx | Kiểm tra lượng còn dùng được, hiển thị thiếu/reservation, checklist thực thi | Browser Start/End Shift, bắt đầu việc, kiểm đếm 3 còn dùng/1 bổ sung, checklist/complete và HKS nghiệm thu không note/checkbox đạt. Còn assignment UI |
| room-issues.tsx, cleaning-incidents.tsx | Người phát hiện, phân loại nguyên nhân, người liên quan và lịch sử | `maintenance-report-ui-seventeen-pass.json`: HK báo hỏng/return-to-cleaning; HKS chuyển kỹ thuật; MS phân công nội bộ/ngoài; staff nhận/gắn checklist/start/complete; MS nghiệm thu; HKS FAIL rồi PASS. Chọn người liên quan và reporter/discoverer riêng đạt. Reception/full branch matrix còn thiếu |
| asset-replacement.tsx, assets.tsx | Đề xuất/giám sát xác nhận/OM quyết định/thay thực tế; pending assets không tự kích hoạt | 16 service/SQL replacement, chặn nút tự duyệt và terminal; cần chuỗi kỹ thuật/HK hoàn chỉnh |
| inventory-lifecycle.tsx, stock.tsx | Cấu hình loại vật tư, xem lô/hạn dùng, tìm kiếm trước phân trang, lịch sử từng lô và xác minh metadata | Đã bỏ cắt ngầm 1000 lô/500 lịch sử, bổ sung phân trang API và nhãn mobile; build và runtime đạt (`transfer-batch-pages-thirteen-pass.json`, `batch-browser-results.json`). API kiểm biên trang, browser lọc đúng lô/lịch sử/empty state/390px |
| reusable-inventory.tsx | Sở hữu/vị trí, giặt là, hỏng, đề nghị ghi giảm và lịch sử có phân trang | 43 kiểm tra đạt; UI READY→IN_USE→DIRTY→LAUNDRY→READY đối chiếu nguồn/đích và tổng sở hữu; OM duyệt ghi giảm qua UI. Còn HK thay khăn và trả đồ bẩn/hỏng cuối ca |
| supply-configuration.tsx | Nhóm B có classification REUSABLE được cấu hình lượng cấp đầu ca; không tự tiêu hao | HTTP cấu hình và cấp/trả đạt; chưa browser form cấu hình |
| transfers.tsx, transfer-progress.tsx | Hiển thị riêng số yêu cầu/duyệt/gửi/nhận/chờ đối soát theo D12 | 12 checks có multi-shipment/UOM; UI đủ bốn actor, nhãn số lượng mobile xác minh bằng CSS và screenshot thực |
| batch-adjustment.tsx, inventory-documents.tsx | Chọn lô điều chỉnh giảm, OM duyệt độc lập, Thủ kho ghi sổ | HTTP/SQL xác minh mixed lines, quyền, số lượng và không post trùng |
| requests.tsx, use-domain-requests.ts | Hộp yêu cầu chung hiển thị ghi giảm và thay tài sản, lọc người xử lý; mở form domain hiện có | Browser OM duyệt ghi giảm; HK tạo refill/HKS duyệt/SK cấp đạt, kiểm balance/history/ledger. Notification event reload đạt, chưa SignalR browser |
| account-management.tsx, account-policy.tsx | Invitation/resend/trạng thái gửi và thiết lập mật khẩu | 15 HTTP/mail sink/browser đạt; admin create/resend, first-login hai tab/setup token/revoke tab cũ; chưa SMTP thật |
| auth.tsx, forgot-password.tsx, CSS đăng nhập | Username-only, hiện/ẩn mật khẩu, popup quên mật khẩu gọn và nút Gửi liên kết đặt lại | login-recovery-username-nineteen-pass.json19PASS HTTP/sink/Edge; login-layout-browser.json4viewportPASS. Không dùng EmployeeCode, không đổi tài khoản cũ. Chưa SMTP thật |
| notification-realtime.tsx, ui.tsx | Nhận cập nhật sau commit, tải lại query có liên quan | 15 HTTP/hub + 5 browser đạt sau review; badge/popup/read/reload/revoke, chưa toàn chuỗi business UI trigger/SLA |
| advisory-panel.tsx | Dự báo/đề xuất được ghi rõ nguồn thống kê hoặc provider | DEMAND tiêu hao dương API; TRANSFER dương UI và trừ cam kết; INCIDENT mobile HKS/MS, scope/không mutation đạt. External AI chưa cấu hình |
| internal-settings.tsx, request-recipient-fields.tsx | Cấu hình chỉ liệt kê role nhận; người gửi chọn người nhận hợp lệ theo role. Thêm loại báo cáo phòng, family nghiệp vụ không tự mở rộng | configuration-report-reviewed-fifteen-pass.json: API scope/validation và Edge tạo loại mới |
| app.tsx, work.tsx, scheduling-staff.tsx, requests.tsx/requests.css | Gỡ view/menu vật tư nhân viên; bỏ scope button; Branch→Phone tại nhân sự; ngăn tiêu đề lặp và bố trí workspace | Edge desktop/mobile kiểm menu/button/header. staff-phone-visual.json đạt1440/768/375 với API mock chỉ đọc: số điện thoại/null đúng, không cột Chi nhánh, không scope/menu vật tư; bảng cuộn trong khung mobile, không tràn trang. Không chứng nhận dữ liệu điện thoại thật. Ledger/API cấp/hoàn giữ nguyên |
| insights.tsx | Hoạt động khách sạn mặc định, lịch sử theo ngày chi nhánh, nhãn ca tự hết giờ, giới hạn10000 rõ ràng | 15 kiểm tra cấu hình/báo cáo API/Edge; mobile không tràn ngang, dữ liệu shift/incident/audit thực |

Tên file cụ thể cần đối chiếu danh sách source tại thời điểm bàn giao; bảng này mô tả nhóm trách nhiệm, không xác nhận mọi file đã có browser acceptance.

Build frontend gần nhất tại checkpoint14:02: `ux-delta-reviewed-frontend.log`, thành công. Rollup báo bỏ hai annotation PURE trong thư viện SignalR. Các screenshot/JSON kiểm tra trình duyệt nằm trong `artifacts/restructure-v2/`; lượt housekeeping UI/ledger đạt 18 kiểm tra (`housekeeping-completion-reviewed-eighteen-pass.json`). Login/forgot-password, expiry custody và incident legacy đang có delta tiếp theo, chưa lấy build cũ để tuyên bố hoàn tất.

Checkpoint15:05: login/recovery19PASS và expiry/settlement20PASS đã có artifact riêng; phần incident mới đang kiểm. Build frontend sau thay đổi phân trang PASS (`incident-final-frontend-build.log`), giữ hai warning PURE của SignalR.
