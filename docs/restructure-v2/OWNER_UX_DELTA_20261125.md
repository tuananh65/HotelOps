# Điều chỉnh được Owner yêu cầu ngày 25/11/2026

Áp dụng cùng Master Prompt và D01–D12. Tệp đính kèm `f86ae379-3a2b-4c0e-8b3c-3b77ccf7a968/Pasted text.txt` được đối chiếu theo dòng: 2.853 dòng, không khác nội dung so với bản `374c8215-0833-41de-8145-86e59f82ca3a`; chỉ khác cách xuống dòng.

## Phạm vi bổ sung

- Đăng nhập theo bố cục ảnh, giữ thương hiệu HotelOps; icon, placeholder, hiện/ẩn mật khẩu và quên mật khẩu. Không suy ra yêu cầu Google OAuth từ ảnh bố cục.
- Kiểm tra và sửa tự kết thúc ca hết giờ, có lịch sử và xử lý vật tư đúng nghiệp vụ.
- Bỏ nút “Đang xem trong phạm vi quyền”; nhân sự hiển thị số điện thoại thay cột chi nhánh.
- Sắp xếp lại workspace yêu cầu; bỏ tiêu đề lặp khi nhúng màn hình con.
- Thêm tạo loại báo cáo phòng, giữ phân loại nghiệp vụ khắc phục rõ ràng.
- Cấu hình người nhận yêu cầu theo vai trò; không liệt kê nhân viên.
- Báo cáo OM thể hiện hoạt động ca, sự cố phòng, kho và tài sản trong chi nhánh.
- Gỡ chức năng xem vật tư nhân viên; giữ sổ cấp phát, reservation, tiêu hao và hoàn trả phục vụ các quy trình nghiệp vụ.
- Sửa drawer sự cố, không hiển thị khối thao tác không được cấp quyền; lịch sử dễ đọc.
- Gửi kết quả kiểm tra bằng popup; đạt không bắt buộc ghi chú, không đạt cần lý do.
- Người tạo là người lập yêu cầu gốc, không bị thay bằng người xác nhận hoặc tạo chứng từ khắc phục.
- Điều tra phòng 104 sau dọn lại; cung cấp đường xử lý sự cố hợp lệ, không tự đóng sự cố chưa xác minh để ép sẵn sàng.

## Phân công ngày 25/11, 13:11

Owner đã yêu cầu dùng sub-agent. `login_refresh` phụ trách đăng nhập/khôi phục mật khẩu; `shift_auto_end` phụ trách ca hết giờ; `room_incident_ux` phụ trách drawer, người tạo và phòng 104. Agent chính phụ trách cấu hình, báo cáo, điều hướng và tích hợp kiểm thử.

Mọi thay đổi database chỉ trên TEST đã backup; không tự commit, push hoặc deploy. Chưa đánh dấu các mục hoàn tất trước khi có kiểm chứng thực tế.

## Bằng chứng ngày 25/11, 14:02

`configuration-report-reviewed-fifteen-pass.json` ghi 15 kiểm tra HTTP/Edge đạt, cleanup sạch. Bao phủ role-only configuration, tạo báo cáo phòng không thay loại cũ, scope và validation, workspace không lặp tiêu đề/nút phạm vi/menu vật tư nhân viên, báo cáo sự kiện mobile. Ca tự kết thúc xuất hiện một lần từ audit; ngày báo cáo được đổi theo múi giờ chi nhánh.

Frontend/backend build đạt; 127 tests PASS, 3 SKIP. Không dùng kết quả này thay bằng chứng forgot-password, ca hết giờ có vật tư hoặc sự cố legacy. Ba phần đó đang do `login_finish`, `shift_finish`, `incident_finish` hoàn thiện và kiểm chứng.

Thay đổi cấu hình người nhận chỉ áp dụng khi lưu loại yêu cầu; chứng từ/yêu cầu cũ không bị viết lại. Gỡ trang xem vật tư nhân viên không xóa ledger, API cấp/hoàn hoặc reservation cần cho nghiệp vụ.

## Sửa bố cục đăng nhập theo ảnh bổ sung 14:06

Nguyên nhân icon đè placeholder: selector input dùng chung có độ ưu tiên cao hơn CSS login, ghi đè padding/height. Login dùng token control cục bộ để giữ ô48px và khoảng cách icon/chữ ít nhất10px. Thu khung còn460px, logo64px, căn lại khoảng cách tiêu đề/form/nút; mobile320px không tràn ngang. Kiểm Edge bốn viewport đạt (`login-layout-browser.json`), hiện/ẩn giữ đúng giá trị và kiểu mật khẩu. Đây là kiểm tra bố cục, không thay kết quả reset qua email.

Owner tiếp tục chốt đăng nhập chỉ bằng **Tên đăng nhập**, không sử dụng mã nhân viên. Quyết định này thay mô tả mã nhân viên ở ảnh mẫu cũ; áp dụng cả login và forgot-password phía UI/backend. Không đổi username, employee code hoặc mật khẩu đang lưu.

## Popup quên mật khẩu — 25/11, 14:21

Popup tối đa420px, bỏ khung nền lồng bên trong và khoảng trống dư. Ô tên đăng nhập có icon tách khỏi chữ; thông tin hạn30phút đặt dưới ô. Nút **Hủy** màu đỏ và **Gửi liên kết đặt lại** màu xanh cùng một hàng; chưa nhập tên thì chưa cho gửi. Nút × vẫn đóng popup.

`login-layout-browser.json` ghi bốn viewport1440×900,1140×720,390×844,320×640 đạt: không tràn ngang, input48px, icon cách chữ ít nhất10px, bật/tắt mật khẩu giữ giá trị, tên/placeholder chỉ Username, popup cao dưới400px, hai nút cao ít nhất40px và Hủy đóng đúng. Ảnh `recovery-layout-desktop.png` và `recovery-layout-small-mobile.png` đã được xem trực tiếp. Kiểm bố cục này không gửi yêu cầu khôi phục và không ghi database; SMTP/reset được kiểm riêng.

## Kiểm chứng tiếp theo — 25/11, 15:05

- Đăng nhập/khôi phục19/19 PASS: `login-recovery-username-nineteen-pass.json`. Chỉ Username; mã nhân viên không login/reset, kể cả trùng Username tài khoản khác. Reset qua SMTP sink, thu hồi mật khẩu/phiên cũ và token một lần/hết hạn đã kiểm. Không gửi email thật.
- Hết ca20/20 PASS: `housekeeping-expiry-settlement-twenty-pass.json`. Worker tự đóng đúng deadline một lần, giữ vật tư/reservation; việc cũ vẫn hoàn tất được; popup đối soát hoàn vật tư và cho bắt đầu ca tiếp theo. Dọn lại không tự gây bảo trì.
- Sự cố104 đã xác định còn incident legacy OPEN riêng dù cleaning/rework/inspection đã xong. Cần người có quyền xác minh và chuyển vào luồng sửa chữa; không tự đóng incident653 hoặc đổi trạng thái phòng trên DB vận hành. Đường phục hồi, người tạo gốc, popup và phân trang/tìm kiếm đang được kiểm HTTP/Edge; chưa công bố PASS mới.

## Kiểm chứng bổ sung — 25/11, 18:23

- **Số điện thoại và báo cáo kho/tài sản OM:** [lượt focused](../../artifacts/restructure-v2/phone-resource-focused-pass.json) và [chi tiết API/Edge](../../artifacts/restructure-v2/phone-resource-reports-live.json) đạt. API trả đúng số điện thoại đã lưu và null; UI hiển thị số hoặc “Chưa cập nhật”. Phiếu nhập đã ghi sổ và cập nhật tình trạng tài sản xuất hiện trong báo cáo hoạt động; báo cáo tồn kho/tài sản phản ánh dữ liệu thực của fixture. Chi nhánh khác trả 403; việc xem UI không thay chứng từ, tài sản hoặc tồn kho. Cleanup sạch. Đây là dữ liệu TEST tổng hợp và session fixture, không phải kiểm password login hoặc realtime.
- **Cảnh báo tồn thấp:** [10/10 kiểm tra](../../artifacts/restructure-v2/low-stock-ui-ten-pass.json) đạt, gồm inbox thật trên desktop/mobile cho vật tư tiêu hao và tái sử dụng. Số lượng khả dụng loại hàng hết hạn/cách ly và đồ đang giặt; cảnh báo cập nhật đúng khi gửi/nhận điều chuyển, phần thiếu vẫn chờ đối soát. Cleanup xác nhận `activeFixtureItems=0`. Không dùng kết quả này thay bằng chứng SignalR.
- **Sự cố cũ:** [lượt có lỗi snapshot](../../artifacts/restructure-v2/incident-snapshot-identifier-failure.json) ghi bảy kiểm tra đầu PASS, gồm liên kết SOURCE hợp lệ, phân trang/tìm kiếm, phục hồi đúng quyền và giữ người tạo/lịch sử, sửa chữa rồi một lần HKS PASS hoàn tất cleaning và đưa phòng về Ready. Toàn lượt vẫn **FAIL** ở kiểm tra quantity tiếp theo do SQL identifier trong harness snapshot; không gọi đây là suite “7/7 PASS”. Bằng chứng này không xác nhận đã xử lý phòng 104 hoặc incident653 trên database vận hành.
- **Build/test:** bản `.artifacts/finish-realtime` build thành công, còn bốn cảnh báo NU1900 do không truy cập được nguồn kiểm tra vulnerability. [Toàn solution](../../artifacts/restructure-v2/finish-realtime-test-summary.json) đạt 155 PASS, 3 SKIP. Cache quyền/chứng từ thông báo theo từng snapshot/page đã qua review độc lập không có finding chặn; kiểm business realtime sau sửa còn phải hoàn tất. Không coi ba test SKIP hoặc vulnerability scan là đã đạt.
- **Chưa hoàn tất:** lượt quantity/multiple-blockers đang tiếp tục; chưa nghiệm thu đầy đủ 48 E2E và tám demo. Maintenance Staff trên TEST hoạt động với 10 người được phục hồi, nhưng local vẫn role inactive/0 người; xem [chuẩn bị rollout local](LOCAL_ROLLOUT_READINESS.md). Không sửa database vận hành, không commit/push/deploy. AI ngoài và SMTP thật còn `BLOCKED_EXTERNAL_CONFIGURATION`; kiểm kê vật lý giữ trạng thái chờ theo quyết định Owner.

Trạng thái tổng hợp tại [STATUS_20261125.md](STATUS_20261125.md); các checkpoint trước được giữ làm lịch sử, không thay thế kết quả mới hơn.

## Cập nhật 18:45
[15/15 tích hợp](../../artifacts/restructure-v2/transfer-business-realtime-fifteen-pass.json), [bốn Edge contexts](../../artifacts/restructure-v2/business-realtime-verified-pass.json): đề xuất fallback → phiếu HTTP thật → thông báo sau commit → badge/popup/list tự cập nhật. Replay không tạo trùng, read ownership đúng, positive control cùng stream. Cleanup sạch; external AI chưa cấu hình.
Quantity/blockers12/12, housekeeping/rework/refill25/25 và phone/resource focused PASS. Xem STATUS_20261125.md; chưa cutover local.
