# 2026-11-23 — Maintenance Lead alignment

Tiếp tục Project 2 với UX compact đã thống nhất. Audit SQL chỉ đọc xác nhận MAINTENANCE_LEAD role 11 và 14 grants; quyền execution, quản trị, kho và lập lịch không được bổ sung.

Thêm dashboard kỹ thuật, danh sách kỹ thuật viên/ca/timer theo MAINTENANCE_ASSIGN, tạo repair liên kết Incident, PM edit/history và asset maintenance context. Giao diện dùng Work/Assets/Documents/Workday hiện có; tạo PM/technician workspace cần thiết, tám nhóm sidebar có grant guards. Review sử dụng verify/reopen hiện hữu, không thêm lifecycle.

Kiểm thử phát hiện và sửa EF không dịch được OrderBy trên typed record; đổi projection anonymous trước mapping DTO. Chặn repair trùng khi work trước COMPLETED nhưng chưa review, cả đường derive và generic create. Giữ review filter khi reset và independent review cho reopen. Escape modal không bubble làm mất drawer. Browser PM edit tìm ra UTC string thiếu Z bị hiểu theo local; chuẩn hóa trước datetime-local để sửa tên không đổi 08:00 thành 01:00.

Kết quả: backend build 0 warnings/errors; FE typecheck/build và targeted formatting pass; .NET 28 pass/3 conditional skip, worker maintenance chạy riêng 1 pass. Maintenance API 33 checks và browser thật 27 views/4 mutations pass trên fixture 3cd42085fa72. Regression API 128 pass trên fixture 2354e037944c. Common UX cùng bốn role trước pass.

Inline review theo Single-Agent; không có subagent/commit/push/deploy. Primary worker giữ tắt; PM automation là giới hạn cấu hình được ghi rõ trong báo cáo. Khôi phục API 5768 PID31524, giữ Vite 5186; fixture API dừng, fixture DB/backup giữ lại.
