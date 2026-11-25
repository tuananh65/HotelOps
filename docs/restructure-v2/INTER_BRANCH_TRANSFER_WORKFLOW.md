# Điều chuyển giữa hai chi nhánh

OM kho đích tạo yêu cầu; OM nguồn duyệt trong phạm vi quyền nguồn. Thủ kho nguồn lập shipment từ phiếu đã duyệt và xuất đúng hàng đủ điều kiện. Thủ kho đích xác nhận lượng thực nhận qua TRANSFER_RECEIPT. Người không thuộc hai bên không được xem hay xử lý.

Requested, approved, shipped và received là bốn lượng riêng. Nhận một phần giữ PARTIALLY_RECEIVED; tổng nhận không vượt đã gửi. Nguồn/đích/line reference và unit conversion phải khớp. Operation key, RowVersion, transaction và supply lock bảo vệ thao tác lặp/concurrent.

Consumable chuyển theo batch origin, không cấp lô hết hạn. Reusable từ READY sang IN_TRANSIT còn thuộc nguồn; chỉ lượng thực nhận chuyển sở hữu sang READY tại đích. Tồn đang đi không bị hạch toán là tiêu hao.

Lý do thực nhận/chênh lệch bắt buộc cả đường derive lẫn create trực tiếp rồi post. D12 được Owner chốt ngày 25/11/2026: giữ phần thiếu chờ đối soát, không tự ghi mất hoặc hoàn nguồn. B07 đã được giải quyết về chính sách; phần đang đi vẫn được bảo toàn trên sổ.

Runtime reusable đã xác minh gửi 3, nhận 2 rồi 1, bảo toàn sở hữu và chặn nhận thêm. Lượt consumable `transfer-eight-pass.json` đạt 8 kiểm tra HTTP/SQL: hai shipment, FEFO, nhận một phần, nguồn gốc lô/hạn dùng, sai vai trò nguồn/đích, nhận vượt và hai request nhận đồng thời. Cuối lượt: gửi 8, nhận 6, còn 2 chờ đối soát; tồn nguồn 2 + đích 6 + đang chuyển 2 = nhập 10.

Gửi lại cùng lệnh ghi sổ với RowVersion cũ trả `409 STALE_ROW_VERSION`; không tạo thêm bút toán. Hai request đồng thời chỉ một commit; request còn lại trả lỗi phiên bản hoặc `TRANSACTION_RETRY_REQUIRED`. Lượt đầu harness kỳ vọng cả hai trả 200 là sai hợp đồng, được lưu riêng và không tính PASS.

Lượt mở rộng `transfer-eleven-pass.json` đạt 11/11 HTTP/SQL/browser. UI bốn actor: OM đích yêu cầu 3 → OM nguồn duyệt 2 → SK nguồn gửi 2 → SK đích nhận 1, có lý do thiếu. Bảng mobile 390px hiển thị chỉ đọc 3/2/2/1/1, không tràn ngang. Kiểm thêm đơn vị gói 2: yêu cầu 4 gói, duyệt/gửi 3 gói; hai phiếu cùng nhận 2 gói chỉ một phiếu commit. Nhận thêm 1 gói còn lại cho tồn nguồn 4 + đích 6 = nhập 10 đơn vị cơ sở. Lô đích giữ originBatchId, hạn dùng và receivedAt nguồn. Fixture runId khác không được Inspect/Cleanup/Stop.

Giới hạn: browser dùng session TEST có sẵn, chưa login/SignalR; chưa chứng minh mọi đường hết hạn khi hàng đang chuyển hoặc mọi vai trò không liên quan. Các ca P0 là bằng chứng lịch sử, không thay thế V2.
