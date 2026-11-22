# Dữ liệu kiểm thử Compact v2

Đã seed `HotelOpsCompactDB` ngày 21/11/2026 theo yêu cầu. Vẫn giữ **37 bảng**; không thay đổi schema, không chuyển dữ liệu hay ghi vào `HotelOpsDB` cũ. Dữ liệu, tên người và nhà cung cấp đều là giả lập.

## Đăng nhập

Mật khẩu được sinh ngẫu nhiên khi seed, dùng chung cho các tài khoản demo, lưu tại file local **`artifacts/compact-v2/seed/HotelOpsCompactDB/credentials.json`**. File này và checkpoint/log thuộc `artifacts/`, được Git bỏ qua; không đưa vào commit. Script không đặt lại mật khẩu khi chạy lại.

Gọi `POST /api/v2/auth/login` với `username` và `password`, sau đó dùng `Authorization: Bearer <accessToken>`. Có thể bắt đầu bằng `demo_manager` để xem vận hành Sài Gòn. `demo_admin` dành cho quản trị danh mục/tài khoản, không có quyền vận hành ngầm định.

| Tài khoản | Vai trò | Chi nhánh |
|---|---|---|
| `demo_admin` | Quản trị tài khoản, quyền và danh mục dùng chung | Toàn hệ thống |
| `demo_manager` | Giám sát, phân công, kiểm tra phòng, nghiệm thu, báo cáo | Sài Gòn |
| `demo_reception` | Lễ tân, nhận/trả/chuyển phòng, báo sự cố | Sài Gòn |
| `demo_hk1`, `demo_hk2` | Buồng phòng, thực hiện việc, yêu cầu vật tư | Sài Gòn |
| `demo_tech` | Kỹ thuật, bảo trì, xử lý yêu cầu nội bộ | Sài Gòn |
| `demo_store` | Thủ kho, nhập/xuất/chuyển/kiểm kê vật tư | Sài Gòn |
| `demo_head` | Trưởng bộ phận, duyệt yêu cầu vật tư | Sài Gòn |
| `demo_auditor` | Chỉ đọc vận hành, báo cáo, audit | Sài Gòn |
| `demo_dn_manager` | Giám sát vận hành | Đà Nẵng |
| `demo_dn_hk` | Nhân viên buồng phòng | Đà Nẵng |

Vai trò mẫu phục vụ kiểm thử, không phải cấu hình phân quyền production đã được phê duyệt. Người kiểm tra phòng khác người dọn; người duyệt vật tư khác người yêu cầu và người xuất kho. Tài khoản theo chi nhánh bị từ chối khi truy cập chi nhánh còn lại.

## Dữ liệu đã có

| Nhóm | Số lượng sau seed |
|---|---:|
| Chi nhánh / bộ phận / tài khoản | 2 / 8 / 11 |
| Phòng Sài Gòn / Đà Nẵng | 12 / 4 |
| Tài sản / kho / vật tư / chính sách tồn | 6 / 3 / 6 / 6 |
| Checklist kiểm tra / mục checklist / lịch định kỳ | 2 / 6 / 2 |
| Công việc / chứng từ / bút toán kho | 28 / 10 / 7 |
| Lịch sử công việc / khoảng thời gian | 130 / 21 |
| Thông báo / audit | 59 / 240 |

Các ID được SQL Server sinh; không giả định bắt đầu từ 1. Dùng mã chi nhánh `DEMO-SG`, `DEMO-DN`, mã phòng và tiêu đề `[DEMO]` để tìm. `state.json` lưu ID trả về của từng bước API. Số liệu có thể thay đổi khi người kiểm thử thao tác hoặc worker chạy.

Các phòng Sài Gòn tiêu biểu:

| Phòng | Tình huống lúc seed hoàn tất |
|---|---|
| 101 | Đã dọn và kiểm tra độc lập đạt; sẵn sàng nhận khách |
| 102 | Đang có khách sau khi nhận phòng hợp lệ |
| 103 | Đã trả phòng, tăng chu kỳ phòng, cần dọn lại |
| 104 | Dọn xong, chờ kiểm tra |
| 105 | Kiểm tra không đạt, có lỗi ga giường và việc làm lại đã nhận |
| 106 | Đang dọn, có bộ đếm thời gian mở cho `demo_hk1` |
| 107 | Đã phân công dọn cho `demo_hk2` |
| 110 | Bị khóa do rò nước, có bảo trì liên kết sự cố đang xử lý |

Đà Nẵng có phòng 101 sẵn sàng và ba phòng chưa dọn. Có công việc chưa phân công, tạm dừng, đã hủy, chờ xác nhận, đã đóng, phụ thuộc công việc và bàn giao đã được người nhận xác nhận. Hai yêu cầu nội bộ gồm một việc đã xử lý và được người yêu cầu xác nhận, một việc mới gửi.

Kho có phiếu nhập đã ghi sổ, yêu cầu vật tư nháp/đã gửi/đã duyệt/bị từ chối/xuất một phần, đề nghị bổ sung và phiếu kiểm kê đang thực hiện. Kho tổng có 96 đơn vị xà phòng sau nhập 100, xuất 4; yêu cầu 10 còn thiếu 6. Găng tay có 5 dưới mức tối thiểu 20; dung dịch vệ sinh chưa có tồn. Kho tầng có 10 xà phòng. Kho Đà Nẵng để trống nhằm kiểm thử kết quả không có dữ liệu.

Ba việc còn mở bộ đếm cho `demo_hk1`, `demo_hk2`, `demo_tech`. Cần tạm dừng/hoàn tất việc hiện tại trước khi bắt đầu việc khác của cùng người. Thời lượng và năng suất chỉ là dữ liệu giả lập. Worker được tắt trong lúc seed; hai lịch mẫu đến hạn vào ngày sau thời điểm tạo và sẽ chạy theo cấu hình worker của backend khi khởi động thông thường.

## Chạy lại và kiểm chứng

Từ thư mục repo, sau khi build backend:

```powershell
& scripts/database/compact-v2/Seed-Testing.ps1
# Có thể chỉ rõ -Server, -Database HotelOpsCompact... và -Port nếu cần.
```

Lần đầu script chỉ nhận database v2 rỗng và tên bắt đầu `HotelOpsCompact`. Nó bootstrap quản trị, tạo danh mục phân quyền mẫu trong một transaction, rồi tạo nghiệp vụ qua API v2 với worker tắt. Script dùng SQL application lock để chặn hai lượt seed đồng thời, từ chối cổng đang được dùng và chỉ dừng API do chính nó tạo.

Mỗi thao tác được lưu checkpoint tại `artifacts/compact-v2/seed/<database>/state.json`. Khi hoàn tất, chạy lại chỉ kiểm tra catalog/DBCC và thống kê, không chèn thêm hay phục hồi dữ liệu đã được người kiểm thử sửa. Không xóa checkpoint để ép chạy lại. Nếu có `pending` sau mất kết nối, script dừng để tránh ghi trùng; cần đối chiếu log, audit và kết quả trong database trước khi phục hồi checkpoint. Mất checkpoint trên database đã có dữ liệu cũng khiến script từ chối seed.

Kết quả local trong cùng thư mục:

- `api-verification.json`: **17 kiểm tra API đạt**, gồm đăng nhập 11 tài khoản, quyền chi nhánh, tài khoản chỉ đọc, phòng, dashboard, tồn kho, xuất một phần, thông báo và thời gian.
- `verification.json`: thống kê và kiểm tra SQL, **37 bảng / 152 FK / 77 CHECK / 26 trigger / 6 view**, DBCC CHECKDB và CHECKCONSTRAINTS không báo lỗi.
- `idempotency-verification.json`: đối chiếu số hàng của toàn bộ 37 bảng trước/sau chạy lại script.

Script không sửa backend trong lượt seed này; đã kiểm chứng trực tiếp bằng bản build Compact v2 đang có. Đây là bộ dữ liệu cho kiểm thử chức năng, không phải kiểm thử tải hay chứng nhận toàn bộ nghiệp vụ. `Test-Compact.ps1` yêu cầu database rỗng nên từ nay phải chạy trên database test độc lập, không chạy trên database chính đã seed.
