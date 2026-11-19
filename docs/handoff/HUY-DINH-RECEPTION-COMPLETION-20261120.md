# Hoàn tất chức năng Lễ tân — huy.dinh

Ngày kiểm chứng: 20/11/2026. Phạm vi theo các quyết định người dùng đã duyệt trong phiên làm việc; không suy ra quyền từ Permission Matrix còn Pending.

## Chức năng đã triển khai

- Nhận khách: phòng đã sẵn sàng, vệ sinh/kiểm tra đạt, không bị khóa hoặc còn công việc mở → Có khách.
- Trả phòng: Có khách → Chờ vệ sinh; giữ khóa phòng nếu có.
- Chuyển phòng: trong cùng chi nhánh, sang phòng đủ điều kiện nhận khách; nguồn → Chờ vệ sinh, đích → Có khách. Chọn số phòng/tầng/loại phòng và nhập lý do; không nhập ID.
- Kiểm tra phiên, CSRF, quyền RECEPTION theo chi nhánh, ROOM_VIEW và quyền thao tác tương ứng. Kiểm tra phiên bản nguồn/đích, từ chối gửi lặp; lịch sử và audit lưu trong cùng transaction.
- Sau trả/chuyển, nghiệm thu trước lúc trả không làm phòng sẵn sàng. Giám sát tạo chu kỳ mới. Kết luận kiểm tra không ghi đè trạng thái Có khách.
- Lễ tân được báo/xem sự cố của mình, tạo/xem yêu cầu nội bộ của mình. Các panel và liên kết yêu cầu kiểm tra quyền đích trước khi mở.
- Ẩn Công việc buồng phòng, Xử lý công việc làm lại và các liên kết giám sát ngoài phạm vi; API cũng kiểm tra vai trò. Bỏ ô phụ “Gõ để tìm lựa chọn” trong phiên Lễ tân.
- Nút Nhận khách/Trả phòng/Chuyển phòng và xác nhận đặt trong popup chi tiết phòng hiện có, giới hạn trong viewport; đóng form không ghi dữ liệu.

API phòng: GET/POST `.../check-in`, GET/POST `.../check-out`, GET `.../move-options`, POST `.../move`, với tiền tố `/api/branches/{branchId}/rooms/{roomId}`. POST yêu cầu CSRF và If-Match; chuyển phòng thêm targetRoomId, targetVersion, reason. Không trả EF entities trực tiếp.

## Kiểm chứng thực tế

| Hạng mục | Kết quả |
| --- | --- |
| `dotnet build HotelOps.slnx` trên bản đã stage | Passed, 0 warning, 0 error |
| `dotnet test HotelOps.slnx --no-restore` | Passed; solution chưa bao gồm project regression riêng |
| Regression C# liên quan trên bản đã stage | 68 Passed; SQL opt-in ban đầu Skipped rồi chạy riêng 1 Passed |
| SQL readiness trên bản đã stage | 1 Passed, mọi ghi thử rollback; kiểm tra chu kỳ cũ, chu kỳ mới bị hủy, chờ kiểm tra và đạt lại |
| Frontend build trên bản đã stage | Passed |
| Frontend regression | 51 Passed, 0 Failed |
| API và browser thật | 45 bước kiểm tra Passed; 15 trang menu Huy không có alert lỗi/JavaScript error trong lần quét |
| Giao dịch thực tế | Chuyển DEMO-LT-03 → DEMO-LT-02, sau đó trả DEMO-LT-02 thành công qua web |
| Kiểm tra từ chối | Sai vai trò/chi nhánh, thiếu CSRF/If-Match, cùng phòng, thiếu lý do, đích không sẵn sàng, phiên bản cũ, gửi lại và tái sử dụng nghiệm thu cũ đều bị chặn |
| SQL history/audit | Đúng 2 audit chuyển phòng cùng request_id, 1 audit trả phòng, 2 history CHECKOUT; không sinh thêm khi gửi lại |
| Popup | Đã kiểm tra khung 1440×1000, không vượt viewport |

Các lần thử ban đầu phát hiện lỗi DTO validation của request chuyển phòng và nhãn truy cập của dropdown; đã sửa và chạy lại thành công. Không dùng kết quả lần thất bại làm kết quả bàn giao.

## Database và demo

Đã cài các cấu hình đã duyệt trước: IR_VIEW OWN cho RECEPTION, membership bộ phận Lễ tân của Huy, ba bảng liên kết yêu cầu nội bộ. Gói nhận/trả/chuyển không thêm schema, generated entities, migration hoặc mã quyền.

Đã bổ sung DEMO-LT-04 và DEMO-LT-05 qua installer có COPY_ONLY backup, CHECKSUM, RESTORE VERIFYONLY, thử rollback và commit. Các phòng cũ được giữ nguyên. Đối chiếu cuối:

| Phòng | Trạng thái | Có thể demo |
| --- | --- | --- |
| DEMO-LT-01 | Sẵn sàng | Nhận khách |
| DEMO-LT-02 | Chờ vệ sinh | Xem lịch sử chuyển vào và trả phòng |
| DEMO-LT-03 | Chờ vệ sinh | Xem lịch sử chuyển đi |
| DEMO-LT-04 | Sẵn sàng | Nhận khách hoặc làm phòng đích |
| DEMO-LT-05 | Sẵn sàng | Nhận khách hoặc làm phòng đích |

API thật của Huy đã xác nhận cả ba phòng sẵn sàng có `canCheckIn=true`. Đây là dữ liệu mô phỏng có nhãn DEMO, không phải nghiệm thu phòng thật. Đăng nhập Huy, vào Trạng thái phòng và tìm `DEMO-LT` để thử. Sau trả phòng, cần chu kỳ vệ sinh/kiểm tra mới trước khi nhận khách lại.

Hướng dẫn SQL: [scripts/database/development/reception/README.md](../../scripts/database/development/reception/README.md). Log và bằng chứng kiểm chứng lưu cục bộ tại `artifacts/reception-completion/`; không commit cookie, mật khẩu, chuỗi kết nối hoặc backup.

## Giới hạn và cảnh báo

- Frontend có cảnh báo React Router `use client` và chunk lớn hơn 500 kB; build vẫn thành công.
- API local còn cảnh báo EF model 20602 về index RoomTypeBranchV1 và JOB01 worker InvalidOperationException từ cấu hình/module có sẵn. Không sửa ngoài phạm vi để che cảnh báo.
- Chưa chạy tải đồng thời quy mô lớn hoặc chủ động gây lỗi giữa hai lệnh ghi của transaction chuyển phòng. Kiểm thử rollback SQL readiness và kiểm thử từ chối không ghi dữ liệu đã chạy; không coi chúng là kiểm thử fault injection đầy đủ.
- Đặt phòng, thông tin khách, thanh toán, đảo giao dịch và chuyển khác chi nhánh không thuộc phạm vi đã duyệt.
- Thay đổi ở workspace ngoài phạm vi Lễ tân được giữ nguyên và không đưa vào commit này. Build/test bản stage tách biệt đã xác nhận không phụ thuộc các thay đổi đó. Không triển khai production.

## Các file trong gói

Danh sách cụ thể được lưu tại [HUY-DINH-RECEPTION-FILES-20261120.txt](HUY-DINH-RECEPTION-FILES-20261120.txt). Bao gồm controller/service/DTO phòng, điểm kiểm tra quyền liên quan, màn hình/popup/menu, kiểm thử, script SQL và tài liệu. `Program.cs` chỉ thêm đăng ký RoomCheckInService; HomeNavigationService chỉ stage các thay đổi Lễ tân.
