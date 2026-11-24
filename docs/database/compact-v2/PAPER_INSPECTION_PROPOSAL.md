# Kiểm tra buồng phòng bằng checklist giấy

Người dùng đã chốt checklist giấy, hệ thống chỉ lưu Đạt/Không đạt và ghi chú (23/11/2026).

## Thay đổi cụ thể

- Endpoint `POST /api/v2/work/{cleaningId}/inspection-result`: tự ghi bản kết quả INSPECTION hiện hữu, không cần tạo phiếu thủ công; không sinh câu trả lời checklist hay giờ thực hiện giả.
- Dùng trạng thái PASS/FAIL, PASSED/FAILED, REWORK_REQUIRED hiện có. Không thêm bảng, cột, enum hoặc grants.
- Sửa duy nhất điều kiện checklist trong `TR_work_items_review`: chấp nhận sự kiện bất biến `PAPER_INSPECTION_RECORDED` do chính người xác nhận ghi, hoặc kết quả checklist điện tử hợp lệ như trước. Vẫn bắt buộc xử lý các lỗi cũ, người kiểm tra độc lập và đóng timer thực thi.
- Quyền INSPECTION_PERFORM, scope branch, account ACTIVE, RowVersion cha và phiếu cũ, operation key, chu kỳ phòng và rework được kiểm tra trong transaction. Không đóng lỗi cũ nếu thiếu DEFECT_RESOLVE hoặc chưa xác nhận và chưa gửi hoàn thành rework.
- Không tự gỡ Room Block; phòng có khách/đã đặt được giữ nguyên occupancy. Chỉ cập nhật READY khi view readiness hiện hành cho phép.

## Áp dụng và khôi phục

Script `scripts/database/compact-v2/Enable-PaperInspection.ps1` sao lưu trước, sửa trigger trong transaction; lỗi sẽ rollback. Không tạm tắt trigger, reset phòng, sửa dữ liệu lịch sử hay grants. Kiểm thử mutation trên database độc lập trước khi áp dụng primary. Khôi phục bằng định nghĩa trigger đã sao lưu hoặc backup; không sửa/xóa lịch sử xác nhận đã phát sinh.

Đã kiểm thử: 42 nhóm API giấy trên `HotelOpsCompactApiTestfba4a8d21f68`; 38 nhóm hồi quy trên `HotelOpsCompactApiTesta83bd8036681`. Bao gồm Không đạt → làm lại → Đạt, từ chối tự kiểm tra, scope/version/idempotency, guard không cho PASS nếu không có bằng chứng, tái sử dụng phiếu cũ và không gỡ Room Block.

Người dùng đã duyệt sao lưu và áp dụng Production cục bộ ngày 23/11/2026. Đã backup/verify rồi cập nhật HotelOpsCompactDB; trigger enabled, Production HTTPS5080 chạy bản mới. Grants và trạng thái phòng được đối chiếu count/checksum trước/sau, không đổi. Bản trigger gốc và bằng chứng ở `artifacts/production-261123-paper-inspection`; chi tiết backup, kết quả và giới hạn tại `plans/reports/implementation-261123-housekeeping-paper.md`.
