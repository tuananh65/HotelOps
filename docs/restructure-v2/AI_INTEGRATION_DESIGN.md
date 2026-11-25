# AI advisory và fallback

POST `api/v2/advisory` nhận DEMAND/TRANSFER/INCIDENT trong branch được phép, horizon hợp lệ và rate limit. IAdvisoryProvider tách provider; adapter hiện là OpenAI Responses API. Không hardcode model/API key. Các adapter khác có thể thêm qua cùng interface, provider không hỗ trợ trả trạng thái rõ ràng.

Khi chưa đủ cấu hình, hệ thống trả method STATISTICAL_RULES, externalStatus BLOCKED_EXTERNAL_CONFIGURATION, requiresHumanDecision=true. Forecast dùng lịch sử xuất/hoàn, xu hướng 7/30 ngày và kịch bản capacity phòng; không gọi kịch bản định mức là nhu cầu thực quan sát. Transfer đề xuất chỉ dựa hàng đủ điều kiện/khả dụng sau cam kết được tính. Incident dùng bằng chứng đã làm sạch, không gán trách nhiệm hay khẳng định chẩn đoán an toàn.

Prompt coi nội dung báo cáo là dữ liệu không tin cậy, không phải lệnh. Provider không có tool ghi nghiệp vụ/phê duyệt. Yêu cầu store=false, bounded output; lỗi HTTP/timeout/JSON shape được trả trạng thái và fallback, không giả response AI thành công. Không gửi tên/contact người liên quan trong bằng chứng incident đã sanitise.

Cấu hình cần có: AI__Provider=OpenAI, AI__Model do quản trị chọn, AI__ApiKey trong secret store, AI__Enabled=true khi được phép. Không ghi key/model giả để làm healthcheck xanh. Local dùng User Secrets; deployment dùng secret store/environment, không commit.

Bằng chứng: unit tests malformed provider response và HTTP fallback/scope/horizon. `transfer-advisory-twelve-pass.json` kiểm ba vật tư chỉ có receipt/transfer: available 2/3/4, used/issued/expectedDemand đều 0; không tính điều chuyển là tiêu hao. Sai branch/role bị 403, horizon 91 bị 400; ledger không đổi. `advisory-browser-results.json` xác minh DEMAND/TRANSFER trên Edge ghi rõ thống kê/quy tắc và cần người quyết định.

`housekeeping-completion-reviewed-eighteen-pass.json`: cấp 17, dùng 1, hoàn 16, khả dụng 34; dự báo 14 ngày là 2, có nhãn lịch sử dưới 7 ngày và không thay ledger. Lỗi 2.0001 do chia trước nhân đã sửa, có hai test hồi quy; toàn suite 122 PASS/3 SKIP.

`transfer-advisory-fourteen-pass.json`: nguồn 3 giữ tối thiểu 1, đề xuất 2; khi incoming đáp ứng thiếu thì đề xuất 0. UI đúng lượng và không thay ledger. `maintenance-report-ui-seventeen-pass.json`: INCIDENT trên UI HKS/MS mobile có nhãn thống kê/quy tắc, không quy kết người báo, chặn branch/ID sai và không đổi issue/tồn kho.

External runtime chưa chạy vì chưa có credentials: BLOCKED_EXTERNAL_CONFIGURATION; không hỏi lại hoặc gửi thử ngoài khi chưa được cho phép.
