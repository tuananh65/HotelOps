# P0 — hồ sơ đồng bộ yêu cầu và triển khai Project 2

Cập nhật 24/11/2026. Các luồng P0 đã triển khai và có kiểm chứng trên TEST, nhưng trạng thái nghiệm thu toàn bộ cả A/B là **PARTIAL**; xem đính chính trong [báo cáo kết quả và giới hạn](implementation-report.md), [bằng chứng 28 E2E](validation-evidence.md). Chỉ áp dụng Compact Project 2; không thay thế yêu cầu hay database của Project 1, chưa deploy Production.

## Nguồn và thứ tự ưu tiên

1. Quyết định trực tiếp của người dùng, gồm [phần tiếp nối và quyết định](continuation-acceptance.md).
2. [B — P0 workflow](p0-workflow-request.md), cùng phần tiếp nối §§24–32.
3. [A — OM consolidation](om-consolidation-request.md), áp dụng các phần không mâu thuẫn B: tài sản, điều chuyển liên cơ sở, công việc vận hành, báo cáo, Action Center.
4. [Compact UX đã được chấp nhận](../../ui/compact-ux.md), giữ các lựa chọn không bị quyết định P0 thay thế.
5. [SRS lịch sử](../HotelOps_SRS_DRAFT.md) và [change notes](../SRS_CHANGE_NOTES.md), giữ nguyên lịch sử; không dùng ma trận cũ để cấp quyền trái P0.

Mâu thuẫn đã được giải quyết: không còn Head duyệt trung gian; Maintenance Supervisor giám sát sửa chữa bên ngoài; HK/Reception được báo cáo phòng theo luồng mới; HK tự kết thúc ca sau khi hoàn tất việc; vật tư nội bộ do thủ kho xử lý; chỉ chọn đồ đã gán vào phòng; mỗi chi nhánh một kho hoạt động. Checklist vẫn là bản giấy. Kiểm tra đạt khắc phục đánh giá toàn bộ điều kiện sẵn sàng, không tự tạo một lượt dọn mới như quyết định trước P0.

## Bộ tài liệu

| Tài liệu | Nội dung |
|---|---|
| [Ma trận quyền](permission-matrix.md) | Tám actor, quyền/scope, giới hạn theo nghiệp vụ, chuyển vai trò |
| [Nghiệp vụ và trạng thái](business-workflows.md) | Use case, routing, state machine, bất biến vật tư, room readiness, UX |
| [API](api-contracts.md) | Route thực tế, DTO, handler, version/key, endpoint cũ và mới |
| [Database và khôi phục](database-and-recovery.md) | Các bảng mới, migration, phạm vi TEST, bằng chứng và recovery |
| [Traceability và khoảng trống](traceability.md) | A/B → source → tiêu chí kiểm thử, phần chưa được chứng minh |
| [Runtime test plan](runtime-test-plan.md) | Hợp đồng runner E2E-01…28, fixture, đối soát |
| [Store policy](store-policy.md) | MR thông thường và một kho/chi nhánh |

## Cách đọc trạng thái

- **IMPLEMENTED**: có code/schema cho hành vi được mô tả; chưa đồng nghĩa chạy đúng end-to-end.
- **VERIFIED**: có kiểm chứng thực tế cho đúng phạm vi được nêu, kèm nguồn bằng chứng.
- **PARTIAL**: còn thiếu triển khai hoặc integration so với yêu cầu đầy đủ.
- **NOT_TESTED**: chưa có kết quả thực thi runtime được ghi nhận trong hồ sơ này.
- **BLOCKED**: có điều kiện bên ngoài cụ thể ngăn tiếp tục; không dùng cho việc đơn thuần chưa làm.

## Mốc bằng chứng tại thời điểm soạn

Integration Owner xác nhận clone TEST đã commit schema `3.0.0`: 49 bảng, 205 FK, 97 check, 38 trigger; rollback/idempotency migration đã chạy đạt. Có backup CHECKSUM, RESTORE VERIFYONLY và phục hồi clone thực tế. Nguồn backup/preflight xem tài liệu database.

Build backend đạt 0 warning/error; frontend tsc/Vite đạt. xUnit: 98 passed, 3 skipped. Đã chạy 28 nhóm API E2E (cộng dồn 1.349 assertions), 5 nhóm bổ sung, 8 nhóm transfer, browser 100/100 route/layout và 6 kiểm tra Action Center/reports. Ba xUnit integration bỏ qua và phạm vi browser cần đọc rõ trong báo cáo; không tính chúng thành kiểm chứng mọi biểu mẫu.

Production không thuộc đợt P0 và chưa được cập nhật bởi đợt này. Không commit/push/merge/deploy. Tên actor, mã quyền và định nghĩa schema trong tài liệu là contract Project 2, không phải thẩm quyền thay đổi primary.
