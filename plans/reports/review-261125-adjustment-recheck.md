# Rà soát lại điều chỉnh vật tư và quyền thông báo

25/11/2026, Asia/Saigon. Rà soát độc lập, chỉ đọc source; không sửa ứng dụng/harness/database, không chạy build hoặc server.

## Kết quả bốn phát hiện trước

1. **Đã sửa:** `ConsumableReduction` dùng `AnyAsync` trên dòng giảm CONSUMABLE; phiếu hỗn hợp tăng/giảm vẫn yêu cầu OM cùng chi nhánh qua `RequireRolePermission`. Thủ kho vẫn ghi sổ bằng quyền riêng.
2. **Đã sửa:** approve ADJUSTMENT từ chối `ApprovedLines` khác toàn bộ `ActualQty`; post kiểm tra lại `ApprovedQty == ActualQty`. Không còn đường duyệt 0/2 rồi ghi giảm 5 được nêu trước đó.
3. **Đã sửa trong source:** `app.tsx:438` chuyển thông báo `documents?kind=ADJUSTMENT&id=...` của OM sang `stocktaking`. Query giữ nguyên; khóa của `<main>` gồm query nên đổi id sẽ remount màn chi tiết. Màn dùng `inventory.canApprove/canReject` và có BatchAdjustment. Chưa thực hiện click browser trong lần review này.
4. **Đã sửa:** `AdjustmentBatches` luôn lấy BatchId đã chọn dù số dư bằng 0; biểu mẫu chỉ đọc vẫn hiện số lượng đã chọn. Harness đã có assertion metadata lô cạn, nhưng tôi không chạy lại HTTP.

## Một lỗi còn lại — P2: duyệt được kế hoạch lô đã lỗi thời sau khi sửa dòng

`DocumentService.cs:103–116` cho sửa ItemId, SourceLineId và số lượng của dòng DRAFT chưa duyệt sau khi lưu kế hoạch lô. `DocumentService.cs:133–135` khi approve chỉ yêu cầu **tồn tại** sự kiện `BATCH_ADJUSTMENT_SELECTED`, chưa kiểm tra snapshot có khớp các dòng hiện tại. Trong khi đó, sau approve cả API sửa dòng và chọn lại lô đều khóa theo ApprovedBy.

Tình huống cụ thể: kiểm kê có hai vật phẩm tiêu hao A và B cùng thiếu 5. Tạo phiếu điều chỉnh với dòng A, chọn lô A giảm 5. Trước duyệt, sửa chính dòng đó sang B và SourceLineId của B, giữ lượng 5 (API Line cho phép, source cùng chứng từ và đúng item). OM duyệt thành công vì có sự kiện chọn lô cũ. Khi Thủ kho post, StockBatchAccounting tìm lô A với ItemId B và trả `BATCH_SOURCE_MISMATCH`. Phiếu đã duyệt không thể chọn lại lô hoặc sửa dòng; phải hủy và lập lại.

Không làm giảm nhầm tồn vì post chặn và transaction rollback, nhưng tạo phiếu được duyệt mà không thể thực hiện. Đề nghị trước approve xác minh snapshot mới nhất khớp LineId/ItemId/tổng lượng của tất cả dòng giảm hiện tại, hoặc vô hiệu hóa kế hoạch khi sửa dòng và yêu cầu lưu lại trước duyệt. Thêm kiểm thử sửa dòng sau chọn lô; approval phải từ chối trước khi gán ApprovedBy.

## VisibilityChecks

Không phát hiện cache quyền xuyên người dùng, chi nhánh hoặc lần phát thông báo trong phần được xem:

- Mỗi `UnreadCount`/`RealtimeSnapshot` tạo một VisibilityChecks mới. Khóa permission gồm code, branch, owns; khóa role gồm code, branch. Actor cố định trong service của lần đọc.
- NotificationRealtimeReader tạo DI scope/DbContext mới cho mỗi pulse và kiểm tra session, SecurityStamp, tài khoản, employment, first-login trước đọc.
- Access kiểm tra trạng thái user/session, thời hạn role, branch active và membership; kết quả cache không sống qua vòng Subscribe kế tiếp.
- Quyền của Work/Request/Transfer và quyền sở hữu vẫn được đánh giá theo từng bản ghi. HasRole không tự kiểm tra session nhưng các nhánh quyết định cuối cùng vẫn đi qua Allows hoặc helper có Allows.
- Đây là quyền tại thời điểm đánh giá snapshot; thay đổi quyền giữa lúc đang tính snapshot và gửi có cửa sổ đồng thời thông thường, không phải cache bền vững. Chưa chạy lại kiểm thử revoke/reconnect trong nhiệm vụ chỉ đọc này.

## Kiểm chứng và giới hạn

`node --check scripts/verification/v2-inventory-runtime.mjs`: **PASS**. Không thay đổi JS. Build, HTTP, SQL và browser: **Not Run** theo phạm vi reviewer. Kết quả 4 PASS runtime do parent cung cấp không được nhận là kết quả tự thực thi của reviewer.

Không có câu hỏi nghiệp vụ cần Owner xác nhận thêm.
