# HotelOpsCompactDB — ERD

Mở [HotelOps-ERD.html](HotelOps-ERD.html) bằng Chrome hoặc Edge. File xem chạy offline, có 8 chế độ: tổng thể và 7 nhóm chức năng; chọn bảng để xem toàn bộ cột, index duy nhất và FK, phóng to/thu nhỏ, tải SVG của nhóm đang mở.

- Nguồn: metadata SQL Server đọc trực tiếp từ HotelOpsCompactDB lúc 2026-11-22 15:11:44 +07:00.
- 40 bảng, 493 cột, 160 ràng buộc FK; 75 FK ghép. Không có FK bị disable hoặc chưa trusted.
- Schema trước/sau chuẩn hóa dữ liệu V2 giống nhau. Sơ đồ thể hiện cấu trúc, không phải danh sách bản ghi nghiệp vụ.
- [SVG tổng thể](overview.svg): đủ 40 bảng và 160 FK, chỉ hiện cột PK để giảm độ rối.
- [Mermaid đầy đủ](hotelops-full.mmd): toàn bộ 493 cột, kiểu dữ liệu, PK/FK/UK và quan hệ.
- Các sơ đồ nhóm hiện đủ cột của bảng thuộc nhóm; bảng ngoài nhóm chỉ hiện PK. Sidebar luôn có đầy đủ metadata.
- [Metadata máy đọc](schema.json); các tệp .mmd/.svg theo nhóm nằm cùng thư mục.

## Cách đọc

PK = khóa chính; FK = khóa ngoại; UK = unique đơn cột, không có filter. Khóa duy nhất ghép và filtered index xem trong sidebar, không suy diễn UK trên từng cột thành phần. Sơ đồ dùng Crow’s foot: || là một, o|/|o là không hoặc một, o{ là không hoặc nhiều. FK có ít nhất một cột cho phép NULL được vẽ tùy chọn; điều này mô tả FK vật lý, không thay thế check constraint hay quy tắc dịch vụ.

Nét đứt là quan hệ không định danh (các cột FK không cùng thuộc PK); nét liền là quan hệ định danh. Cardinality phía con dùng unique index không lọc bao phủ bởi các cột FK; FK không bắt buộc một bản ghi cha phải có bản ghi con.

Mỗi Fxxx tương ứng một ràng buộc FK thật, ánh xạ bên dưới. Hai FK tự tham chiếu reference_values (F081/F082) dùng chung một đường có hai nhãn do giới hạn renderer; không bị loại bỏ. Vì vậy sơ đồ tổng thể có 159 đường cho 160 ràng buộc. Không tự thêm quan hệ đa hình hoặc quan hệ suy ra từ tên cột.

## Đối chiếu khóa ngoại

| Mã | FK | Bảng/cột con | Bảng/cột cha |
|---|---|---|---|
| F001 | FK__workday_a__creat__7ECCBBD3 | dbo.workday_assignments (created_by) | dbo.users (user_id) |
| F002 | FK__workday_a__shift__7BF04F28 | dbo.workday_assignments (shift_id) | dbo.workday_shifts (shift_id) |
| F003 | FK__workday_a__user___7CE47361 | dbo.workday_assignments (user_id) | dbo.users (user_id) |
| F004 | FK__workday_e__actor__066DDD9B | dbo.workday_events (actor_user_id) | dbo.users (user_id) |
| F005 | FK__workday_e__assig__0579B962 | dbo.workday_events (assignment_id) | dbo.workday_assignments (assignment_id) |
| F006 | FK__workday_s__branc__744F2D60 | dbo.workday_shifts (branch_id) | dbo.branches (branch_id) |
| F007 | FK__workday_s__depar__75435199 | dbo.workday_shifts (department_id) | dbo.departments (department_id) |
| F008 | FK__workday_s__role___763775D2 | dbo.workday_shifts (role_id) | dbo.roles (role_id) |
| F009 | FK_asset_events_actor_user_id | dbo.asset_events (actor_user_id) | dbo.users (user_id) |
| F010 | FK_asset_events_asset_id | dbo.asset_events (asset_id) | dbo.assets (asset_id) |
| F011 | FK_asset_events_branch_id | dbo.asset_events (branch_id) | dbo.branches (branch_id) |
| F012 | FK_asset_events_from_branch_id | dbo.asset_events (from_branch_id) | dbo.branches (branch_id) |
| F013 | FK_asset_events_from_location_id | dbo.asset_events (from_location_id, from_branch_id) | dbo.locations (location_id, branch_id) |
| F014 | FK_asset_events_from_room_id | dbo.asset_events (from_room_id, from_branch_id) | dbo.rooms (room_id, branch_id) |
| F015 | FK_asset_events_from_user_id | dbo.asset_events (from_user_id) | dbo.users (user_id) |
| F016 | FK_asset_events_source_work_id | dbo.asset_events (source_work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F017 | FK_asset_events_to_location_id | dbo.asset_events (to_location_id, branch_id) | dbo.locations (location_id, branch_id) |
| F018 | FK_asset_events_to_room_id | dbo.asset_events (to_room_id, branch_id) | dbo.rooms (room_id, branch_id) |
| F019 | FK_asset_events_to_user_id | dbo.asset_events (to_user_id) | dbo.users (user_id) |
| F020 | FK_assets_asset_type_id | dbo.assets (asset_type_id, asset_type_kind) | dbo.reference_values (reference_id, kind) |
| F021 | FK_assets_branch_id | dbo.assets (branch_id) | dbo.branches (branch_id) |
| F022 | FK_assets_custodian_user_id | dbo.assets (custodian_user_id) | dbo.users (user_id) |
| F023 | FK_assets_department_id | dbo.assets (department_id, branch_id) | dbo.departments (department_id, branch_id) |
| F024 | FK_assets_location_id | dbo.assets (location_id, branch_id) | dbo.locations (location_id, branch_id) |
| F025 | FK_assets_room_id | dbo.assets (room_id, branch_id) | dbo.rooms (room_id, branch_id) |
| F026 | FK_attachments_asset_id | dbo.attachments (asset_id, branch_id) | dbo.assets (asset_id, branch_id) |
| F027 | FK_attachments_branch_id | dbo.attachments (branch_id) | dbo.branches (branch_id) |
| F028 | FK_attachments_document_id | dbo.attachments (document_id, branch_id) | dbo.inventory_documents (document_id, branch_id) |
| F029 | FK_attachments_finding_id | dbo.attachments (finding_id, branch_id) | dbo.inspection_findings (finding_id, branch_id) |
| F030 | FK_attachments_uploaded_by | dbo.attachments (uploaded_by) | dbo.users (user_id) |
| F031 | FK_attachments_work_id | dbo.attachments (work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F032 | FK_audit_logs_actor_user_id | dbo.audit_logs (actor_user_id) | dbo.users (user_id) |
| F033 | FK_audit_logs_branch_id | dbo.audit_logs (branch_id) | dbo.branches (branch_id) |
| F034 | FK_checklist_items_checklist_template_id | dbo.checklist_items (checklist_template_id) | dbo.checklist_templates (checklist_template_id) |
| F035 | FK_checklist_templates_branch_id | dbo.checklist_templates (branch_id) | dbo.branches (branch_id) |
| F036 | FK_checklist_templates_created_by | dbo.checklist_templates (created_by) | dbo.users (user_id) |
| F037 | FK_departments_branch_id | dbo.departments (branch_id) | dbo.branches (branch_id) |
| F038 | FK_departments_manager_user_id | dbo.departments (manager_user_id) | dbo.users (user_id) |
| F039 | FK_document_events_actor_user_id | dbo.document_events (actor_user_id) | dbo.users (user_id) |
| F040 | FK_document_events_branch_id | dbo.document_events (branch_id) | dbo.branches (branch_id) |
| F041 | FK_document_events_document_id | dbo.document_events (document_id, branch_id) | dbo.inventory_documents (document_id, branch_id) |
| F042 | FK_document_events_line_document | dbo.document_events (line_id, document_id) | dbo.inventory_document_lines (line_id, document_id) |
| F043 | FK_inspection_findings_assigned_to | dbo.inspection_findings (assigned_to) | dbo.users (user_id) |
| F044 | FK_inspection_findings_branch_id | dbo.inspection_findings (branch_id) | dbo.branches (branch_id) |
| F045 | FK_inspection_findings_inspection_work_id | dbo.inspection_findings (inspection_work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F046 | FK_inspection_findings_result | dbo.inspection_findings (work_checklist_item_id, inspection_work_id) | dbo.work_checklist_items (work_checklist_item_id, work_id) |
| F047 | FK_inspection_findings_rework_work_id | dbo.inspection_findings (rework_work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F048 | FK_inspection_findings_severity_id | dbo.inspection_findings (severity_id, severity_kind) | dbo.reference_values (reference_id, kind) |
| F049 | FK_inventory_document_lines_branch_id | dbo.inventory_document_lines (branch_id) | dbo.branches (branch_id) |
| F050 | FK_inventory_document_lines_document_id | dbo.inventory_document_lines (document_id, branch_id) | dbo.inventory_documents (document_id, branch_id) |
| F051 | FK_inventory_document_lines_item_id | dbo.inventory_document_lines (item_id) | dbo.inventory_items (item_id) |
| F052 | FK_inventory_document_lines_source_item | dbo.inventory_document_lines (source_line_id, item_id) | dbo.inventory_document_lines (line_id, item_id) |
| F053 | FK_inventory_document_lines_unit_id | dbo.inventory_document_lines (unit_id, unit_kind) | dbo.reference_values (reference_id, kind) |
| F054 | FK_inventory_documents_approved_by | dbo.inventory_documents (approved_by) | dbo.users (user_id) |
| F055 | FK_inventory_documents_branch_id | dbo.inventory_documents (branch_id) | dbo.branches (branch_id) |
| F056 | FK_inventory_documents_created_by | dbo.inventory_documents (created_by) | dbo.users (user_id) |
| F057 | FK_inventory_documents_department_id | dbo.inventory_documents (department_id, branch_id) | dbo.departments (department_id, branch_id) |
| F058 | FK_inventory_documents_destination_branch_id | dbo.inventory_documents (destination_branch_id) | dbo.branches (branch_id) |
| F059 | FK_inventory_documents_destination_warehouse_id | dbo.inventory_documents (destination_warehouse_id, destination_branch_id) | dbo.warehouses (warehouse_id, branch_id) |
| F060 | FK_inventory_documents_posted_by | dbo.inventory_documents (posted_by) | dbo.users (user_id) |
| F061 | FK_inventory_documents_receiving_room_id | dbo.inventory_documents (receiving_room_id, branch_id) | dbo.rooms (room_id, branch_id) |
| F062 | FK_inventory_documents_source_document_id | dbo.inventory_documents (source_document_id) | dbo.inventory_documents (document_id) |
| F063 | FK_inventory_documents_source_warehouse_id | dbo.inventory_documents (source_warehouse_id, branch_id) | dbo.warehouses (warehouse_id, branch_id) |
| F064 | FK_inventory_documents_source_work_id | dbo.inventory_documents (source_work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F065 | FK_inventory_items_base_unit_id | dbo.inventory_items (base_unit_id, base_unit_kind) | dbo.reference_values (reference_id, kind) |
| F066 | FK_inventory_items_category_id | dbo.inventory_items (category_id, category_kind) | dbo.reference_values (reference_id, kind) |
| F067 | FK_inventory_policies_branch_id | dbo.inventory_policies (branch_id) | dbo.branches (branch_id) |
| F068 | FK_inventory_policies_item_id | dbo.inventory_policies (item_id) | dbo.inventory_items (item_id) |
| F069 | FK_inventory_policies_warehouse_id | dbo.inventory_policies (warehouse_id, branch_id) | dbo.warehouses (warehouse_id, branch_id) |
| F070 | FK_locations_branch_id | dbo.locations (branch_id) | dbo.branches (branch_id) |
| F071 | FK_locations_parent_location_id | dbo.locations (parent_location_id, branch_id) | dbo.locations (location_id, branch_id) |
| F072 | FK_memberships_branch_id | dbo.memberships (branch_id) | dbo.branches (branch_id) |
| F073 | FK_memberships_department_id | dbo.memberships (department_id, branch_id) | dbo.departments (department_id, branch_id) |
| F074 | FK_memberships_recorded_by | dbo.memberships (recorded_by) | dbo.users (user_id) |
| F075 | FK_memberships_user_id | dbo.memberships (user_id) | dbo.users (user_id) |
| F076 | FK_notifications_branch_id | dbo.notifications (branch_id) | dbo.branches (branch_id) |
| F077 | FK_notifications_document_id | dbo.notifications (document_id, branch_id) | dbo.inventory_documents (document_id, branch_id) |
| F078 | FK_notifications_recipient_user_id | dbo.notifications (recipient_user_id) | dbo.users (user_id) |
| F079 | FK_notifications_room_id | dbo.notifications (room_id, branch_id) | dbo.rooms (room_id, branch_id) |
| F080 | FK_notifications_work_id | dbo.notifications (work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F081 | FK_reference_values_base_unit_id | dbo.reference_values (base_unit_id) | dbo.reference_values (reference_id) |
| F082 | FK_reference_values_parent_id | dbo.reference_values (parent_id) | dbo.reference_values (reference_id) |
| F083 | FK_role_permissions_permission_id | dbo.role_permissions (permission_id) | dbo.permissions (permission_id) |
| F084 | FK_role_permissions_role_id | dbo.role_permissions (role_id) | dbo.roles (role_id) |
| F085 | FK_room_blocks_blocked_by | dbo.room_blocks (blocked_by) | dbo.users (user_id) |
| F086 | FK_room_blocks_branch_id | dbo.room_blocks (branch_id) | dbo.branches (branch_id) |
| F087 | FK_room_blocks_released_by | dbo.room_blocks (released_by) | dbo.users (user_id) |
| F088 | FK_room_blocks_room_id | dbo.room_blocks (room_id, branch_id) | dbo.rooms (room_id, branch_id) |
| F089 | FK_room_blocks_source_work_id | dbo.room_blocks (source_work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F090 | FK_room_events_actor_user_id | dbo.room_events (actor_user_id) | dbo.users (user_id) |
| F091 | FK_room_events_branch_id | dbo.room_events (branch_id) | dbo.branches (branch_id) |
| F092 | FK_room_events_room_id | dbo.room_events (room_id, branch_id) | dbo.rooms (room_id, branch_id) |
| F093 | FK_room_events_source_work_id | dbo.room_events (source_work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F094 | FK_room_types_branch_id | dbo.room_types (branch_id) | dbo.branches (branch_id) |
| F095 | FK_rooms_branch_id | dbo.rooms (branch_id) | dbo.branches (branch_id) |
| F096 | FK_rooms_current_cleaning_work_id | dbo.rooms (current_cleaning_work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F097 | FK_rooms_current_cycle | dbo.rooms (current_cleaning_work_id, branch_id, room_id, turnover_no) | dbo.work_items (work_id, branch_id, room_id, turnover_no) |
| F098 | FK_rooms_department_id | dbo.rooms (department_id, branch_id) | dbo.departments (department_id, branch_id) |
| F099 | FK_rooms_location_id | dbo.rooms (location_id, branch_id) | dbo.locations (location_id, branch_id) |
| F100 | FK_rooms_room_type_id | dbo.rooms (room_type_id, branch_id) | dbo.room_types (room_type_id, branch_id) |
| F101 | FK_stock_movements_branch_id | dbo.stock_movements (branch_id) | dbo.branches (branch_id) |
| F102 | FK_stock_movements_created_by | dbo.stock_movements (created_by) | dbo.users (user_id) |
| F103 | FK_stock_movements_line_item | dbo.stock_movements (line_id, branch_id, item_id) | dbo.inventory_document_lines (line_id, branch_id, item_id) |
| F104 | FK_stock_movements_reversal_of_movement_id | dbo.stock_movements (reversal_of_movement_id) | dbo.stock_movements (movement_id) |
| F105 | FK_stock_movements_warehouse_id | dbo.stock_movements (warehouse_id, branch_id) | dbo.warehouses (warehouse_id, branch_id) |
| F106 | FK_user_roles_assigned_by | dbo.user_roles (assigned_by) | dbo.users (user_id) |
| F107 | FK_user_roles_branch_id | dbo.user_roles (branch_id) | dbo.branches (branch_id) |
| F108 | FK_user_roles_role_id | dbo.user_roles (role_id) | dbo.roles (role_id) |
| F109 | FK_user_roles_user_id | dbo.user_roles (user_id) | dbo.users (user_id) |
| F110 | FK_user_sessions_user_id | dbo.user_sessions (user_id) | dbo.users (user_id) |
| F111 | FK_users_manager_user_id | dbo.users (manager_user_id) | dbo.users (user_id) |
| F112 | FK_warehouses_branch_id | dbo.warehouses (branch_id) | dbo.branches (branch_id) |
| F113 | FK_warehouses_department_id | dbo.warehouses (department_id, branch_id) | dbo.departments (department_id, branch_id) |
| F114 | FK_warehouses_location_id | dbo.warehouses (location_id, branch_id) | dbo.locations (location_id, branch_id) |
| F115 | FK_work_assignments_assigned_by | dbo.work_assignments (assigned_by) | dbo.users (user_id) |
| F116 | FK_work_assignments_assignee_user_id | dbo.work_assignments (assignee_user_id) | dbo.users (user_id) |
| F117 | FK_work_assignments_branch_id | dbo.work_assignments (branch_id) | dbo.branches (branch_id) |
| F118 | FK_work_assignments_work_id | dbo.work_assignments (work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F119 | FK_work_checklist_items_branch_id | dbo.work_checklist_items (branch_id) | dbo.branches (branch_id) |
| F120 | FK_work_checklist_items_checked_by | dbo.work_checklist_items (checked_by) | dbo.users (user_id) |
| F121 | FK_work_checklist_items_item_template | dbo.work_checklist_items (checklist_item_id, checklist_template_id) | dbo.checklist_items (checklist_item_id, checklist_template_id) |
| F122 | FK_work_checklist_items_work_id | dbo.work_checklist_items (work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F123 | FK_work_checklist_items_work_template | dbo.work_checklist_items (work_id, checklist_template_id) | dbo.work_items (work_id, checklist_template_id) |
| F124 | FK_work_events_actor_user_id | dbo.work_events (actor_user_id) | dbo.users (user_id) |
| F125 | FK_work_events_branch_id | dbo.work_events (branch_id) | dbo.branches (branch_id) |
| F126 | FK_work_events_recipient_user_id | dbo.work_events (recipient_user_id) | dbo.users (user_id) |
| F127 | FK_work_events_related_event | dbo.work_events (related_event_id, work_id, branch_id) | dbo.work_events (work_event_id, work_id, branch_id) |
| F128 | FK_work_events_work_id | dbo.work_events (work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F129 | FK_work_intervals_assignment_identity | dbo.work_intervals (assignment_id, work_id, branch_id, user_id) | dbo.work_assignments (assignment_id, work_id, branch_id, assignee_user_id) |
| F130 | FK_work_intervals_branch_id | dbo.work_intervals (branch_id) | dbo.branches (branch_id) |
| F131 | FK_work_intervals_closed_by | dbo.work_intervals (closed_by) | dbo.users (user_id) |
| F132 | FK_work_intervals_opened_by | dbo.work_intervals (opened_by) | dbo.users (user_id) |
| F133 | FK_work_intervals_user_id | dbo.work_intervals (user_id) | dbo.users (user_id) |
| F134 | FK_work_intervals_work_id | dbo.work_intervals (work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F135 | FK_work_items_asset_id | dbo.work_items (asset_id, branch_id) | dbo.assets (asset_id, branch_id) |
| F136 | FK_work_items_branch_id | dbo.work_items (branch_id) | dbo.branches (branch_id) |
| F137 | FK_work_items_category_id | dbo.work_items (category_id, category_kind) | dbo.reference_values (reference_id, kind) |
| F138 | FK_work_items_checklist_template_id | dbo.work_items (checklist_template_id, branch_id) | dbo.checklist_templates (checklist_template_id, branch_id) |
| F139 | FK_work_items_created_by | dbo.work_items (created_by) | dbo.users (user_id) |
| F140 | FK_work_items_department_id | dbo.work_items (department_id, branch_id) | dbo.departments (department_id, branch_id) |
| F141 | FK_work_items_location_id | dbo.work_items (location_id, branch_id) | dbo.locations (location_id, branch_id) |
| F142 | FK_work_items_parent_work_id | dbo.work_items (parent_work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F143 | FK_work_items_priority_id | dbo.work_items (priority_id, priority_kind) | dbo.reference_values (reference_id, kind) |
| F144 | FK_work_items_requester_department_id | dbo.work_items (requester_department_id, branch_id) | dbo.departments (department_id, branch_id) |
| F145 | FK_work_items_room_id | dbo.work_items (room_id, branch_id) | dbo.rooms (room_id, branch_id) |
| F146 | FK_work_items_severity_id | dbo.work_items (severity_id, severity_kind) | dbo.reference_values (reference_id, kind) |
| F147 | FK_work_items_verified_by | dbo.work_items (verified_by) | dbo.users (user_id) |
| F148 | FK_work_items_work_template_id | dbo.work_items (work_template_id, branch_id) | dbo.work_templates (work_template_id, branch_id) |
| F149 | FK_work_links_branch_id | dbo.work_links (branch_id) | dbo.branches (branch_id) |
| F150 | FK_work_links_created_by | dbo.work_links (created_by) | dbo.users (user_id) |
| F151 | FK_work_links_source_work_id | dbo.work_links (source_work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F152 | FK_work_links_target_work_id | dbo.work_links (target_work_id, branch_id) | dbo.work_items (work_id, branch_id) |
| F153 | FK_work_templates_asset_id | dbo.work_templates (asset_id, branch_id) | dbo.assets (asset_id, branch_id) |
| F154 | FK_work_templates_branch_id | dbo.work_templates (branch_id) | dbo.branches (branch_id) |
| F155 | FK_work_templates_checklist_template_id | dbo.work_templates (checklist_template_id, branch_id) | dbo.checklist_templates (checklist_template_id, branch_id) |
| F156 | FK_work_templates_created_by | dbo.work_templates (created_by) | dbo.users (user_id) |
| F157 | FK_work_templates_department_id | dbo.work_templates (department_id, branch_id) | dbo.departments (department_id, branch_id) |
| F158 | FK_work_templates_location_id | dbo.work_templates (location_id, branch_id) | dbo.locations (location_id, branch_id) |
| F159 | FK_work_templates_room_id | dbo.work_templates (room_id, branch_id) | dbo.rooms (room_id, branch_id) |
| F160 | FK_work_templates_room_type_id | dbo.work_templates (room_type_id, branch_id) | dbo.room_types (room_type_id, branch_id) |
