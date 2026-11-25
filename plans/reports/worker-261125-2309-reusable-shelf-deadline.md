# Hạn kiểm tra tồn kho vật tư tái sử dụng

Thêm policy nội bộ theo chi nhánh/vật tư, lưu audit POLICY_SET; ngày 25/11/2028 được Owner cho phép tự đặt, không phải hạn nhà sản xuất. Chặn cấp từ READY và đưa STAFF_HOLDING vào IN_USE sau ngày này; vẫn cho trả, gom bẩn, giặt và đối soát. Availability và cảnh báo tồn kho loại lượng đã quá hạn. Không tự ghi giảm sở hữu.

Giới hạn: policy theo loại vật tư/chi nhánh, chưa theo từng chiếc/lô. API UI chưa hiển thị riêng ngày; parent có thể bổ sung. Tests thêm boundary inclusive và các hướng return/issue. Node syntax script chỉnh sửa PASS; parent cần thu kết quả build và chạy dotnet test sau tích hợp.
SQL đã diễn tập clone và áp dụng official sau backup CHECKSUM/VERIFYONLY, 10 policy mỗi database. Build chạy session17637, cần parent thu kết quả.
