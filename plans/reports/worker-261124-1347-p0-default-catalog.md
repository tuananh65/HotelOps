# P0 default catalog

Hoàn thành script `scripts/database/compact-v2/p0-supply-defaults.sql` và mapping `docs/requirements/p0-261124/supply-default-catalog-mapping.md`.

- TEST-only; không apply và không tạo stock/ledger.
- Reuse chính xác Dầu gội/Khăn tắm/Nước uống với DEMO-EA; thêm 5 item metadata thiếu và 4 UOM nếu cần. Không coi Xà phòng là Sữa tắm.
- 8 cấu hình A mỗi chi nhánh: initial10/min50/perguest1. Không overwrite cấu hình hiện có; version1 cho cấu hình chưa tồn tại.
- Người đại diện: unique OM gốc có active role+membership, loại assignment mới chuyển Head. Runtime đọc thấy DVBV435/DVCT433; SQL không hard-code.
- 6 điều hòa có asset type Điều hòa → nhóm C nếu chưa cấu hình. Không đoán nhóm B; chưa có posted room ISSUE trong dữ liệu đọc.
- 2 loại báo cáo mỗi branch chỉ insert nếu thiếu; không re-enable loại bị tắt.
- SQL PARSEONLY đã chạy thành công trước guard same-name cuối; chưa chạy rollback/commit theo ownership parent.

Parent cần chạy rollback/retry migration sau p0-store-policy.sql, kiểm số dòng config và ràng buộc. Có thể dùng Apply-P0Test.ps1 -Rollback -Scripts @('p0-supply-defaults.sql'). Không có câu hỏi nghiệp vụ mới; dữ liệu nhóm B cần cấu hình thực tế cho fixture kiểm thử.
