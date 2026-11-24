# Dọn phòng và khắc phục sự cố phòng

Đã triển khai luồng dọn phòng theo hạn 10–30 phút từ lúc phân công; ghi audit quá hạn một lần theo lượt giao. Nhân viên bắt đầu trực tiếp rồi báo hoàn thành; không báo sự cố, tải bằng chứng hoặc bàn giao sau nhận. Giao diện điện thoại mở công việc bằng cách chạm hàng, không tràn ngang.

Giám sát báo hỏng/thiếu đồ đã gán vào phòng. Trưởng bộ phận cấu hình hai loại, duyệt và gửi bảo trì hoặc kho. Bảo trì phải hoàn thành/nghiệm thu; kho phải cấp thật và xác nhận bổ sung. Sau đó Head gửi giám sát kiểm tra giấy. Đạt mở lượt cần dọn mới, không đưa phòng về sẵn sàng. Nhiều sự cố giữ block đến khi tất cả đạt; block thủ công được giữ.

Quyết định ngày 23/11 lúc18:00: phiếu vật tư của sự cố đã duyệt được kho tiếp nhận trực tiếp. Ngoại lệ tự duyệt chỉ áp dụng phiếu này, có audit/bằng chứng và trigger kiểm tra. Phiếu thường vẫn duyệt độc lập; người xuất kho phải khác người duyệt. Đã kiểm tra cấp thiếu, hoàn trả, thay phiếu, không tái sử dụng kết quả sửa chữa cũ và chặn đổi người tạo để vượt kiểm tra SQL.

## Tệp thay đổi trong phạm vi

- Backend Compact: Contracts/Common.cs, Contracts/RoomIssueRequests.cs; Controllers/OperationsControllers.cs; Services/WorkService.cs, WorkService.RoomIssueConfig.cs, WorkService.RoomIssues.cs, WorkService.RoomIssueActions.cs, WorkService.RoomIssueMaterials.cs, DocumentService.cs, DocumentService.RoomRemediation.cs, CleaningDeadline.cs, OperationsWorker.cs, EvidenceService.cs, RoomService.cs, NotificationService.cs.
- Frontend Compact: work.tsx, room-issues.tsx, housekeeping-mobile.css, app.tsx, rooms.tsx, insights.tsx, api.ts.
- Database scripts compact-v2: grant-room-remediation.sql, enable-room-remediation.sql, Enable-RoomRemediation.ps1, Test-Backend.ps1, test-room-remediation.ps1, test-room-remediation-grants.ps1, test-cleaning-on-shift.ps1.
- Tests: CleaningDeadlineTests.cs; compact-room-remediation.mjs, compact-cleaning-deadline.mjs. Luồng kiểm tra giấy hiện có được chạy hồi quy.

## Xác minh

- .NET build: Passed, 0 warning/0 error. Release publish cuối: Passed, không cảnh báo.
- .NET test: 66 Passed, 3 Skipped (các test tích hợp yêu cầu cấu hình riêng).
- API fixture HotelOpsCompactApiTest54f9c926ca5b: 69 nhóm Passed.
- Browser mock API: cả ba bộ remediation/deadline/paper inspection Passed, điện thoại 320/375/430 và bố cục rộng hơn. Đây không phải thao tác tạo dữ liệu trên Production.
- Frontend build Passed. Vite thông báo outDir ngoài frontend sẽ không tự xóa; thư mục release riêng được dùng.
- Production database đã backup CHECKSUM và VERIFYONLY lúc18:20; áp dụng đúng3grant. Đối chiếu giữ nguyên users, rooms và quyền của vai trò khác; vẫn27trigger. API PID46692, frontend launcher19284. Preview5082 đã đóng.
- Không reset phòng, không commit/push, không sửa Project1.

Giới hạn: Production chỉ kiểm tra khởi động và HTTP/browser đọc, không tạo nghiệp vụ giả vào dữ liệu thật. Trưởng bộ phận cần cấu hình hai loại báo cáo trên giao diện trước khi giám sát dùng.
