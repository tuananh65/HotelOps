# Rà soát cảnh báo hạn dùng

Phạm vi: `LowStockService`, `ExpiryWarningPolicy`, `InventoryLifecycleService`, `NotificationService`; đối chiếu luồng gọi từ chứng từ/worker, `Access`, entity/mapping lô và thông báo, cùng `docs/restructure-v2/FIFO_FEFO_DESIGN.md`. Rà soát chỉ đọc mã nguồn, không thay đổi DB hay chạy kiểm thử ghi dữ liệu.

## Phát hiện cần sửa

1. **P1 — Luồng ghi sổ dùng ngưỡng mặc định, có thể đóng sai cảnh báo của worker.** `src/HotelOps.Api/Compact/Services/DocumentService.cs:256` và `DocumentService.Supplies.cs:43` khởi tạo `new LowStockService(db,notifications,clock)` không truyền cấu hình. Trong khi worker lấy service từ DI, `ExpiryWarningPolicy.cs:7`–9 dùng cấu hình nếu được cấp và mặc định 30 khi thiếu. Ví dụ cấu hình chi nhánh 60 ngày, lô còn 45 ngày: worker tạo cảnh báo; ghi sổ bất kỳ chứng từ cùng chi nhánh đối soát với 30 ngày rồi đặt `ExpiresAt` tại `LowStockService.cs:70`. Worker lần sau không khôi phục do lỗi 2. Với cấu hình 7 ngày, posting còn tạo cảnh báo quá sớm cho lô còn 20 ngày. **Đề nghị:** cấp cùng một service/cấu hình cho mọi đường gọi; kiểm thử xen kẽ worker và posting với ngưỡng 7/60 ngày và override theo chi nhánh. `InventoryLifecycleService.Batches` dùng DI nên hiện cũng có thể hiển thị cờ khác cảnh báo tạo từ posting.

2. **P2 — Cảnh báo đã đóng bị dedup vĩnh viễn khi trở lại cùng điều kiện.** `LowStockService.cs:64` tạo khóa cố định từ batch/date/expired; dòng 70 đóng cảnh báo không còn liên quan; dòng 124 trả về nếu đã có bất kỳ bản ghi cùng recipient/key, kể cả bản ghi có `ExpiresAt` trong quá khứ. Vì vậy khi cùng lô/hạn/giai đoạn quay lại vùng cảnh báo (ví dụ đổi ngưỡng 60 → 30 → 60, hoặc chỉnh metadata của lô chưa xuất từ hạn A → ngoài ngưỡng → hạn A), không còn cảnh báo hiển thị. Người nhận mới vẫn nhận được trong khi người nhận cũ không nhận. **Đề nghị:** định nghĩa tái kích hoạt bản ghi cũ khi điều kiện trở lại, cập nhật `ExpiresAt` và trạng thái đọc/giao theo quyết định nghiệp vụ; hoặc dùng episode mới. Không chỉ bỏ điều kiện dedup: mapping `HotelOpsCompactDbContext.cs:816` có unique `(RecipientUserId, EventKey)`. Bổ sung test đóng → tái xuất hiện, gồm cảnh báo đã đọc và chưa đọc.

## Các điểm đã đối chiếu

- Truy vấn lô và truy vấn đóng cảnh báo cùng giới hạn `branchId`; khóa chứa BatchId là khóa chính toàn bảng. Không thấy đường rò thông báo sang chi nhánh khác trong phần mới.
- Người nhận được lọc role STOREKEEPER, permission INVENTORY_VIEW, trạng thái tài khoản/lao động, thời hạn role và BRANCH_ACCESS. Inbox/unread/realtime lọc người nhận và kiểm lại role/quyền bằng `Access`, gồm session và chi nhánh đang hoạt động. Thu hồi role/quyền khiến thông báo không còn hiển thị.
- Đối soát lặp khi điều kiện không đổi có transaction application lock, dedup DB và dedup local; không thấy tạo trùng trong đường này. Người nhận mới được bổ sung cho cảnh báo đang tồn tại.
- Ngày Việt Nam và biên `expiresOn == today` nhất quán giữa danh sách lô và cảnh báo. Chuyển sang quá hạn đổi khóa và đóng cảnh báo sắp hết hạn trước đó. Lượng tồn dương ở nhân viên vẫn giữ cảnh báo dù kho hết tồn.
- Đã đọc entity `StockBatch`, `StaffBatchBalance`, `Notification`, mapping lifecycle/notification và SQL DDL liên quan. Chưa xác minh schema thực tế của DB.

## Kiểm chứng và giới hạn

- Chỉ tạo báo cáo này; không sửa source, cấu hình hay DB.
- Build: **Not Run** — review chỉ đọc, không sửa C#.
- Test runtime/API/DB: **Not Run** — không được chạy test gây ghi dữ liệu trong nhiệm vụ này.
- `node --check scripts/verification/v2-inventory-runtime.mjs`: **Passed** (exit 0), chỉ kiểm tra cú pháp.
- Test hiện tại ở `v2-inventory-runtime.mjs:154` có tên “persisted once” nhưng chỉ kiểm tra inbox không còn cảnh báo sau hủy và người ở chi nhánh khác không nhìn thấy; không chứng minh tạo đúng một bản ghi trước khi đóng, ngưỡng tùy chỉnh, tái kích hoạt hoặc chuyển ngày. Không dùng tên test làm bằng chứng PASS cho các tình huống đó.
- Các kết luận là bằng chứng tĩnh của snapshot đang rà soát; cần kiểm thử runtime sau khi sửa hai lỗi trên. Tác nhân khác có thể đang cập nhật cùng workspace.

Câu hỏi còn mở: Khi cảnh báo tái xuất hiện, cần đặt lại trạng thái chưa đọc hay chỉ mở lại cảnh báo cũ? Cần quyết định trước khi chốt semantics tái kích hoạt.
