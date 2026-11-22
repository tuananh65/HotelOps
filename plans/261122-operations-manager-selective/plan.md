# Operations Manager — áp dụng chọn lọc

Giữ UX đã chốt: chi nhánh dưới logo, tiêu đề navbar, toolbar gọn, bộ lọc đóng mặc định, phòng 15 ô/trang và drawer. Tài liệu P0 là nguồn đối chiếu, không thay thế các quyết định UX trước.

- [x] Đọc tài liệu và mã hiện hành. SQL chỉ đọc xác nhận 24 quyền BRANCH, mỗi chi nhánh một OM; không cần cấp thêm quyền.
- [x] Đối chiếu luồng: Room/Asset/Work đã có audit, version và kiểm tra phạm vi. Không mở thêm quyền chuyên môn, lịch hay quản trị tài khoản.
- [x] Nhóm điều hướng OM thành 8 mục bằng component hiện có; mặc định giám sát toàn phạm vi thay vì mine; tìm kiếm/bộ lọc server.
- [x] Bổ sung dashboard thực theo từng quyền, nhật ký lọc/phân trang, tài sản tạo/sửa qua editor dùng chung; sửa phòng có khách đồng thời bị chặn.
- [x] Build FE/BE, kiểm thử API trên database fixture và UI hồi quy; rà soát và báo cáo phần chưa áp dụng.

Không thêm lịch sử xu hướng khi chưa có nguồn snapshot; không tạo quy trình chuyển tài sản liên chi nhánh mới. Không sửa schema, entity, quyền hoặc dữ liệu chính. Chỉ thay đổi dịch vụ/DTO đầu ra và giao diện thuộc phạm vi yêu cầu.

Process giữ lại: FE node 6424/9324 cổng 5186; API dotnet 40104 cổng 5768. API sẽ dừng trước build rồi khởi động lại. Không có process thừa do lượt OM tạo.

API cũ 40104 đã dừng trước build; API mới 12864 cổng 5768 health ok, worker tắt. Fixture 5187 tự dừng sau kiểm thử. Không có commit/push/deploy.

Kiểm chứng: backend build 0 warnings/errors; .NET 28 passed, 2 skipped do thiếu môi trường của test riêng. Fixture cuối HotelOpsCompactApiTest1e38bf904781: 18 nhóm đạt. Hai lượt fixture đầu thất bại do thiếu PRIORITY và cố đặt OCCUPIED trực tiếp; đã sửa dữ liệu fixture và dùng đủ luồng dọn/kiểm tra/nhận phòng. Không nới lỏng trigger.

Hoàn tất: FE build cuối Passed; OM UI 24 route/viewport Passed sau sửa tràn ngang grid 375px; DH UI 21 Passed; cả 5 suite UX chung Passed; workflow 4/4 Passed; Prettier Passed. Xem `plans/reports/operations-manager-261122-selective.md` cho danh sách file, giới hạn và bằng chứng. Không còn kiểm tra đang chạy.
