# Lễ tân: yêu cầu cá nhân và thao tác phòng trong ca

- [x] Rà soát UI, quyền yêu cầu nội bộ, Workday và room actions.
- [x] Bỏ sự cố lễ tân; cố định yêu cầu cá nhân nhân viên; gộp nhãn cần dọn dẹp.
- [x] Enforce ownership khi đọc IR và ca thực tế khi thay đổi phòng ở backend.
- [x] Build, test database riêng, browser, review; cập nhật Production cục bộ theo yêu cầu vận hành hiện hành.

Production hoàn tất: HTTPS5080 bundle mới, TLS tin cậy, API health200/auth401, không browser runtime error. API48468, frontend listener31292/launcher40032. Test5187 và preview5082 đã dừng. Grants131/checksum19132, rooms96/checksum546048433 không đổi trước/sau release. Chi tiết tại plans/reports/implementation-261123-1533-reception.md.

Không đổi schema/grants, không kiểm thử ghi primary. Fullstack delegate lỗi Windows command line too long; hai lượt explore đã hoàn tất rà soát độc lập và phát hiện thiếu guard ca, đường báo cáo/overview và lịch sử hủy cần cùng ownership. Đã sửa cả ba. Các thay đổi cũ được bảo toàn. SQL fixture cuối HotelOpsCompactApiTestd51a06f0e2df:29 PASS; .NET56 PASS/3SKIP; workflow5 PASS; frontend build/browser mới và hồi quy PASS. API Production mới PID48468; đang kiểm tra frontend5080 sau switch.
