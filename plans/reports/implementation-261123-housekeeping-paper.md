# Buồng phòng: thao tác gọn và nghiệm thu bằng checklist giấy

## Kết quả triển khai

- **IMPLEMENTED** Nhận việc / Bắt đầu / Gửi hoàn thành không có ô lý do. Bỏ cập nhật phần trăm tiến độ cho Cleaning/Rework ở UI và từ chối tại API.
- **IMPLEMENTED** Bàn giao chỉ trước khi bắt đầu lần đầu. API kiểm tra cả ActualStartAt và lịch sử timer, kể cả lúc tạm dừng hoặc đã gửi kiểm tra; lời mời cũ không thể nhận sau khi công việc đã bắt đầu.
- **IMPLEMENTED** Giám sát bấm Xác nhận hoàn thành trên Cleaning, ghi Đạt/Không đạt và ghi chú. Checklist giấy không được số hóa thành câu trả lời giả. Không đạt bắt buộc ghi chú, giữ kết quả và tạo lỗi; sau khi rework gửi hoàn thành, giám sát có DEFECT_RESOLVE mới xác nhận đóng lỗi cũ.
- **IMPLEMENTED** Bản kiểm tra hiện có được tái sử dụng với RowVersion và đúng người được phân công. Thời gian đã thực hiện được đóng và giữ lịch sử; không tự tạo timer cho kiểm tra vật lý.
- **IMPLEMENTED** Bỏ Tạo phiếu kiểm tra / Mẫu checklist khỏi workspace giám sát, ẩn mã kỹ thuật và đoạn hướng dẫn đã chỉ định. Giữ dữ liệu điện tử cũ để tương thích và truy vết.
- **IMPLEMENTED** Tab Đã làm theo ngày: ngày trước/sau/hôm nay, tìm kiếm và phân trang. Backend lọc CompletedAt theo timezone chi nhánh, trả trạng thái hiện hành để phân biệt gửi hoàn thành và đã nghiệm thu.
- Sửa liên kết công việc buồng phòng liên quan từng trỏ nhầm sang MAINTENANCE.

## Bảo vệ nghiệp vụ

INSPECTION_PERFORM và CLEANING_VIEW đúng scope, account/session hợp lệ, người kiểm tra độc lập với mọi người từng làm Cleaning/Rework, đúng room turnover, parent/inspection RowVersion, operation key, transaction và audit. PASS không bỏ qua lỗi cũ hoặc Room Block, không thay occupancy OCCUPIED/RESERVED. Không sửa grants, entities, schema bảng hoặc Project 1.

## Kiểm chứng

- **VERIFIED** `dotnet build HotelOps.slnx --no-restore`: 0 warning/error.
- **VERIFIED** `dotnet test HotelOps.slnx --no-build --no-restore`: 56 Passed, 3 Skipped (các test opt-in worker/account integration ngoài bộ fixture của thay đổi này).
- **VERIFIED** `Test-Backend.ps1 -PaperInspection`: 42 nhóm PASS, database riêng `HotelOpsCompactApiTestfba4a8d21f68`.
- **VERIFIED** `Test-Backend.ps1 -HousekeepingStaff`: 38 nhóm PASS, database riêng `HotelOpsCompactApiTesta83bd8036681`.
- **VERIFIED** Frontend workflow tests: 5 PASS. HK Staff browser: thao tác không lý do, không progress/bàn giao sau start, conflict và 27 responsive views PASS. Paper supervisor browser: ghi kết quả, 409, validate Không đạt, lịch sử ngày và responsive PASS.
- **VERIFIED** Backend Release publish. Lần đầu có MSB3026 retry file font OFL.txt đang bị giữ; publish chạy lại thành công không warning. Vite cảnh báo outDir nằm ngoài frontend và không tự xóa; output ở artifacts được chủ động chỉ định.
- Lần đầu fixture Room Block sai tên cột đã sửa; fixture hồi quy cũ thiếu BranchId, ca trực và bước đặt phòng đã được cập nhật đúng contract hiện hành. Không sửa quyền/logic để làm test pass. Selector kết quả UI được bổ sung accessible label rõ ràng.

## Files của thay đổi

Frontend: `work.tsx`, `api.ts`, `housekeeping-workspace.tsx`, `housekeeping-review-queue.tsx`, mới `paper-inspection.tsx`, `housekeeping-history.tsx` trong `frontend/src/compact`.

Backend: `Compact/Services/WorkService.cs`, `WorkService.Housekeeping.cs`, mới `WorkService.PaperInspection.cs`; `Compact/Controllers/OperationsControllers.cs`; mới `Compact/Contracts/PaperInspectionRequest.cs`.

Tests/scripts: `frontend/tests/compact-housekeeping-staff.mjs`, mới `compact-paper-inspection.mjs`; `scripts/database/compact-v2/Test-Backend.ps1`, `test-housekeeping-staff.ps1`, mới `test-paper-inspection.ps1`, `Enable-PaperInspection.ps1`, `enable-paper-inspection.sql`.

Docs: `docs/ui/compact-ux.md`, `docs/database/compact-v2/PAPER_INSPECTION_PROPOSAL.md`, plan `plans/261123-1443-housekeeping-simplification/plan.md`, báo cáo này. Các thay đổi có sẵn khác được giữ nguyên, không commit/push.

## Production

**VERIFIED:** Người dùng đã duyệt và đã áp dụng lên HotelOpsCompactDB, cập nhật Production tại **https://127.0.0.1:5080/** ngày 23/11/2026. Backup COPY_ONLY/CHECKSUM đã được RESTORE VERIFYONLY trước khi thay trigger trong transaction. Trigger vẫn bật, chỉ đổi điều kiện checklist đã duyệt.

- Backup: `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\HotelOpsCompactDB_before_workday_20261123_152550699.bak`.
- Định nghĩa trigger gốc: `artifacts/production-261123-paper-inspection/review-trigger-before.sql`.
- API PID 24860, môi trường Production, health trả `ok`, schema 2.1.0. SHA256 DLL: `1F1ED33BD53F3FAA149C35E178E7980EC40CED5E4133A3C69D800F47F7B22A2B`.
- Frontend launcher 45096, listener 45304, bundle `index-B9QMvD78.js` HTTP200. HTTPS được trình duyệt tin cậy, login/ảnh logo hoạt động, API chưa đăng nhập trả401, không lỗi runtime. Preview 5082 và API kiểm thử 5187 đã dừng; giữ API5769 và FE5080 để sử dụng.
- Đối chiếu baseline trước/sau: 131 grants/checksum19132, 96 phòng/checksum2031418514, 1 work item, 57 users, tất cả không đổi. Không chạy mutation test trên primary.
- Bằng chứng triển khai: `artifacts/production-261123-paper-inspection/verification.json`, `runtime.json`, `database-before.json`, `database-after.json`; backup receipt `artifacts/workday/backup-HotelOpsCompactDB.json`.

Giới hạn kiểm chứng: luồng ghi Đạt/Không đạt và làm lại được chạy trên database fixture riêng; Production chỉ smoke test đọc. Ba test opt-in ngoài bộ fixture vẫn Skipped. Production cục bộ dùng frontend đã build phục vụ qua Vite preview, API chạy tiến trình nền; chưa cấu hình Windows service tự khởi động sau reboot.

Bằng chứng: `artifacts/compact-v2/backend/paper-inspection-verification.json`, `housekeeping-staff-verification.json`, `artifacts/paper-inspection-ui/verification.json`, `artifacts/housekeeping-staff/ui-verification.json`.

## Nhật ký quyết định

Checklist giấy là quyết định mới rõ ràng, thay cho cấu hình checklist/hạng phòng đã hủy. Dùng entity INSPECTION và lifecycle có sẵn để giữ readiness và audit; bổ sung một đường xác nhận vật lý, không thêm hệ thống lifecycle song song. Người dùng đã duyệt riêng thay trigger trước primary. Build cuối, UI reinspection (xác nhận đóng lỗi/ghi chú), triển khai và smoke test Production đã hoàn tất.
