# Ánh xạ danh mục đồ fill P0

Ngày kiểm tra: 24/11/2026, Asia/Saigon. Chỉ database `HotelOpsCompactWorkdayTestP0261124`. Đọc thực tế qua kết nối User Secrets, không xuất thông tin đăng nhập.

## Phạm vi

Script `scripts/database/compact-v2/p0-supply-defaults.sql` chỉ bổ sung metadata. Thứ tự: role migration → supply schema → store policy → supply defaults. Integration Owner thực hiện rollback test, retry test và commit TEST; worker chưa áp dụng script.

Không tạo receipt, stock movement, tồn kho, ledger nhân viên hoặc số lượng giả. Định mức mới: đầu ca 10 đơn vị, minimum stock 50, mỗi khách 1 đơn vị; mỗi chi nhánh một kho đang hoạt động. Stock vẫn theo chứng từ thực tế, nên thiếu hàng sẽ được nghiệp vụ đầu ca báo thiếu.

## Ánh xạ chính xác

| Đồ fill | Danh mục dùng | Đơn vị cơ sở | Bằng chứng |
|---|---|---|---|
| Bàn chải đánh răng | P0-FILL-TOOTHBRUSH, mới | Cái | Chưa có danh mục tương ứng |
| Kem đánh răng | P0-FILL-TOOTHPASTE, mới | Tuýp | Chưa có danh mục tương ứng |
| Dầu gội | DEMO-ITEM-2, có sẵn | DEMO-EA: Cái / chai | Tên chính xác Dầu gội, đơn vị từng chai |
| Sữa tắm | P0-FILL-BODYWASH, mới | Chai | Không đồng nhất Xà phòng với Sữa tắm |
| Nước uống | DEMO-ITEM-4, có sẵn | DEMO-EA: Cái / chai | Tên chính xác Nước uống, đơn vị từng chai |
| Giấy vệ sinh | P0-FILL-TOILETPAPER, mới | Cuộn | Chưa có danh mục tương ứng |
| Khăn tắm | DEMO-ITEM-3, có sẵn | DEMO-EA: Cái / chai | Tên chính xác Khăn tắm, đơn vị từng cái |
| Khăn mặt | P0-FILL-FACETOWEL, mới | Cái | Chưa có danh mục tương ứng |

Đơn vị DEMO-EA được giữ nguyên, không đổi đơn vị của giao dịch lịch sử và không suy ra dung tích ml/lít hoặc hệ số thùng. Các mã P0-EACH/P0-TUBE/P0-BOTTLE/P0-ROLL là đơn vị cơ sở độc lập, không có conversion factor.

Script dừng nếu mã cũ đổi tên/đơn vị/trạng thái, nếu mã P0 xung đột hoặc bị tắt, hoặc có tên tương ứng chưa được ánh xạ. Không tự tạo bản sao khi danh mục cùng tên đã tồn tại.

## Chi nhánh và người đại diện cấu hình

Đọc thực tế sau role/store migration của Integration Owner:

- DVBV: kho DEMO-MAIN hoạt động, DEMO-FLOOR đã tắt. OM gốc user 435; user 431 được chuyển từ Department Head.
- DVCT: kho DEMO-MAIN hoạt động. OM gốc user 433; user 478 được chuyển từ Department Head.

SQL không hard-code ID. Nó yêu cầu đúng một OM gốc đang hoạt động, có role trong thời gian hiệu lực và membership BRANCH_ACCESS còn hiệu lực. Assignment được chuyển từ Head được nhận diện bằng thời điểm Head bị revoke bằng thời điểm OM được assign. Nếu nhiều OM gốc hoặc không có, dừng để ánh xạ có kiểm chứng.

`updated_by` là OM gốc đại diện chi nhánh cho bản cấu hình khởi tạo; audit `P0_SUPPLY_DEFAULTS/BOOTSTRAP_METADATA` ghi đây là bootstrap hệ thống, không giả mạo thao tác bấm lưu của OM.

Không thay đổi bất kỳ cấu hình supply đã có, kể cả đang tắt hoặc có revision lịch sử. Chỉ cấu hình A mới được đồng bộ minimum stock 50 vào inventory policy; policy liên quan cấu hình đã có không bị ghi đè. Chạy lại không thêm revision hay đặt lại tùy chỉnh.

## Nhóm B/C và loại báo cáo

Đọc thực tế có 6 tài sản điều hòa gán phòng, asset type chính xác `Điều hòa` trong `ASSET_TYPE`. Các tài sản này được khởi tạo nhóm C theo bằng chứng loại thiết bị, không dựa trên tên chứa từ khóa. Giữ nguyên mọi cấu hình tài sản đã có.

Không có chứng từ ISSUE đã posted có receiving_room_id trong dữ liệu đọc. Không suy đoán đồ nhóm B, không tự gán đồ mới vào phòng. Xà phòng, Găng tay, Dung dịch vệ sinh chưa được phân loại thêm. Vật dụng lâu bền phải được OM cấu hình từ danh mục thực tế trước khi kiểm thử luồng thiếu đồ.

Đọc thực tế chưa có mã WORK_CATEGORY `ROOM_ISSUE_{branch}_{BROKEN|MISSING}`. Script chỉ thêm mã còn thiếu với tên Hỏng đồ vật/Thiếu đồ vật; không bật lại, đổi tên hoặc ghi đè metadata đã được cấu hình.

## Kiểm chứng

- VERIFIED: truy vấn danh mục, UOM, tài sản/asset type, kho, role assignments trên TEST.
- VERIFIED: SQL Server `SET PARSEONLY ON` chấp nhận cú pháp script trước bản bổ sung guard same-name; cần chạy lại trong validation tích hợp.
- NOT_TESTED: thực thi migration, idempotency runtime, constraints sau insert; Integration Owner chạy trước khi commit.
- Không sửa C#, JS/TS; build/node syntax không áp dụng cho phần SQL này.
