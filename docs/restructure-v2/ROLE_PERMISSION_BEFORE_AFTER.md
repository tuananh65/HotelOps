# Vai trò và quyền trước/sau V2

Trạng thái 25/11/2026: delta trên database TEST, chưa phát hành. Nguồn chính là Master Prompt, D01–D12 và các quyết định Owner bổ sung trong OWNER_UX_DELTA_20261125.md; SRS cũ không ghi đè các quyết định này.

| Vai trò | Baseline và thay đổi V2 | Ràng buộc còn giữ |
|---|---|---|
| OM | Giữ gộp DH; quản lý kế hoạch, cấu hình, tài sản; duyệt ghi giảm reusable và giảm consumable theo D03/D10 | Chỉ xem yêu cầu liên bộ phận thông thường; không tự tạo/xử lý sự cố thay đội nghiệp vụ |
| HK Supervisor | Phân công, kiểm tra, xác nhận sự cố và kiểm tra sau khắc phục | Cùng chi nhánh; không truy cập trang Tình trạng phòng |
| HK Staff | Nhận việc, đánh giá vật tư thực tế, bổ sung thiếu, checklist, gửi hoàn thành | Vật tư khả dụng phải đủ reservation; không tiêu hao cả định mức khi phòng còn đồ dùng được |
| Maintenance Supervisor | Quản lý đội kỹ thuật, giao nội bộ/thuê ngoài, xác minh và nghiệm thu kỹ thuật | Không chuyển Maintenance Staff thành HK Staff |
| Maintenance Staff | Khôi phục 10 tài khoản theo provenance P0; xem/thực hiện việc được giao | OWN work, BRANCH asset view; không quyền nghiệm thu thay giám sát |
| Storekeeper | Nhập/xuất/điều chuyển, xác minh lô, reusable, đề nghị ghi giảm | Không tự duyệt đề nghị của mình; OM không ghi sổ kho thay Thủ kho |
| Reception / Owner / Administrator | Giữ baseline ngoài thay đổi Master | Owner giám sát không mặc nhiên có quyền thực thi nghiệp vụ |

Backend kiểm tra role, permission, branch membership, session và thời hạn role. Không hardcode role ID trong service. D10 dùng đường quyền OM + ROOM_SUPPLY_CONFIG cùng chi nhánh; không cấp STOCK_ADJUST_POST cho OM. Phiếu có bất kỳ dòng giảm consumable đều cần OM, kể cả có dòng tăng đi kèm.

Nguồn thực thi: `Compact/Security/Access.cs`, `WorkService.Authorization.cs`, `RequestWorkflowService.cs`, `DocumentService.BatchAdjustment.cs`, `ReusableInventoryService.cs`. Bằng chứng D10: `artifacts/restructure-v2/adjustment-focused-runtime.log` (4 PASS, gồm sai chi nhánh/sai role). Chưa có ma trận HTTP/browser âm đầy đủ cho mọi role; không suy từ build thành RBAC E2E PASS.
