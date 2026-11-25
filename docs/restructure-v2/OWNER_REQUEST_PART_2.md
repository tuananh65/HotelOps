- Có thể cần giặt là hoặc vệ sinh.
- Chỉ ghi giảm tổng sở hữu khi có nghiệp vụ ghi giảm hợp lệ.

### GROUP C — STOCKED DURABLE ITEMS

Vật dụng cứng không phải thiết bị điện tử dự phòng, nếu khách sạn thực tế lưu kho:

- Móc treo quần áo.
- Thùng rác.
- Các vật dụng lâu bền phù hợp.

Theo dõi cấp phát đến phòng hoặc luân chuyển.

Không dùng phiếu xuất tiêu hao nếu vật dụng vẫn thuộc sở hữu khách sạn.

### GROUP D — EQUIPMENT / ASSETS

Thiết bị và tài sản:

- Điều hòa.
- Tivi.
- Bình nóng lạnh.
- Thiết bị an ninh.
- Thiết bị báo cháy.
- Máy sấy tóc.
- Các thiết bị điện tử và điện gia dụng phù hợp.

Không quản lý các thiết bị này như hàng tiêu hao dự phòng trong kho.

Nếu hỏng:

- Sửa chữa.
- Kiểm tra.
- Đề xuất thay mới nếu không thể sửa.

Theo dõi thông qua Asset Management và Maintenance.

### Phân loại phải cấu hình được

Không hardcode category bằng tên sản phẩm.

Cùng một tên thương mại có thể có phiên bản dùng một lần hoặc tái sử dụng.

Ví dụ:

- Dép dùng một lần: Consumable.
- Dép tái sử dụng: Reusable.
- Khăn dùng một lần: Consumable.
- Khăn vải: Reusable.

Không thay đổi category master của toàn bộ mặt hàng chỉ vì một đơn vị vật tư bị hỏng.

Vật tư tái sử dụng bị hỏng được xử lý thông qua nghiệp vụ chuyển trạng thái và ghi giảm, không nhất thiết đổi loại hàng thành Consumable.

---

## 13. ROOM SUPPLY STANDARD & CONFIGURATION

Giữ chức năng OM cấu hình định mức theo từng khách sạn.

Thông tin:

- Branch.
- Item.
- Item category.
- Room type nếu có.
- Quantity per guest.
- Fixed quantity per room nếu phù hợp.
- Initial shift allocation.
- Minimum stock.
- Expiry warning threshold nếu áp dụng.
- Active status.

Giữ định mức mặc định từ các quyết định trước:

- Cấp đầu ca: 10 đơn vị mỗi loại đồ fill được cấu hình.
- Tồn tối thiểu: 50 đối với đồ fill.

Tuy nhiên, khăn tắm và khăn mặt phải được quản lý theo vòng đời vật tư tái sử dụng nếu là khăn vải.

Không ghi giảm tổng sở hữu khăn khi cấp đầu ca hoặc đưa vào phòng.

Không hardcode 10, 50 hoặc định mức phòng trong service.

Các giá trị phải cấu hình được theo branch.

Không tự áp dụng cùng một định mức cho tất cả mặt hàng và phòng.

---

## 14. START SHIFT — SUPPLY ALLOCATION V2

Giữ nghiệp vụ cấp đồ đầu ca cho HK Staff nếu đã triển khai hoặc còn thuộc yêu cầu hiện hành.

Khi HK Staff bắt đầu ca:

1. Kiểm tra Workday Session.
2. Đọc định mức.
3. Kiểm tra vật tư khả dụng.
4. Lựa chọn lô hợp lệ.
5. Chuyển vật tư từ kho sang nhân viên.
6. Ghi nhận số lượng theo Item, Batch và Shift.
7. Tạo lịch sử cấp phát.
8. Thông báo Storekeeper.

### Đối với Consumables

Giảm tồn khả dụng kho khi chuyển cho nhân viên.

Theo dõi lượng nhân viên đang giữ.

Không trừ kho lần hai khi đồ được đưa vào phòng.

### Đối với Reusable Items

Chuyển:

Warehouse Ready → Staff Holding.

Không giảm tổng sở hữu chi nhánh.

### Nếu kho thiếu

- Không xuất âm.
- Hiển thị lượng thực cấp.
- Hiển thị phần thiếu.
- Thông báo HK Supervisor và Storekeeper.
- Không phân công công việc khi không đáp ứng nhu cầu vật tư.

Giữ phương án cho bắt đầu ca với lượng thực cấp là policy đề xuất cần đối chiếu với quyết định hiện hành.

Một ca chỉ nhận cấp phát đầu ca một lần.

Không cấp trùng khi retry.

---

## 15. ROOM SUPPLY ASSESSMENT — KIỂM TRA ĐỒ HIỆN CÓ TRONG PHÒNG

Đây là thay đổi bắt buộc so với công thức cũ.

Không mặc định mỗi phòng sức chứa 3 khách phải trừ 3 đơn vị của tất cả vật tư.

Trước khi xác nhận bổ sung, HK Staff phải kiểm tra số lượng thực tế trong phòng.

Mỗi mặt hàng hiển thị:

- Tên vật tư.
- Định mức phòng.
- Số lượng hiện có.
- Số lượng còn sử dụng được.
- Số lượng không đạt hoặc hết hạn nếu có.
- Số lượng còn thiếu.
- Số lượng bổ sung.
- Ghi chú.

### Công thức

Target Quantity:

Room Maximum Guest Capacity × Quantity Per Guest

Hoặc định mức theo Room Type/Fixed Quantity nếu đã cấu hình.

Missing Quantity:

MAX(0, Target Quantity - Usable Existing Quantity).

Ví dụ:

Phòng sức chứa 3 khách.

Định mức bàn chải: 3.

Trong phòng có 1 bàn chải còn nguyên vẹn, đủ điều kiện sử dụng.

Số cần bổ sung: 2.

Không trừ 3.

### Vật tư không đạt

Không tính vào Usable Existing Quantity đối với:

- Đã sử dụng.
- Hết hạn.
- Hư hỏng.
- Không còn hợp vệ sinh.
- Không đủ điều kiện cấp cho khách.

Vật tư hết hạn cần ghi nhận xử lý riêng, không tự biến mất khỏi sổ kho.

### Validation

Backend phải:

- Tính lại nhu cầu.
- Kiểm tra định mức.
- Kiểm tra số lượng hợp lệ.
- Kiểm tra assignment.
- Kiểm tra vật tư nhân viên đang giữ.
- Chống cập nhật trùng.

Không tin số lượng cần bổ sung do frontend tự tính mà không xác minh.

---

## 16. CLEANING ASSIGNMENT & RESERVATION V2

Giữ quy tắc:

HK Supervisor không được phân công phòng cho nhân viên không đủ vật tư cần thiết.

Tuy nhiên cần thay đổi cách tính reservation.

### Trường hợp đã kiểm tra phòng

Sử dụng số lượng thực tế còn thiếu.

### Trường hợp chưa kiểm tra phòng

Có hai phương án:

A. HK Supervisor kiểm tra trước nhu cầu vật tư rồi giao việc.

B. Giữ chỗ tạm thời theo định mức tối đa, sau đó điều chỉnh reservation khi HK Staff kiểm tra phòng.

Ưu tiên phương án tương thích source hiện tại và ghi Decision Register.

Không giả định lượng vật tư thiếu bằng 0 khi chưa kiểm tra.

### Khi HK Staff kiểm tra thực tế

- Cập nhật nhu cầu.
- Giảm phần reservation không cần thiết.
- Giữ chỗ lượng bổ sung thực tế.
- Không tiêu hao phần đã giải phóng.
- Nếu phát hiện thiếu thêm và nhân viên không đủ đồ, chuyển yêu cầu bổ sung.

### Khi hoàn thành dọn phòng

Chỉ ghi nhận số lượng thực tế đã cấp vào phòng.

Không ghi nhận toàn bộ reservation là tiêu hao.

Không trừ vật tư hai lần khi Rework hoặc Inspection retry.

---

## 17. FIFO / FEFO & EXPIRY MANAGEMENT

Bổ sung quản lý vật tư theo lô nhập.

### Stock Batch

Tối thiểu có:

- Batch ID.
- Item ID.
- Branch/Warehouse ID.
- Batch/Lot Number.
- Received Date.
- Expiry Date nếu áp dụng.
- Quantity Received.
- Quantity Available.
- Reserved Quantity.
- Status.
- Stock Receipt Reference.

### FEFO — First Expired First Out

Đối với vật tư có hạn sử dụng:

Ưu tiên lô có hạn sử dụng gần nhất nhưng vẫn đủ điều kiện cấp phát.

Không được cấp lô hết hạn.

Nếu hai lô có cùng Expiry Date, ưu tiên lô nhập trước.

### FIFO — First In First Out

Đối với vật tư không quản lý hạn sử dụng:

Ưu tiên lô nhập trước.

### Quy tắc

- Không tự động coi vật tư thiếu Expiry Date là không hết hạn nếu loại hàng bắt buộc quản lý hạn.
- Không cấp vật tư hết hạn.
- Không chuyển vật tư hết hạn sang chi nhánh khác để sử dụng.
- Không tự sửa Expiry Date để làm hàng có thể cấp.
- Không xuất vượt tồn từng lô.
- Mọi giao dịch lưu Batch Reference.
- Cấp đầu ca và cấp bổ sung đều phải tuân thủ FIFO/FEFO.
- Hoàn trả cuối ca giữ nguyên nguồn gốc lô.
- Hàng hoàn trả không còn đủ điều kiện sử dụng phải đưa vào trạng thái kiểm tra/cách ly phù hợp.

### Ví dụ

Lô A: 30 chai nước, hạn 30/12.

Lô B: 20 chai nước, hạn 15/12.

Lô C: 50 chai nước, hạn 30/01 năm sau.

Nếu cần 25 chai và tất cả lô còn hợp lệ:

Xuất 20 từ lô B và 5 từ lô A.

Không chọn lô C chỉ vì số lượng lớn hơn.

### Dữ liệu migration

Nếu tồn kho hiện tại không có thông tin lô:

- Không tự bịa hạn sử dụng.
- Tạo mapping tồn legacy có thể đối soát.
- Đánh dấu dữ liệu cần kiểm tra bổ sung.
- Không cho cấp hàng bắt buộc hạn sử dụng khi chưa xác định đủ dữ liệu an toàn.

### Cảnh báo

Storekeeper được xem:

- Sắp hết hạn.
- Đã hết hạn.
- Lượng hàng bị cách ly.
- Lượng có thể cấp.
- Lịch sử xuất theo lô.

---

## 18. CONSUMABLE ISSUE DOCUMENT

Chỉ tạo phiếu xuất vật tư tiêu hao đối với Consumables.

Phiếu phải có:

- Mã phiếu.
- Chi nhánh.
- Kho.
- Người nhận.
- Mục đích.
- Item.
- Batch.
- Số lượng.
- Đơn vị.
- Thời gian.
- Người xác nhận.
- Giao dịch liên quan.

Phân biệt các loại giao dịch:

- Cấp vật tư tiêu hao cho nhân viên.
- Cấp bổ sung tiêu hao.
- Hoàn trả tiêu hao chưa sử dụng.
- Ghi nhận vật tư đã cấp vào phòng.
- Xử lý hàng hết hạn hoặc hư hỏng.

Không sử dụng phiếu xuất tiêu hao cho:

- Chăn tái sử dụng.
- Khăn vải.
- Gối.
- Thảm.
- Ga giường.
- Thiết bị điện tử.
- Vật dụng cứng vẫn được quản lý theo vòng đời.

Đối với vật tư tái sử dụng, sử dụng chứng từ luân chuyển, bàn giao hoặc thay thế theo domain phù hợp.

Không loại bỏ chứng từ lịch sử chỉ để giảm số lượng loại phiếu.

---

## 19. REUSABLE ITEM INVENTORY

Quản lý vật tư tái sử dụng theo trạng thái và vị trí.

Các trạng thái logic:

- READY: Sẵn sàng cấp.
- STAFF_HOLDING: Nhân viên đang giữ.
- IN_USE: Đang sử dụng tại phòng.
- DIRTY: Cần vệ sinh.
- LAUNDRY: Đang/cần giặt là.
- DAMAGED: Hư hỏng, chờ đánh giá.
- IN_TRANSIT: Đang điều chuyển.
- WRITTEN_OFF: Đã ghi giảm.

Tên trạng thái chỉ là định hướng. Ánh xạ vào schema hiện tại nếu phù hợp.

### Inventory balances

Phải phân biệt:

1. Tổng số lượng thuộc sở hữu chi nhánh.
2. Số lượng sẵn sàng tại kho.
3. Số lượng nhân viên đang giữ.
4. Số lượng tại phòng.
5. Số lượng cần giặt.
6. Số lượng hỏng chờ đánh giá.
7. Số lượng đang điều chuyển.
8. Số lượng đã ghi giảm.

Không tính vật tư đã ghi giảm vào tổng sở hữu hiện tại.

### Ví dụ

Tổng chăn = 100.

Ready = 80.

Laundry = 20.

Cấp 5 chăn vào phòng:

Ready = 75.

In Use = 5.

Laundry = 20.

Total Owned = 100.

Không giảm Total Owned thành 95.

### Các nghiệp vụ cần có

- Cấp vật tư vào phòng.
- Thu hồi vật tư đã dùng.
- Chuyển sang cần giặt.
- Xác nhận giặt xong.
- Chuyển về sẵn sàng.
- Báo hư hỏng.
- Đánh giá khả năng phục hồi.
- Ghi giảm nếu không thể sử dụng.

Không cần xây dựng module quản lý nhà giặt hoặc hợp đồng giặt là.

Chỉ quản lý trạng thái và số lượng.

---

## 20. REUSABLE ITEM REPLACEMENT

Khi HK Staff cần thay khăn, chăn, gối hoặc ga giường:

1. Kiểm tra vật tư hiện có tại phòng.
2. Xác định loại cần thay.
3. Ghi số lượng.
4. Kiểm tra vật tư sạch có sẵn.
5. Cấp vật tư sạch.
6. Thu hồi vật tư đã sử dụng.
7. Chuyển vật tư cũ sang trạng thái phù hợp.
8. Ghi nhận lịch sử.

Ví dụ:

Phòng cần thay 2 khăn tắm.

- 2 khăn sạch chuyển từ READY hoặc STAFF_HOLDING sang IN_USE.
- 2 khăn cũ chuyển từ IN_USE sang DIRTY/LAUNDRY.

Tổng sở hữu không thay đổi.

Không ghi nhận 2 khăn sạch là vật tư bị tiêu hao.

Nếu khăn cũ bị mất hoặc hỏng, sử dụng quy trình báo thiếu/hỏng và ghi giảm riêng.

Không tự coi vật tư chưa thu hồi được là đã bị tiêu hủy.

---

## 21. DAMAGED ITEM & WRITE-OFF

Khi vật tư tái sử dụng bị hỏng:

- Chuyển trạng thái DAMAGED.
- Ghi lý do.
- Ghi người phát hiện.
- Ghi kết quả đánh giá.
- Xác định có thể phục hồi hay không.

Nếu có thể phục hồi:

- Thực hiện sửa chữa/vệ sinh.
- Kiểm tra.
- Chuyển về READY khi đủ điều kiện.

Nếu không thể phục hồi:

- Tạo yêu cầu ghi giảm.
- Xác nhận bởi người có thẩm quyền.
- Giảm tổng sở hữu.
- Tạo chứng từ.
- Giữ lịch sử.

Không ghi giảm chỉ vì vật tư đang cần giặt.

Không đổi toàn bộ Item Master từ Reusable thành Consumable.

Một đơn vị vật tư bị hỏng không làm thay đổi phân loại của mọi đơn vị còn lại.

Nếu quyền ghi giảm chưa có contract rõ ràng, đề xuất Storekeeper tạo ghi nhận và OM xác nhận theo phạm vi quản lý tài sản/kho; ghi vào Decision Register trước khi khóa policy.

---

## 22. CONSUMABLE REFILL REQUEST

Giữ workflow từ các quyết định trước:

HK Staff → HK Supervisor → Storekeeper.

### HK Staff

Khi thiếu vật tư:

- Tạo yêu cầu bổ sung.
- Chọn mặt hàng.
- Nhập số lượng.
- Hiển thị lượng đang giữ.
- Hiển thị nhu cầu còn thiếu.
- Liên kết ca hiện tại.

### HK Supervisor

Kiểm tra nhu cầu.

Xác nhận hoặc từ chối.

Từ chối bắt buộc lý do.

Nếu xác nhận, hệ thống tự chuyển người nhận sang Storekeeper.

Không tạo yêu cầu mất liên kết với yêu cầu gốc.

### Storekeeper

Kiểm tra tồn khả dụng, lô và hạn sử dụng.

Nếu đủ:

- Xác nhận cấp phát.
- Chọn lô theo FIFO/FEFO.
- Tạo phiếu xuất tiêu hao.
- Cập nhật vật tư nhân viên.

Nếu thiếu:

- Hiển thị lượng thiếu.
- Không xuất âm.
- Từ chối kèm lý do.

OM không duyệt trung gian workflow này.

Đối với vật tư tái sử dụng, sử dụng workflow cấp vật tư thay thế và chứng từ luân chuyển tương ứng, không dùng phiếu xuất tiêu hao.

---

## 23. END SHIFT — AUTO RETURN V2

Khi HK Staff kết thúc ca:

- Kiểm tra công việc còn mở.
- Kiểm tra reservation.
- Kiểm tra yêu cầu cấp bổ sung chưa xử lý.
- Xác định lượng vật tư còn giữ.
- Hoàn trả vật tư đủ điều kiện.
- Ghi nhận vật tư cần giặt hoặc bị hỏng nếu có.
- Đóng allocation.
- Đóng Workday Session.
- Gửi Notification cho Storekeeper.

### Consumables

- Hoàn trả đúng số lượng chưa sử dụng.
- Giữ Batch và Expiry Date.
- Chỉ đưa về tồn khả dụng khi còn đủ điều kiện.
- Không hoàn trả đồ đã cấp vào phòng.

### Reusable Items

- Đồ sạch chưa sử dụng: chuyển về READY.
- Đồ đã sử dụng/cần giặt: chuyển DIRTY/LAUNDRY.
- Đồ hỏng: chuyển DAMAGED.
- Không giảm tổng sở hữu do thay đổi trạng thái.

Không tự động coi tất cả đồ hoàn trả là READY.

Không hoàn trả hai lần.

Không cho nhân viên giữ số dư âm.

Không làm mất lịch sử.

---

## 24. MINIMUM STOCK & LOW STOCK ALERT

Giữ chức năng OM cấu hình Minimum Stock.

Đồ fill mặc định: 50.

Nhưng phải kiểm tra đúng loại tồn.

### Consumables

Sử dụng tồn khả dụng đủ điều kiện cấp phát.

Không tính:

- Hàng hết hạn.
- Hàng hỏng.
- Hàng bị cách ly.
- Lượng đã reservation cho giao dịch khác.

### Reusable Items

Theo dõi riêng:

- Total Owned.
- Ready Quantity.

Nếu tổng chăn là 100 nhưng chỉ 10 chăn sạch sẵn sàng, hệ thống phải phát hiện thiếu chăn khả dụng nếu ngưỡng READY là 50.

Không dùng tổng 100 để kết luận đủ hàng phục vụ khách.

### Alert

Thông báo Storekeeper:

- Mặt hàng.
- Chi nhánh.
- Loại thiếu hụt.
- Tồn khả dụng.
- Ngưỡng tối thiểu.
- Số lượng thiếu.
- Lượng đang giặt hoặc đang điều chuyển nếu liên quan.

Không tạo cảnh báo lặp liên tục.

Cho phép giải quyết thiếu hụt bằng:

- Hoàn thành giặt là.
- Yêu cầu bổ sung kho.
- Yêu cầu điều chuyển.
- Nhập kho hợp lệ.

OM xử lý yêu cầu bổ sung hàng trực tiếp từ Storekeeper.

---

## 25. INTER-BRANCH STOCK TRANSFER V2

Bổ sung đầy đủ nghiệp vụ điều chuyển giữa DVBV và DVCT.

### Actor

- OM chi nhánh yêu cầu.
- OM chi nhánh cung cấp.
- Storekeeper kho nguồn.
- Storekeeper kho đích.

Giữ quyết định trước: OM được xem danh sách kho chi nhánh khác để tạo yêu cầu điều chuyển, nhưng không được truy cập toàn bộ dữ liệu nghiệp vụ chi nhánh đó.

### STEP 1 — Create Transfer Request

Người yêu cầu nhập:

- Chi nhánh yêu cầu.
- Kho đích.
- Chi nhánh cung cấp.
- Kho nguồn.
- Mặt hàng.
- Requested Quantity.
- Lý do.
- Thời hạn cần hàng nếu hỗ trợ.

### STEP 2 — Review

OM chi nhánh nguồn:

- Xem yêu cầu.
- Kiểm tra lượng khả dụng.
- Xác nhận số lượng có thể cung cấp.
- Hoặc từ chối.

Từ chối bắt buộc lý do.

### STEP 3 — Shipment

Storekeeper kho nguồn:

- Chuẩn bị hàng.
- Chọn Batch theo FIFO/FEFO đối với vật tư tiêu hao.
- Xác nhận Shipped Quantity.
- Tạo phiếu điều chuyển.
- Chuyển hàng sang IN_TRANSIT.
- Ghi lịch sử.

Không tự động ghi nhận kho đích đã nhận ngay khi kho nguồn gửi hàng.

### STEP 4 — Receipt

Storekeeper kho đích mở phiếu điều chuyển.

Phải hiển thị tối thiểu hai ô theo Note mới:

- Số lượng vật tư yêu cầu.
- Số lượng vật tư thực nhận.

Để đối soát đầy đủ, bổ sung thông tin chỉ đọc:

- Approved Quantity.
- Shipped Quantity.
- Received Quantity.
- Discrepancy.

Requested và Shipped Quantity không cho người nhận tự sửa.

Người nhận nhập Received Quantity thực tế.

### STEP 5 — Discrepancy handling

Ví dụ:

Requested = 20.

Approved = 20.

Shipped = 20.

Received = 18.

Discrepancy = 2.

Bắt buộc ghi lý do hoặc báo chênh lệch.

Không tăng tồn kho đích lên 20 khi thực tế chỉ nhận 18.

Không tự đưa 2 đơn vị còn thiếu trở lại kho nguồn khi chưa xác minh.

Không tự ghi mất hàng.

Giữ phần chênh lệch trong trạng thái cần đối soát.

### STEP 6 — Completion

Sau khi đối soát hợp lệ:
