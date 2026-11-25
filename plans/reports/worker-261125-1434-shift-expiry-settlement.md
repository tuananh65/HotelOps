# Kiểm chứng tự kết thúc ca và đối soát

- Database chỉ dùng `HotelOpsCompactWorkdayTestV22611250010`. Backup COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY thành công trước fixture. Không thay database vận hành.
- Build `HotelOps.slnx`: PASS, 0 warning/error. `dotnet test`: 127 PASS, 3 SKIP. Evidence: `artifacts/restructure-v2/resume-shift-build.log`, `resume-shift-tests.log`.
- Runtime: **20/20 PASS**, cleanup sạch, phiên TEST revoked, branch tổng hợp inactive. Evidence: `artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json`, `resume-shift-runtime.log`.
- Worker thật tự đóng attendance đúng biên giờ, chỉ quan sát SQL, không GET để kích hoạt. AUTO_END đúng một lần; ledger, custody và reservation không bị thay đổi bởi timer.
- Nhân viên hoàn thành công việc đã nhận sau khi hết ca; ca mới bị chặn trước đối soát. Popup mobile đối soát hoàn 16 vật tư tiêu hao cùng vật tư tái sử dụng, giữ nguyên ended_at. Retry không ghi sổ hai lần. Sau đối soát ca mới bắt đầu/kết thúc thành công.
- Kiểm tra dọn lại không chuyển phòng sang bảo trì; cấp phát, FEFO refill và tiêu hao thực tế giữ đúng số dư.
- `node --check` hai runtime/browser helpers và contract script PASS; contract TSX/static PASS.
- Không sửa nghiệp vụ mới trong lượt resume: các thay đổi trước đã đúng và được kiểm chứng lại. Timeout Start Shift trước được lưu tại `shift-start-browser-timeout-preserved.json`; log cũ ghi SQL operation cancelled sau browser timeout. Lượt mới dùng cùng timeout và source, không che lỗi bằng tăng timeout. Nguyên nhân tải môi trường của lượt cũ chưa được chứng minh độc lập.
- Runner đã dừng API5779 và worker riêng, xóa metadata process/fixture của lượt; lịch sử TEST được giữ theo fixture design. Cổng5779 được trả cho runner tiếp theo.

Không có câu hỏi nghiệp vụ còn chờ trong phạm vi ca làm việc.
