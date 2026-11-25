# Phân loại vật tư V2

| Phân loại | Sổ vận hành | Khả dụng | Tác động khi cấp vào phòng |
|---|---|---|---|
| CONSUMABLE | StockMovement, StockBatch/Entry, StaffBatchBalance | READY, đủ dữ liệu hạn dùng, chưa hết hạn, trừ reserved | Tiêu hao đúng lượng thực bổ sung |
| REUSABLE | ReusablePosition/Entry và sổ kho đối ứng | READY tại đúng nơi giữ | Chuyển custody; tổng sở hữu không giảm |
| ASSET | Asset và lịch sử; pending migration có audit mapping | Tài sản đã xác minh, đúng vị trí/tình trạng và còn active | Gắn từng tài sản cụ thể, không hạch toán tiêu hao |
| UNCLASSIFIED | Chưa được xuất dùng | 0 | OM phải cấu hình hoặc đối soát trước |

Hàng mới qua catalog xuất hiện UNCLASSIFIED/UNKNOWN ở từng chi nhánh; OM cấu hình trước khi nhập/xuất. Không tự đổi ý nghĩa hàng đã có stock movement hoặc staff balance bằng dropdown.

Owner xác nhận Khăn tắm 213/Khăn mặt 218 là vải tái sử dụng; Ấm đun 222 là tài sản. Các ID chỉ dùng trong migration đã đối soát, không phải logic phân loại production theo ID/tên. Đồ fill khác phải có policy REQUIRED/NONE/UNKNOWN; thiếu hạn dùng không tự thành NONE.

A/B/C cũ phục vụ cấu hình cấp phát không đồng nghĩa trực tiếp A/B/C/D của Master. `ItemLifecycleProfile` là phân loại vòng đời chính thức; `SupplyConfiguration` giữ định mức/warehouse/version theo chi nhánh. Chưa đổi đồng loạt nhãn cũ khi chưa đối chiếu ngữ nghĩa.

Chứng cứ: inventory runtime 35 ca; focused D10 4 ca có tạo item thật qua API. Không coi fixture trực tiếp seed profile là bằng chứng tạo item qua giao diện.
