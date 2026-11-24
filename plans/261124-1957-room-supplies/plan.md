# Vật phẩm trong phòng và tiêu hao đồ fill

- [x] Đối chiếu dữ liệu phòng, vật phẩm, chứng từ và tiêu hao hiện tại.
- [x] Gán đồ fill và đồ dùng bền cho tất cả phòng hoạt động DVBV/DVCT, sao lưu trước cập nhật; dữ liệu nhập kiểm thử được duyệt và ghi nhãn rõ ràng.
- [x] Kiểm tra định danh phòng–vật phẩm, trừ đồ fill theo sức chứa tối đa và ID vật phẩm, chống trừ lặp.
- [x] Kiểm thử TEST, áp dụng local đã được duyệt, đối soát và báo cáo.

Người dùng xác nhận tất cả phòng hoạt động ở hai chi nhánh. Đồ dùng bền 1 đơn vị mỗi loại/phòng; đồ fill theo sức chứa × định mức mỗi khách. Thiếu tồn phải được xử lý minh bạch. Không sub-agent, commit hoặc push.

Đã duyệt thêm ấm đun, ly uống nước, móc áo, thùng rác; nhập dữ liệu kiểm thử 100 đơn vị mỗi loại fill và đồ dùng bền vào kho mỗi chi nhánh. Ngoại lệ đã duyệt: Develop Bùi Viện nhập 102 đơn vị mỗi loại fill do tồn ban đầu 0.

Kết quả mỗi môi trường: 96 phòng, 1.152 cặp phòng–vật phẩm, 194 chứng từ (2 nhập, 96 yêu cầu, 96 xuất). TEST chạy lại tạo 0 chứng từ. API thực kiểm tra phòng 2/4 khách, chống trừ lặp, từ chối vật phẩm không thuộc phòng và hoàn tất luồng thiếu đồ. API TEST tạm PID2060:5775 đã dừng; Production/Develop giữ nguyên tiến trình và health OK.

Báo cáo: `plans/reports/implementation-261124-1957-room-supplies.md`.
