# Quyết định và blocker V2

## Quyết định chính thức của Owner — 25/11/2026
| ID | Quyết định | Trạng thái |
|---|---|---|
| D01 | Master Prompt đính kèm là nguồn nghiệp vụ chính thức; không chờ Note 24_11.docx riêng. Quyết định bổ sung của Owner được ghi tại đây. | CONFIRMED |
| D02 | Giữ chỗ theo định mức tối đa khi phân công; nhân viên kiểm tra usable thực tế, backend tính thiếu và giải phóng dư; chỉ ghi nhận lượng thực bổ sung. Không giao khi khả dụng không đủ reservation. | CONFIRMED |
| D03 | Vật tư tái sử dụng hỏng còn thuộc sở hữu nhưng không khả dụng. Thủ kho đề nghị ghi giảm; OM cùng chi nhánh xác nhận. Chỉ giảm sở hữu sau commit. Từ chối có lý do và lịch sử. | CONFIRMED |
| D04 | AI ưu tiên OpenAI qua provider abstraction, model/key từ cấu hình. Fallback thống kê/rules phải ghi nhãn; không tự mutation. | CONFIRMED |
| D05 | Email invitation/setup token/resend/status/retry qua abstraction; kiểm thử mail sink. Không gửi email thật cho nhân viên hiện có. | CONFIRMED |
| D06 | Triển khai và kiểm thử SignalR thực tế độc lập với AI/SMTP. | CONFIRMED |
| D07 | Không hỏi lại API key/SMTP; external AI và email thật là BLOCKED_EXTERNAL_CONFIGURATION cho đến khi cấu hình hợp lệ. | CONFIRMED |
| D08 | Không reset, không dữ liệu tồn/hạn dùng giả, không đổi mật khẩu hiện có; migration chỉ vào TEST. Không commit/push/deploy nhiệm vụ này. | CONFIRMED |
| D09 | Khăn tắm ID 213 và Khăn mặt ID 218 là khăn vải tái sử dụng; Ấm đun ID 222 chuyển sang quản lý tài sản. Đối soát số dư, giữ chứng từ cũ, không tạo thêm tồn. Owner xác nhận 25/11/2026. | CONFIRMED |
| D10 | Thủ kho đề nghị điều chỉnh giảm/hủy vật tư tiêu hao hết hạn; OM cùng chi nhánh duyệt độc lập. Chỉ ghi giảm sau duyệt và commit chứng từ; từ chối cần lý do. Owner chọn phương án này lúc 02:59 ngày 25/11/2026. | CONFIRMED |

| D11 | Ấm đun: tạo hồ sơ chờ kiểm kê thực tế cho 200 đơn vị theo sổ; không tự kết luận vị trí/tình trạng/mất, không ghi giảm hay cộng tồn. Owner xác nhận 03:34 ngày 25/11/2026. | CONFIRMED |
| D12 | Điều chuyển thiếu so với lượng gửi: giữ phần chênh lệch chờ đối soát, không tự ghi mất hoặc hoàn kho nguồn. Owner xác nhận 09:10 ngày 25/11/2026. | CONFIRMED |

| D13 | Owner yêu cầu hoàn tất thay đổi và chạy cả Development/Production, xác nhận cả hai cùng local. Chạy hai clone V2 độc lập, không đổi Git branch hay triển khai EC2; giữ nguyên database vận hành và credentials. | CONFIRMED |

## Blocker và thông tin còn cần đối chiếu
| ID | Phạm vi | Bằng chứng/trạng thái | Cách tiếp tục |
|---|---|---|---|
| B01 | External AI | Owner chưa cấp provider credentials/model. | Triển khai adapter/API/UI và fallback rõ nhãn; runtime external chưa PASS. |
| B02 | Email thật | Owner chưa cho phép địa chỉ nhận và chưa cấp SMTP. | Test mail sink; không gửi đến email trong DB. |
| B03 | SQL baseline | RESOLVED: kết nối ngoài sandbox thành công; COPY_ONLY/CHECKSUM/VERIFYONLY và restore TEST HotelOpsCompactWorkdayTestV22611250010. | Chỉ migration TEST, database chính không đổi. |
| B04 | Recovery staff | Phục hồi 10 Maintenance Staff trên TEST theo snapshot P0; không đổi credential. | Kiểm thử quyền và workflow tiếp theo; không đổi HK_STAFF khác. |
| B05 | Inventory mapping legacy | A/B/C cũ khác A/B/C/D V2. Hạn dùng/lô lịch sử không được bịa; ấm đun đã tồn dưới inventory cần đối soát riêng. | Chuẩn bị mapping cần review; không tự chuyển dữ liệu bằng tên hàng. |
| B06 | Cấp đầu ca thiếu | Hiện all-or-nothing; Master Prompt ghi partial là đề xuất cần đối chiếu. | Giữ policy hiện có cho tới khi có quyết định thay thế; không xuất âm. |
| B07 | Transfer discrepancy final resolution | RESOLVED_POLICY: D12 giữ chênh lệch chờ đối soát. | Kiểm chứng hiển thị lượng còn thiếu và lịch sử; không thêm giao dịch tự ghi mất/hoàn nguồn. |

## Khóa cấu hình đã được Owner định hướng
AI: `AI__Provider`, `AI__Model`, `AI__ApiKey`, `AI__Enabled`.

Email: `Email__SmtpHost`, `Email__SmtpPort`, `Email__Username`, `Email__Password`, `Email__FromAddress`, `Email__Enabled`.

Danh sách khóa thực tế sau implementation, URL kích hoạt và token lifetime phải được ghi trong tài liệu tích hợp; không lưu giá trị secret tại đây.
