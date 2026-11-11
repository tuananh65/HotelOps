# Authorization và options — candidate chờ owner

Tái sử dụng GET /api/incidents/options của local FE06 candidate, thêm optional roomId, không tạo endpoint preflight khác. GET chỉ read, NoStore. Không cần INCIDENT_VIEW cho create-only actor. Query duplicate bị400, room thiếu branch/ID<=0 bị400. Không có quyền branch hoặc branch unknown/inactive bị403. Không được trả canCreate=true khi thiếu branch; phải chọn branch có grant.

INCIDENT_CREATE đã có trong source catalog tại scripts/database/release/02_HotelOps_Upgrade_Auth_To_RBAC.sql:207 và 03_HotelOps_RBAC_Catalog_Seed.sql:57 ở baseline. Source registration không chứng minh deployed grants hoặc revised role approval; không chạy script hoặc cấp quyền. Dùng IPermissionService hiện hữu: global ALL/branch-scoped ALL hoặc BRANCH theo actual eligible grants; OWN/DEPARTMENT không tự suy ra create authority. Đọc lại permission paths; không cache grant để POST.

Options branch-only chỉ là UI hint theo branch. Khi có roomId mới kiểm tra current room/floor/area. Thành công GET không ghi dữ liệu và không cấp quyền POST. Actor của GET do cookie middleware/current-user xác minh; POST khóa user/session và placement trong transaction rồi kiểm tra lại trước commit. Session expiry/revocation, restricted, inactive user và denied grant fail closed. Live deployed security và SQL concurrency chưa kiểm chứng.

Security/RBAC + PL cần duyệt reporter compatibility, exact create-only/branch scope contract và grant của actor FE03 thực tế. Không thay shared policy/catalog/schema.

