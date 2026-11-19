# Khắc phục ca bị kẹt của Lan — phương án dữ liệu

Đã đọc SQL thực: ca đang mở không có work_intervals. Nguồn chặn là CLN-20261016-002 (workflow_status NULL) và phiếu làm lại cũ của lần kiểm tra có nguồn trùng (phiếu đang IN_PROGRESS cũng thiếu workflow_status).

Đề xuất tạm giữ (ON_HOLD) đúng hai nguồn cũ đang chạy, giữ nguyên trạng thái vật lý, ngày bắt đầu và nội dung; không tự ghi hoàn tất hoặc tạo giờ công quá khứ. Lưu audit trước/sau và lý do phục hồi dữ liệu. Hồ sơ cũ vẫn tra cứu; không làm cho nguồn thiếu checklist/trùng phiếu trở thành hợp lệ để tiếp tục thực hiện.

Không tự kết thúc ca của Lan. Sau khắc phục, Lan có thể tự bấm kết thúc ca; các guard cho công việc khác vẫn giữ nguyên. Trước áp dụng: COPY_ONLY backup + VERIFYONLY, chạy transaction thử và rollback, sau đó mới commit. Không sửa quyền. Phạm vi constraint dùng chung được bổ sung ở phần dưới và đã được người dùng xác nhận.

Dữ liệu so sánh được thêm riêng với nhãn [DEMO], gồm công việc mới và khoảng thời gian đã kết thúc, nằm trong ca tương ứng, không chồng thời gian. Không dùng dữ liệu mẫu để thay kết quả phòng thật.

Phát hiện thêm lỗi schema: CK_rework_workflow_v1 chưa cho phép ON_HOLD trong khi source ReworkWorkdayBoundary và AssignedReworkExecutionService đã sử dụng trạng thái này. Gói cập nhật đúng constraint hiện hữu, giữ tất cả nhánh cũ và bổ sung ON_HOLD + IN_PROGRESS. SQL cụ thể: repair-draft.sql. Thay đổi này dùng chung BE1/BE2, cần xác nhận theo AGENTS.md trước khi áp dụng.
