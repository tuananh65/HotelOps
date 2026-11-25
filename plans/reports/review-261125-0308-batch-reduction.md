# Rà soát ghi giảm vật tư tiêu hao — D10

Ngày 25/11/2026, Asia/Saigon. Rà soát tĩnh độc lập; không sửa ứng dụng, không ghi database, không chạy build/server hoặc can thiệp fixture của agent khác.

## Phát hiện

### 1. P1 — Phiếu hỗn hợp bỏ qua thẩm quyền duyệt OM

`src/HotelOps.Api/Compact/Services/DocumentService.BatchAdjustment.cs:15` chỉ nhận diện ghi giảm khi **mọi** dòng đều giảm và mọi vật phẩm đều CONSUMABLE. `DocumentService.cs:27` chỉ áp dụng quyền OM khi hàm này trả true; ngược lại dùng STOCK_ADJUST_POST. `DocumentService.Inventory.cs:66` lặp lại quyết định này trên giao diện.

Tái hiện: một phiếu kiểm kê có dòng vật tư tiêu hao A thiếu và dòng vật tư tiêu hao B thừa; lập ADJUSTMENT cho cả hai, chọn lô giảm cho A. Hàm nhận diện trả false vì B tăng. OM không được duyệt; một người khác có STOCK_ADJUST_POST có thể duyệt rồi Thủ kho ghi sổ cả phần giảm mà không có OM. Với hiện trạng mỗi chi nhánh chỉ một Thủ kho, phiếu bị kẹt ngay tại duyệt độc lập.

Đề nghị: phân loại quyền theo việc phiếu **có bất kỳ** dòng giảm tiêu hao hay không; hoặc bắt buộc tách riêng phiếu giảm và phiếu tăng trước khi chọn/duyệt. Không để thêm dòng tăng làm hạ yêu cầu thẩm quyền.

### 2. P1 — API nhận số lượng duyệt thấp hơn nhưng ghi sổ toàn bộ lượng đề nghị

`DocumentService.cs:136-138` cho phép ApprovedLines với Quantity từ 0 đến ActualQty cho ADJUSTMENT và lưu ApprovedQty. `DocumentService.cs:203` vẫn dùng ActualQty × ConversionFactor khi ghi sổ; không kiểm ApprovedQty trong nhánh ADJUSTMENT.

Tái hiện: đề nghị giảm 5, kế hoạch lô giảm 5; OM gửi approve với ApprovedLines Quantity=0 hoặc 2. API chấp nhận và ghi ApprovedBy, nhưng thao tác post sau đó vẫn giảm 5. Đây là hành vi tồn tại trong bộ xử lý dùng chung, nay được mở cho luồng OM mới.

Đề nghị: nếu điều chỉnh kiểm kê phải duyệt toàn bộ, từ chối ApprovedLines khác lượng đề nghị và thể hiện rõ hợp đồng. Không cần hỗ trợ duyệt một phần vì kiểm kê đang bắt buộc đúng chênh lệch. Kiểm tra lại lượng được duyệt tại post để bảo vệ dữ liệu cũ/bất nhất.

### 3. P2 — Thông báo OM dẫn đến màn không có nút duyệt và không có kế hoạch lô

`frontend/src/compact/insights.tsx:313` mở `documents?kind=ADJUSTMENT&id=...` cho thông báo đề nghị. `app.tsx:443` chỉ chuyển route documents sang inventory document cho Storekeeper; OM vẫn dùng `documents.tsx`. Màn này xác định nút approve theo STOCK_ADJUST_POST (`documents.tsx`, phần actions), không dùng `detail.inventory.canApprove`, không tích hợp BatchAdjustment và không hỗ trợ từ chối phiếu DRAFT.

Kết quả: OM nhận thông báo và mở phiếu nhưng không có thao tác duyệt/từ chối theo quyền mới. Đường dẫn thủ công từ stock sang `stocktaking?kind=ADJUSTMENT` có thể làm việc, nhưng đường thông báo chính bị đứt.

Đề nghị: route ADJUSTMENT của OM về `stocktaking` (giữ id), hoặc dùng một màn chi tiết chung dựa hoàn toàn vào quyền từ backend. Kiểm thử bằng click thông báo của OM, không chỉ mở URL stocktaking trực tiếp.

### 4. P2 — Kế hoạch lô đã chọn biến mất khi lô hết số dư

`DocumentService.BatchAdjustment.cs:55` chỉ trả lô có WarehouseQuantity > ReservedQuantity. `frontend/src/compact/batch-adjustment.tsx:16` chỉ render từ danh sách này, dù snapshot selected vẫn chứa lô cũ.

Tái hiện: chọn giảm hết 5 đơn vị của lô, duyệt và ghi sổ thành công. Mở lại phiếu: lô có số dư 0 bị loại khỏi response batches; phần “Lô điều chỉnh giảm” không còn thể hiện lô và số lượng 5 đã ghi giảm. Tương tự, trước duyệt nếu lô đã bị xuất hết bởi chứng từ khác, OM không nhìn thấy mục đã chọn để đánh giá trong form.

Đề nghị: luôn trả metadata cho các BatchId có trong snapshot, kể cả số dư 0; tách danh sách lựa chọn hiện tại khỏi bảng kế hoạch đã chọn. Khi phiếu không sửa được, hiển thị snapshot đã duyệt/ghi sổ bằng nội dung đọc thay vì chỉ các input lấy theo tồn hiện tại.

## Điểm đã đối chiếu

- Kế hoạch chọn lô kiểm tra kho/chi nhánh, vật phẩm, số lượng dương, trùng LineId/BatchId và tổng khớp dòng.
- Post kiểm tra tồn lô lại, không dựa duy nhất vào giá trị lúc chọn; toàn bộ ghi sổ nằm trong transaction Serializable và dùng shared supply lock.
- RowVersion của header được cập nhật khi lưu kế hoạch; không cho sửa kế hoạch khi ApprovedBy đã có.
- Từ chối yêu cầu lý do và tạo DocumentEvent/Audit theo đường Act dùng chung.
- Tạo lifecycle profile mới kiểm quyền OM cùng chi nhánh, item active; việc đổi classification khi đã có lịch sử kho/balance vẫn chặn để yêu cầu đối soát.

## Giới hạn kiểm chứng

Không chạy build, HTTP, SQL hoặc browser theo phân công chỉ đọc; các tình huống trên là đường thực thi xác định từ mã, chưa phải kết quả runtime. Không có tệp JS thay đổi trong nhiệm vụ review nên `node --check` không áp dụng cho báo cáo Markdown. Cần bổ sung test phiếu hỗn hợp, approval quantity không đầy đủ, mở từ thông báo OM và xem lại phiếu giảm hết lô.

Không có câu hỏi nghiệp vụ cần Owner xác nhận thêm; các sửa chữa trên thực thi D10 đã chốt.
