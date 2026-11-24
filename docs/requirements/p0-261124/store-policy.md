# Chính sách xử lý vật tư trong khách sạn

Quyết định của người dùng ngày 24/11/2026: vật tư trong phạm vi khách sạn do thủ kho xử lý; mỗi chi nhánh chỉ có một kho.

## Đã triển khai trong source

- Yêu cầu vật tư thông thường dùng quyền hiện có `MR_APPROVE_L1`, scope `BRANCH`, và bắt buộc vai trò `STOREKEEPER`. OM và vai trò cũ không còn đường duyệt dự phòng.
- Người tạo vẫn không được tự duyệt yêu cầu thông thường. Một thủ kho có thể duyệt yêu cầu do người khác tạo rồi xuất hàng. Giữ kiểm tra quyền xuất, chi nhánh, số lượng đã duyệt, tồn kho, giao dịch và phiên bản.
- Ngoại lệ khắc phục phòng đã được xác nhận nằm trong module riêng; thay đổi này không mở tự duyệt đại trà.
- API tạo/bật kho chặn kho hoạt động thứ hai. API không cho tắt kho cuối cùng. Cấu hình vật tư kiểm tra đúng một kho hoạt động và kho được chọn thuộc chi nhánh.

## Migration và mốc integration

`scripts/database/compact-v2/p0-store-policy.sql` chỉ chấp nhận database clone P0 TEST. Chạy sau migration vai trò và bản sao lưu mới đã kiểm chứng. Mốc bàn giao worker ban đầu chưa chạy script; Integration Owner sau đó xác nhận đã commit bộ migration P0 trên clone schema 3.0.0, có rollback/idempotency checks. Xem [database evidence](database-and-recovery.md); không suy ra API/runtime đã được kiểm thử từ việc commit schema.

Script kiểm tra toàn bộ khóa ngoại trỏ tới kho. Chỉ tự tắt kho phụ hoàn toàn không được tham chiếu khi chi nhánh có đúng một kho đang được tham chiếu. Nếu không xác định duy nhất, hoặc nhiều kho đều có dữ liệu, script xuất danh sách và rollback toàn bộ; không chọn theo tên DEMO-MAIN/DEMO-FLOOR, không sửa ledger, không chuyển tồn giả, không xóa lịch sử.

Sau đối soát, script tạo unique filtered index cho một kho hoạt động/chi nhánh, cấp `MR_APPROVE_L1` cho thủ kho, thu hồi đường duyệt của OM/DH, và sửa có kiểm soát điều kiện người duyệt/người xuất trong trigger `TR_material_issue_approval`. Những kiểm tra còn lại của trigger giữ nguyên. Constraint người tạo khác người duyệt không bị sửa.

Thứ tự integration: role migration → store policy → các migration khác có chỉnh cùng trigger phải giữ dấu `P0_STOREKEEPER_APPROVAL`. [Permission matrix](permission-matrix.md) đã thêm MR_APPROVE_L1 BRANCH cho thủ kho; Integration Owner sở hữu đồng bộ source contract `p0-role-contract.mjs` và SchemaGate.

## Validation

Source contract và cú pháp JavaScript được kiểm tra bằng `node --check` và `node scripts/verification/p0-store-policy-contract.mjs`. Đây không phải kiểm thử runtime. Không chạy build chung theo phân công để tránh đụng worker khác.

Integration owner cần chạy build/test C#, áp dụng script vào fixture, kiểm tra chạy lặp, kiểm tra đồng thời tạo hai kho, OM bị từ chối duyệt, thủ kho duyệt và xuất yêu cầu của người khác, tự duyệt bị từ chối, giới hạn số lượng và không âm kho. Nhánh có nhiều kho được tham chiếu cần quyết định giữ kho và phương án đối soát riêng trước khi migration có thể thành công.

Recovery: nếu transaction thất bại dữ liệu không đổi. Sau commit, phục hồi bản clone từ bản sao lưu hoặc migration đảo chiều được rà soát; không áp dụng script này lên Production.
