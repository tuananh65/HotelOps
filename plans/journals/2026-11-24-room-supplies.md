# 24/11/2026 — Vật phẩm trong phòng

Người dùng xác nhận cấp cho toàn bộ phòng Bùi Viện/Cao Thắng, bổ sung 4 loại đồ bền và nhập dữ liệu kiểm thử 100 đơn vị mỗi loại/kho; Develop Bùi Viện riêng 102 fill. Đã kiểm tra code hiện hữu trừ theo item ID và sức chứa tối đa, nên không thay logic ứng dụng.

Script dữ liệu được chạy trên clone trước. Lần đầu trigger yêu cầu nguồn phiếu vật tư nên rollback; bổ sung đủ nguồn và duyệt độc lập, không sửa trigger. Đối soát và retry đạt. API TEST kiểm tra 2/4 khách và thiếu đồ theo room/item pair thành công. Sao lưu mới, cập nhật Production/Develop, mỗi nơi đủ 96 phòng và 1.152 cặp. Dừng API TEST tạm; dịch vụ chính health OK. Develop tồn fill thấp/0 là hệ quả số lượng nhập đã duyệt, không tự thêm ngoài phạm vi.

Bằng chứng: `plans/reports/implementation-261124-1957-room-supplies.md` và `artifacts/room-supplies/`.
