# Đóng phân công quá hạn — 25/11/2026

Đã áp dụng lên hai runtime local. Development: http://127.0.0.1:5173 (API 5081). Production: https://localhost:5080.

## Hành vi

- WORKING vẫn đóng tại giờ kết thúc đã cấu hình.
- PLANNED đóng khi đủ một giờ sau giờ kết thúc theo múi giờ chi nhánh. Dùng CANCELLED theo constraint hiện hữu, không tạo StartedAt/EndedAt giả. Audit AUTO_CANCEL_EXPIRED phân biệt với hủy thủ công.
- Lịch hiển thị “Đã kết thúc · chưa vào ca”, không tính vào sức chứa và không cung cấp thao tác bắt đầu/hủy.
- Ngừng cấu hình ca đối soát phân công hết hạn trước khi kiểm tra blocker. Phân công tương lai vẫn chặn.
- Không thay đổi tồn kho hoặc tự xác nhận bàn giao vật tư.

## Kiểm chứng

- Sao lưu COPY_ONLY, CHECKSUM và RESTORE VERIFYONLY cả hai database local trước cập nhật.
- Release build: 0 lỗi, 0 cảnh báo.
- Test: 158 passed, 0 failed, 3 skipped (account SQL integration, recurrence worker, maintenance worker chưa có cấu hình chạy).
- Frontend TypeScript/Vite build passed; hai cảnh báo PURE annotation của SignalR.
- Trước triển khai: mỗi database local có 31 PLANNED quá hạn, gồm 25 phân công ngày cũ.
- Sau triển khai: mỗi database đóng 31; còn 0 quá hạn, 0 chấm công giả hoặc audit trùng. Phân công còn trong khoảng hợp lệ: Development 205, Production 199.
- Chưa kiểm thử thao tác ngừng cấu hình ca qua phiên người dùng thực tế. Kiểm tra database xác nhận các phân công cũ không còn PLANNED/WORKING; phân công tương lai có thể vẫn chặn theo quy tắc.

## File thay đổi trong lượt này

- src/HotelOps.Api/Compact/Services/ShiftExpiryService.cs
- src/HotelOps.Api/Compact/Services/WorkdayService.cs
- src/HotelOps.Api/Compact/Services/WorkdayService.Scheduling.cs
- tests/HotelOps.Compact.Tests/ShiftExpiryTests.cs
- frontend/src/compact/workday.tsx
- docs/ui/compact-ux.md
- scripts/local/verify-shift-expiry.ps1

## Yêu cầu dọn dữ liệu đang chờ xác định phạm vi

Owner yêu cầu xóa bản ghi test và đưa vào database chính thức. Chưa xóa hoặc chuyển dữ liệu. Cần xác nhận database đích và giữ giao dịch thực tế hay khởi tạo sạch giao dịch; đây là thay đổi có khả năng mất lịch sử/số dư.

Kiểm kê chỉ đọc: HotelOpsCompactDB có 49 bảng; HotelOpsCompactWorkdayTestLocalDevV2261125 và HotelOpsCompactWorkdayTestLocalProductionV2261125 có 57 bảng. Không thể đổi connection sang database 49 bảng và coi đã tương thích bản V2 hiện tại. Chưa nâng cấp hoặc ghi đè database chính.
