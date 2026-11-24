# Cấu hình vật tư theo nhóm và kho chi nhánh

## Kết quả

IMPLEMENTED và VERIFIED trong phạm vi thay đổi này. Trang OM có ba tab A/B/C với số cấu hình, mô tả riêng và biểu mẫu theo nhóm. Tạo mới kế thừa tab; sửa vật tư vẫn có thể đổi nhóm. A có số cấp đầu ca/mỗi khách/tồn tối thiểu; B có tồn tối thiểu; thiết bị gắn với phòng không có các trường cấp phát hoặc tồn kho. Bộ lọc trạng thái mặc định đóng.

Kho được hiển thị chung theo chi nhánh, không chọn riêng ở mỗi dòng. API tự tìm đúng một kho hoạt động cho vật tư lưu kho, kiểm tra kho do client cũ gửi và từ chối kho khác. Không đổi schema, grants hoặc dữ liệu kho. Phiên bản cấu hình cũ, audit và snapshot ca vẫn được giữ.

## Kiểm chứng

- `dotnet build HotelOps.slnx`: Passed, 0 warnings/errors.
- `dotnet test HotelOps.slnx --no-build --no-restore`: 101 Passed, 3 Skipped, 0 Failed.
- Frontend build và Release API publish: Passed. Vite cảnh báo thư mục output ngoài frontend không tự xóa; dùng thư mục artifact riêng có chủ đích.
- `npm run test:compact:ci`: 9 Passed.
- API thật trên clone `HotelOpsCompactWorkdayTestSupply261124`: 5 nhóm kiểm thử Passed, gồm hai chi nhánh A/B/C không gửi warehouseId, tăng phiên bản, từ chối kho sai, stale version, HK ghi và đọc chéo chi nhánh; request không xác thực bị từ chối. Đã xác minh PID API kết nối đúng clone và ít nhất 8 phiên bản lịch sử còn nguyên.
- Browser fixtures trên develop 5174 và bundle Production 5080: Passed hai chi nhánh; ba nhóm, trường form, lỗi lưu giữ dialog, thiếu/nhiều kho khóa lưu; mobile 390/320px không tràn ngang. API được intercept trong browser test, không dùng fixture như dữ liệu thật của ứng dụng.
- Đối chiếu SQL Production chỉ đọc: DVBV và DVCT đều đúng 1 kho hoạt động.
- Production API `/health` trả schema 3.0.0; proxy 5080 trả 401 khi không có session.
- Không thực hiện ghi thử cấu hình trên Production. Kết quả ghi API được xác minh trên clone.

Lần gọi API đầu thực hiện trước khi server lắng nghe nên ECONNREFUSED; chạy lại sau startup. Phép so sánh TEST ban đầu khác hậu tố UTC `Z` giữa dữ liệu vừa lưu và dữ liệu đọc từ SQL; chuẩn hóa phép so sánh rồi chạy lại toàn bộ, không bỏ kiểm tra RowVersion/nội dung. Các lần chạy cuối đều Passed.

## Production cục bộ và phục hồi

- Đã sao lưu COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY `HotelOpsCompactDB` trước cập nhật. Bằng chứng local: `artifacts/workday/backup-HotelOpsCompactDB.json`.
- API mới: `artifacts/production-261124-supply-groups/api`, PID 28312, HTTPS localhost:5769, Production. Manifest `runtime.json` cùng thư mục cha.
- Frontend mới đã sao chép vào thư mục đang phục vụ bởi PID 7680 tại HTTPS 127.0.0.1:5080. Bản trước lưu tại `artifacts/production-261124-supply-groups/frontend-before`; API trước vẫn ở `artifacts/production-261124-p0/api`.
- Nếu cần quay lui: xác minh và dừng PID API mới; chạy launcher cũ khi cổng 5769 trống; phục hồi frontend-before vào thư mục frontend đang phục vụ, index.html sau cùng. Không restore database vì không có migration.
- API clone tạm 5775 đã dừng sau kiểm chứng. TEST API 5774 và frontend 5174 được giữ.
- Không commit/push thay đổi của lượt này; yêu cầu push trước đã hoàn thành ở lượt trước.

## Files thay đổi

- `frontend/src/compact/supply-configuration.tsx`
- `frontend/src/compact/supplies.css`
- `frontend/src/compact/supply-api.ts`
- `src/HotelOps.Api/Compact/Services/SupplyConfigurationService.cs`
- `frontend/tests/supply-configuration-groups-browser.mjs`
- `scripts/verification/supply-configuration-groups.mjs`
- `docs/ui/compact-ux.md`
- Báo cáo này.

Evidence local: `artifacts/supply-config-validation/api-results.json`, `browser-results.json`, `production-warehouse-audit.json`, ảnh desktop/mobile. Bộ test không thực hiện lại toàn bộ nghiệm thu P0; các mục P0 còn mở trong báo cáo trước không được coi là đã hoàn tất bởi thay đổi này.
