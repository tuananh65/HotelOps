# Cố định mức độ và bỏ cấu hình nghiệp vụ Admin

Đã cập nhật HotelOpsCompactDB sau backup COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY. Backup: `HotelOpsCompactDB_before_workday_20261122_195811673.bak`; metadata được giữ ở `artifacts/fixed-operational-levels/backup.json`.

## Kết quả

- PRIORITY: Thấp, Bình thường, Cao, Khẩn cấp.
- SEVERITY: Nhẹ, Trung bình, Nặng, Nghiêm trọng.
- Cả hai dùng mã LOW/NORMAL/HIGH/CRITICAL và thứ tự 1–4; đủ 8 giá trị đang hoạt động.
- Giữ ID 214/215/216, bổ sung 5 giá trị; không xóa/đổi các liên kết công việc và lỗi kiểm tra phòng.
- Bỏ Cấu hình nghiệp vụ, nút thêm/sửa, dropdown chọn cấu hình khỏi UI Admin. Các URL cũ hiển thị vai trò. Không thay đổi chức năng phân quyền vai trò.
- API chặn thêm/sửa/đổi nhóm/vô hiệu hóa hai nhóm cố định, kể cả khác hoa/thường hoặc dấu cách cuối. Các màn nghiệp vụ tiếp tục đọc DB theo thứ tự mức độ.
- Vẫn 40 bảng. Giữ UOM/ITEM_CATEGORY/ASSET_TYPE vì 6 vật tư, 14 dòng chứng từ và 6 tài sản đang tham chiếu; không ép bảng dùng chung chỉ còn hai kind bằng cách xóa dữ liệu liên quan.

## File tạo/sửa

- `scripts/database/compact-v2/fixed-operational-levels.sql`
- `scripts/database/compact-v2/Set-FixedOperationalLevels.ps1`
- `scripts/database/compact-v2/Seed-Testing-Scenarios.ps1` — đọc HIGH/NORMAL hiện có thay vì tạo lại mã demo bị API khóa; dừng nếu thiếu dữ liệu SQL khởi tạo.
- `src/HotelOps.Api/Compact/Services/CatalogService.cs`
- `src/HotelOps.Api/Compact/Controllers/WorkspaceController.cs`
- `frontend/src/compact/catalog.tsx`, `catalog-specs.ts`, `api.ts`
- `frontend/tests/compact-reference-settings.mjs`, `compact-permissions-ui.mjs`
- `docs/requirements/FIXED-OPERATIONAL-LEVELS.md`, plan, báo cáo này và journal cục bộ.

## Kiểm chứng

- `dotnet build HotelOps.slnx`: **Passed**, 0 warnings / 0 errors.
- `npm run build`: **Passed**, không cảnh báo.
- `dotnet test HotelOps.slnx --no-build`: **28 Passed / 2 Skipped / 0 Failed**. Hai test tích hợp worker/account-reset cần database fixture riêng không được bật trong lượt này.
- API/database: **Passed**. 12 request ghi danh mục bị từ chối 405; dữ liệu trước/sau giữ nguyên. Workspace trả đúng bốn giá trị mỗi nhóm theo thứ tự. Các FK liên quan vẫn enabled/trusted; ràng buộc reference_values không có vi phạm.
- Liên kết công việc: một bản ghi còn trỏ priority 214, một bản ghi còn trỏ severity 215; một finding vẫn trỏ severity 215. Các số lượng vật tư/tài sản/chứng từ giữ nguyên.
- UI desktop/mobile: **Passed** với 3 route settings, không còn nội dung cấu hình hoặc request đọc bảng cấu hình. Kiểm thử vai trò/phân quyền hiện có cũng **Passed**. Screenshot đã xem; không có lỗi browser.
- API đã chạy lại PID 2964, cổng 5768, health schema 2.1.0 và 40 bảng; FE 5186.

## Sự cố kiểm chứng và giới hạn

- Truy vấn kiểm tra toàn bộ constraints ban đầu timeout 30 giây. Đã kiểm tra lại phạm vi reference_values và các liên kết/FK liên quan thành công; không tuyên bố đã kiểm lại toàn bộ constraints toàn DB.
- Lượt browser/API đầu chạy khi API chưa ready, proxy trả 500 ở login. Sau khi xác nhận health, chạy lại toàn bộ test liên quan đạt.
- Một lần áp dụng patch test bị lỗi định dạng; đã khôi phục file, sửa và thực thi test thành công.
- Screenshot mobile đầu chụp giữa transition sidebar; harness đã chờ kết thúc chuyển động trước khi chụp lại.
- Script seed được kiểm tra cú pháp, không chạy lại toàn bộ seed vì sẽ thay đổi dữ liệu ngoài phạm vi. Database mới cần khởi tạo các mức cố định trước khi seed kịch bản.
- Đây là dữ liệu cố định ở tầng ứng dụng: SQL DBA vẫn có thể bảo trì trực tiếp. Không thêm trigger/migration hoặc thay quyền database.
- Không chạy lại toàn bộ E2E của các actor ngoài phạm vi. Không commit/push/deploy.
