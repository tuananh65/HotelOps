# Chuẩn hóa 8 tầng × 6 phòng mỗi chi nhánh

Người dùng đã chọn tổng cộng 8 tầng mỗi chi nhánh, mỗi tầng 6 phòng và cho phép sắp xếp lại phòng cũ.

## Kết quả

| Chi nhánh | Tầng | Phòng mỗi tầng | Tổng phòng | Phòng thêm |
|---|---:|---:|---:|---:|
| Bùi Viện (DVBV) | 8 | 6 | 48 | 36 |
| Cao Thắng (DVCT) | 8 | 6 | 48 | 44 |

Đã thêm 14 tầng và 80 phòng. Số phòng theo tầng: 101–106, 201–206, …, 801–806. Sáu phòng 107–112 của Bùi Viện chuyển thành 201–206 ở tầng 2; giữ ID phòng 419–424, loại phòng, bộ phận, trạng thái, chu kỳ dọn phòng và ngày tạo. Các liên kết lịch sử/công việc qua ID phòng được giữ nguyên. Ba công việc 634, 642, 649 vẫn liên kết đúng phòng và giữ trạng thái ASSIGNED, OPEN, IN_PROGRESS.

Phòng bổ sung dùng loại Standard và bộ phận HK đang có tại từng chi nhánh, trạng thái DIRTY theo quy tắc tạo phòng hiện hành. Các phòng Suite cũ vẫn giữ loại Suite.

## An toàn và kiểm chứng

- Backup COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY thành công trước khi ghi; thông tin bản sao tại `artifacts/hotel-floors/backup.json`.
- Chạy dry-run trước khi áp dụng; toàn bộ thay đổi dữ liệu và 100 bản ghi audit thực hiện trong một transaction.
- SQL sau commit xác nhận 16 tầng, 96 phòng, mỗi tầng đúng 6 phòng, không trùng mã trong chi nhánh. DBCC CHECKCONSTRAINTS trên locations/rooms không trả lỗi.
- Giữ 40 bảng, không thay schema, quyền hoặc database legacy.
- Chạy dry-run lần nữa: mỗi chi nhánh đều có 0 tầng cần thêm, 0 phòng cần thêm, 0 phòng cần sắp xếp.
- API thật qua frontend 5186: đăng nhập Admin thành công, GET danh mục tầng và phòng trả HTTP 200; kiểm tra đủ 8 tầng và 48 phòng mỗi chi nhánh, mã từng tầng đúng 6 phòng.
- Build: **Not Run** vì không sửa C#/frontend. Kiểm thử dữ liệu SQL và API: **Passed**. Không kiểm thử browser trong lượt này.

## File

- `scripts/database/compact-v2/normalize-hotel-floors.sql`: kiểm tra dữ liệu đầu vào, dry-run, cập nhật nguyên tử, bảo toàn phòng cũ, audit và kiểm tra kết quả.
- `scripts/database/compact-v2/Normalize-HotelFloors.ps1`: đọc kết nối từ User Secrets, yêu cầu backup mới trước Apply và lưu kết quả.
- Bằng chứng: `artifacts/hotel-floors/before-plan.json`, `applied.json`, `dry-run.json`, `verification.json`, `api-verification.json`, `backup.json`.

Không có lỗi còn tồn tại trong các kiểm tra đã chạy. Không commit/push.
