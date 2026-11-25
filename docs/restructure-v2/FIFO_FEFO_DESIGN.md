# Lô, FIFO và FEFO

`StockBatchAccounting` phân rã stock movement theo lô trong cùng transaction với chứng từ và sổ custody. Mỗi lô có branch, warehouse, item, nguồn nhập, thời điểm nhập, hạn dùng, lượng tại kho/reserved và trạng thái. Mỗi lần ghi sổ có StockBatchEntry liên kết movement hoặc staff ledger.

REQUIRED dùng FEFO: hạn gần nhất, sau đó thời điểm nhập và ID ổn định. NONE dùng FIFO. UNKNOWN, LEGACY_REVIEW, QUARANTINED và EXPIRED không được xuất sử dụng. Ngày hết hạn xét theo ngày Việt Nam; ngày hết hạn hiện tại còn hợp lệ, ngày trước hiện tại bị loại. Không tự bịa hạn dùng cho lịch sử thiếu dữ liệu.

Thông tin định danh lô đã có xuất không được sửa để kéo dài hạn. Hoàn trả giữ batch origin; chuyển chi nhánh giữ origin batch. Số lượng khả dụng không âm, không vượt từng lô; mọi writer dùng cùng khóa supply và transaction.

Giảm/hủy consumable là ngoại lệ có kiểm soát: Thủ kho chọn đúng lô, lý do và lượng khớp chênh lệch kiểm kê; OM cùng chi nhánh duyệt độc lập; Thủ kho ghi sổ. Không dùng bộ chọn FIFO/FEFO của xuất sử dụng để hủy hàng hết hạn. Phiếu hỗn hợp vẫn cần OM. Điều chỉnh phải duyệt toàn bộ; backend từ chối partial approval và kiểm lại trước post. Snapshot lô phải khớp dòng hiện tại khi duyệt; post kiểm lại tồn. Lô hết tồn vẫn hiện trong lịch sử kế hoạch.

`Inventory__ExpiryWarningDays` đặt ngưỡng 1–365 ngày (mặc định 30), có thể ghi đè `Inventory__Branches__<branchId>__ExpiryWarningDays`. Cấu hình này không phải hạn dùng của hàng. `LowStockService` quét cảnh báo bền vững EXPIRY_WARNING cho Thủ kho, dedup theo lô/hạn/giai đoạn sắp hết hoặc đã hết; cảnh báo cũ hết hiệu lực khi không còn liên quan. Source mới cần runtime xác minh, không đánh dấu PASS chỉ vì build.

Giới hạn: cột ReservedQuantity chưa đại diện đầy đủ mọi cam kết material request/transfer đã duyệt. Posting vẫn chống xuất âm, nhưng khả dụng trước duyệt chưa đủ chứng cứ chống hứa cùng một tồn cho nhiều phiếu. Không công bố đã hoàn tất reservation theo lô.
