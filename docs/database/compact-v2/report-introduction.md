# HOTELOPS — SYSTEM SIMPLIFICATION REPORT

Ngày: **21/11/2026**. Nguồn: tài liệu “SYSTEM SIMPLIFICATION CONSOLIDATION” người dùng gửi; `docs/requirements/HotelOps_SRS_DRAFT.md`; `SRS_CHANGE_NOTES.md`; code Reception/readiness legacy; schema Compact v1 và SQL Server local.

## A. Executive Summary

Thực hiện phương án **Level 2 — Balanced Simplification** trên database độc lập **HotelOpsCompactDB**, schema **2.0.0**, **37 bảng**, giảm từ 40 bảng của Compact v1 và vẫn dưới giới hạn 40. Database HotelOpsDB 149 bảng không bị sửa.

Yêu cầu chat mới “làm lại database vừa tạo theo giản lược này” cho phép thay database dù tài liệu nguồn vốn là đề bài phân tích có ghi “không thay đổi database”. Phạm vi áp dụng là **database thử nghiệm mới**; các dòng `PROPOSE_REMOVE`/`NEEDS_APPROVAL` vẫn không được diễn giải thành phê duyệt xóa UC trên sản phẩm đang chạy. Không chỉnh mã ứng dụng, seed quyền hoặc deploy frontend trong lượt sửa database này.

Thay đổi chính:

- Bỏ `shifts`, `workdays`, `time_corrections` khỏi schema mới; không yêu cầu bắt đầu workday để thực hiện nhiệm vụ. Việc bỏ UC-138/139 trên sản phẩm vẫn được ghi là đề xuất; nếu PO giữ chúng, cần thiết kế lại trước khi chuyển backend.
- Giữ `work_intervals` theo **work + assignment + user + branch**, ghi khoảng Start/Pause/Resume/Complete, chống overlap và bảo toàn thời gian đã chốt. Backend sẽ tự ghi thời gian từ thao tác; schema không tự thực hiện thao tác ứng dụng.
- Giữ xác nhận bàn giao backlog bằng sự kiện có recipient, đề nghị và một quyết định nhận/từ chối; không cần bảng ca hoặc bảng handover mới.
- Giữ lifecycle riêng cho Cleaning, Inspection, Rework, Incident, Maintenance, Operational Task và Internal Request. Dùng chung bảng lưu trữ không có nghĩa gộp chúng thành cùng một nghiệp vụ.
- Incident là hồ sơ nguồn; maintenance tham chiếu incident, tránh nhập lại thông tin ở UI/backend sau này. Vật tư dùng source document/line; view tự tính lượng còn thiếu, replenishment nằm trong module kho.
- Giữ scope, rowversion, lịch sử bất biến, approval, ledger/reversal, kiểm tra độc lập và readiness theo inspection hiện hành + block.

Schema v1 đã được kiểm tra **rỗng** trước khi thay. Installer thử tạo v2 rồi rollback về v1; kiểm tra độc lập vẫn đủ 40 bảng/version 1.0.0; sau đó mới áp dụng v2. Không có dữ liệu người dùng bị xóa hoặc được chuyển ngầm.
