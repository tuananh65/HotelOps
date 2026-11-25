# Luồng bảo trì V2

HK báo sự cố trong việc dọn phòng, giám sát xác minh và chuyển đúng nơi: thiếu đồ đến kho, hỏng đến kỹ thuật. Đội Maintenance Supervisor/Staff được giữ. Giám sát chọn nội bộ với người đủ quyền/chi nhánh hoặc sửa ngoài; completion có checklist snapshot và nghiệm thu kỹ thuật trước kiểm tra HK/readiness.

Người báo, người phát hiện và người liên quan tới nguyên nhân là các vai trò khác nhau. Người phát hiện mặc định actor, nếu chọn người khác phải active cùng chi nhánh. Cause investigation có lịch sử, không tự kết luận trách nhiệm từ tên người phát hiện. AI chỉ hỗ trợ phân tích.

Nhánh thay mới: NONE → PROPOSED → CONFIRMED → APPROVED hoặc REJECTED → INSTALLED. Người có quyền tương ứng xác nhận; OM duyệt độc lập; lắp đặt phải chứng minh tài sản mới hợp lệ ở đúng phòng/địa điểm và tài sản cũ đã được dời. Ghi AssetEvents cả tài sản cũ/mới, giữ liên kết lịch sử. Đề nghị chưa giải quyết chặn hoàn tất. Một vòng bị từ chối có thể mở vòng mới, không ghi đè lịch sử.

Phòng chỉ Ready khi đủ điều kiện tổng hợp của RoomReadinessService/view/trigger; không bỏ qua sự cố khác, checklist hoặc occupancy vì một việc đã hoàn thành. Không dùng hết thời gian dọn làm lý do tự chuyển phòng sang bảo trì.

Bằng chứng hiện có: 16 service/SQL checks của replacement, gồm shared EF context và nhiều vòng đề nghị; fixtures rollback. `maintenance-fifteen-pass.json` đạt 15 HTTP/SQL cases trên TEST: báo hỏng từ việc dọn, HKS xác minh, tiếp nhận nội bộ/sửa ngoài không cần note, checklist bắt buộc, kỹ thuật không tự nghiệm thu, FAIL quay lại sửa/reset checklist và PASS không note đóng việc, phòng Ready. API từ chối OM/HK tiếp nhận kỹ thuật và kỹ thuật tự ghi ý kiến giám sát.

`maintenance-inspection-browser.json` kiểm bằng Edge thật: HKS chọn người liên quan, lưu cause riêng với người báo/người phát hiện, gửi PASS không note; kiểm API sau lưu và màn hình 390px không tràn ngang. Chỉ các bước cuối này qua browser, phần tiếp nhận/sửa chữa chạy HTTP; chưa chứng minh toàn demo UI, đăng nhập, SignalR hoặc mọi blocker đồng thời.

Hai lỗi thực đã sửa: HKS điều tra nguyên nhân dùng quyền IR_PROCESS thay gate INCIDENT_MANAGE của kỹ thuật; gán template của parent được lưu trước snapshot checklist con trong cùng transaction để thỏa FK composite. Build `checklist-parent-build.log` 0 warning/error, test `checklist-parent-tests.log` 120 PASS/3 SKIP. Review độc lập đã kiểm tính atomic và scope; database vận hành không đổi.
