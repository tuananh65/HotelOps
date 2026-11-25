# Notification và SignalR

Nguồn sự thật là notifications trong SQL, có recipient/branch/domain/event key. NotificationRealtimeReader tạo scope mới mỗi pulse, kiểm tra session, SecurityStamp, trạng thái user và first-login; chỉ đọc dữ liệu đã commit. Hub authenticated stream qua LongPolling theo frontend hiện tại, poll 2 giây, mỗi connection một subscription.

Snapshot lọc quyền theo domain trước khi trả unread count/IDs; transfer có ngoại lệ hai branch tham gia, không mở cả ledger chi nhánh khác. SHA-256 trên danh sách IDs giúp không phát lại snapshot không đổi. `VisibilityChecks` chỉ tái sử dụng phép kiểm tra role/permission trong một lần snapshot; lần tiếp theo đọc lại quyền. Không cache quyền xuyên user/session/branch hoặc suốt đời connection.

Frontend resubscribe sau automatic reconnect; khi transport đóng có retry onclose. Offline unread được phát lại khi reconnect. Logout/first-login/session revocation cắt stream. Không hứa mỗi user chỉ có một toast trên mọi tab; dedup hiện theo connection/event.

Bằng chứng 25/11: recovery-only 5/5; optimized full run lần 2 15/15. Lần 1 cùng binary/harness 8 PASS/1 FAIL (assertion rollback đếm thêm frame, chưa chứng minh dirty read), các ca sau chưa chạy. Giữ cả hai artifact trong report `plans/reports/realtime-v2-optimized-test.md`. Restart scenario khoảng 53 giây; không phải SLA production. Có log SQL cancellation 3980 lượt đầu.

Lượt hiện hành sau review: `realtime-reviewed-fifteen-pass.json` đạt 15/15 HTTP/hub. Lỗi đếm frame rollback được xác định ở harness: connection gốc chưa nhận sự kiện đã commit của ca reconnect trước. Harness chờ đúng ID trước ca tiếp theo và kiểm trực tiếp ID rollback không xuất hiện trong bất kỳ frame nào; không thay backend hoặc nới assertion. Giữ `realtime-owned-rollback-failure.json` làm bằng chứng lỗi cũ.

`realtime-reviewed-browser-five-pass.json` đạt 5/5 trên Edge với SignalR LongPolling thật qua proxy TEST: badge/popup đúng người và chi nhánh, đóng popup không hiện lại, đọc cập nhật badge, reload không lặp popup, thu hồi session chặn API và ID thông báo mới không tới stream. Cleanup xác minh hoàn tất. Hai browser dùng session TEST, thông báo được tạo/đọc bằng fixture SQL; chưa chứng minh toàn chuỗi thao tác nghiệp vụ UI tạo notification, password login, WebSocket hay SLA production. Reload browser không được tính thành transport reconnect; reconnect được kiểm riêng trong lượt HTTP/hub.
