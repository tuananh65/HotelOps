// Reads metadata only; never connects to SQL Server or migrates data.
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
const inventory = JSON.parse(readFileSync(process.argv[2] || 'artifacts/compact-db/source-inventory.json', 'utf8').replace(/^\uFEFF/, ''));
const manifest = JSON.parse(readFileSync('scripts/database/compact/table-manifest.json', 'utf8'));
const targets = new Set(manifest.map(t => t.name));
const mapping = new Map();
function group(sources, destination, strategy) {
  for (const source of sources.trim().split(/\s+/)) {
    if (mapping.has(source)) throw new Error(`Duplicate mapping: ${source}`);
    for (const target of destination.split(';')) if (!targets.has(target) && target !== 'extended_property') throw new Error(`Unknown target: ${target}`);
    mapping.set(source, { source_table: source, target_tables: destination, strategy });
  }
}
for (const source of 'branches departments room_types rooms users roles permissions role_permissions user_roles user_sessions'.split(' ')) {
  group(source, source, 'Giữ thực thể; chuẩn hóa tên cột, rowversion và FK. Không tương thích nguyên trạng với EF cũ.');
}
group('floors areas', 'locations', 'Gộp theo kind FLOOR/AREA; parent_location_id; giữ branch và mã địa điểm.');
group('user_branch_access employee_department_memberships department_management_access employee_department_membership_history', 'memberships;audit_logs', 'Tách kind BRANCH_ACCESS/EMPLOYMENT/DEPARTMENT_AUTHORITY, khoảng hiệu lực; sự kiện sửa được audit. Không tự cấp quyền.');
group('reference_values asset_types item_categories units_of_measure uom_conversions work_categories internal_request_types internal_request_type_policy incident_priority_sla_targets system_settings', 'reference_values', 'Danh mục có kind và code; cấu hình/SLA là settings_json có phiên bản hợp đồng; UOM chuẩn hóa về đơn vị gốc. Không tự suy ra quy đổi mâu thuẫn.');
group('assets', 'assets', 'Giữ tài sản hiện tại và người giữ; lịch sử tách asset_events.');
group('asset_condition_history asset_location_history asset_handovers', 'asset_events', 'Sự kiện bất biến condition/location/handover; chấp nhận bàn giao là sự kiện tiếp theo.');
group('inspection_checklists inspection_checklist_versions', 'checklist_templates', 'Một dòng cho mỗi code/version/branch; version đã công bố bất biến.');
group('inspection_checklist_items cleaning_item_definitions task_completion_criteria', 'checklist_items;checklist_templates', 'Chuẩn hóa tiêu chí có cấu trúc; snapshot khi tạo công việc.');
group('inspection_checklist_room_types task_category_processes operational_task_definitions operational_task_series preventive_maintenance_schedules', 'work_templates', 'Mẫu công việc/lịch lặp/checklist; một mẫu theo phạm vi room_type/asset/location. Quan hệ nhiều room_type cần nhiều mẫu.');
group('cleaning_tasks cleaning_reworks room_inspections incidents maintenance_works operational_tasks internal_requests preventive_maintenance_runs task_series_occurrences preventive_occurrence_details', 'work_items;work_links', 'Gộp theo kind; status theo từng loại; inspection/rework có parent cleaning và turnover; occurrence có khóa chống sinh trùng.');
group('cleaning_assignments internal_request_assignments operational_task_assignments', 'work_assignments', 'Lịch sử phân công; một ACTIVE assignment/work; giữ người giao/nhận và thời điểm.');
group('operational_task_dependencies internal_request_cleaning_links internal_request_incident_links internal_request_task_links', 'work_links', 'FK thật giữa các work_item; relation có ý nghĩa; kiểm tra chu trình dependency tại service.');
group('cleaning_progress_logs cleaning_progress_versions fs014_cleaning_blocker_resolutions incident_events incident_escalations incident_reopen_cycles maintenance_assignment_receipts maintenance_decline_events maintenance_log_progress maintenance_work_logs internal_request_events internal_request_closures internal_request_escalation_actions internal_request_escalations internal_request_feedback internal_request_handovers internal_request_progress operational_task_progress task_pause_events task_progress_versions task_reopen_events task_submission_events task_verification_events fe04_cleaner_attributions fe04_rework_authority_issuers fe05_reinspection_bindings_v1 fs016_rework_source_proofs', 'work_events;work_items', 'Sự kiện bất biến với actor/operation_key/reason/snapshot; trạng thái hiện tại ở work_items. Snapshot chứng cứ phải có contract theo loại trước khi chuyển dữ liệu.');
group('cleaning_task_items inspection_results fe05_reinspection_defect_results_v1', 'work_checklist_items;work_events', 'Kết quả theo work/checklist version; snapshot tiêu chí; reinspection là work mới, không ghi đè kết quả cũ.');
group('inspection_defects cleaning_rework_defects', 'inspection_findings;work_links', 'Defect gắn inspection và lần rework hiện tại; lịch sử gán qua work_events/links.');
group('room_blocks incident_affected_rooms', 'room_blocks;room_events', 'Room block có source_work; phòng bị ảnh hưởng nhưng không bị block lưu sự kiện liên kết room/source_work.');
group('room_cleaning_cycle_state room_readiness_events room_status_history', 'rooms;room_events', 'Con trỏ current_cleaning_work và turnover; lịch sử trạng thái. Readiness tính từ bằng chứng, không từ status đơn lẻ.');
group('warehouses', 'warehouses', 'Giữ kho và chi nhánh/phòng ban.');
group('inventory_items', 'inventory_items', 'Giữ mã hàng và base unit có FK phân loại UOM.');
group('stock_thresholds', 'inventory_policies', 'Ngưỡng theo warehouse/item; tồn tính từ ledger, episode chống thông báo lặp.');
group('stock_documents stocktakes stock_transfer_requests stock_transfer_receipts material_requests material_replenishments material_fulfillments', 'inventory_documents', 'Phân biệt kind, chứng từ nguồn và chứng từ con; nhận từng phần là chứng từ nhận riêng. Không biến yêu cầu thành bút toán kho.');
group('stock_document_lines stocktake_lines stock_transfer_request_lines stock_transfer_receipt_lines stock_receipt_line_details stock_return_line_details material_request_lines material_replenishment_lines material_replenishment_receipt_lines material_fulfillment_lines maintenance_material_usages', 'inventory_document_lines;inventory_documents', 'Dòng chứng từ có item/UOM/factor/qty và source_line_id. Usage gắn source_work trên chứng từ; dùng chứng từ riêng khi nguồn việc khác nhau.');
group('stock_movements', 'stock_movements', 'Giữ ledger bất biến, số lượng signed base, đảo giao dịch có FK và chống đảo trùng.');
group('material_request_approvals material_request_approval_cycles material_request_approval_line_snapshots internal_request_material_links', 'document_events;inventory_documents', 'Phê duyệt từng revision, snapshot từng line; source_work gắn yêu cầu nội bộ, không dùng ID đa hình.');
group('shift_templates work_shifts', 'shifts', 'Gộp TEMPLATE và SCHEDULED; thời gian lịch thực tế có start/end đầy đủ cho ca qua đêm.');
group('shift_assignments shift_attendance workdays', 'workdays', 'SCHEDULED thay dòng phân ca; WORKING/ENDED lưu chấm công và handover; lịch sử điều chỉnh qua audit.');
group('work_intervals', 'work_intervals', 'Giữ khoảng giờ, FK đến đúng workday/user/branch và work_id; một interval đang mở mỗi người.');
group('time_correction_requests', 'time_corrections', 'Giữ đề xuất giờ, bản gốc/version, người quyết định và trạng thái.');
group('attachments progress_evidence_links inspection_defect_attachments inspection_result_attachments fe05_reinspection_defect_attachments_v1 fs016_rework_evidence', 'attachments;work_events', 'Metadata tệp với đúng một owner FK; cần sao chép metadata khi một tệp dùng cho nhiều owner. Nội dung tệp ở object storage.');
group('audit_logs', 'audit_logs', 'Giữ audit bất biến; JSON trước/sau chỉ là snapshot, không là nguồn nghiệp vụ chính.');
group('notifications notification_recipients notification_delivery_intents notification_operation_reservations notification_publications internal_request_assignment_outbox internal_request_completion_outbox internal_request_decision_outbox internal_request_escalation_outbox internal_request_handover_outbox internal_request_intake_outbox internal_request_progress_outbox internal_request_withdrawal_outbox unscheduled_workday_notifications', 'notifications;work_events;document_events', 'Outbox một dòng/recipient/event; lease/retry/ack; producer ghi cùng transaction với nghiệp vụ; operation idempotency ở event.');
group('dev3_schema_migrations', 'extended_property', 'Không chuyển ledger các bản vá cũ. Phiên bản compact lưu extended property; lịch sử triển khai giữ trong Git/receipt.');

const sourceNames = new Set(inventory.tables.map(t => t.name));
const missing = [...sourceNames].filter(n => !mapping.has(n));
const extra = [...mapping.keys()].filter(n => !sourceNames.has(n));
if (missing.length || extra.length) throw new Error(JSON.stringify({missing,extra}));
const rows = inventory.tables.map(t => ({ ...mapping.get(t.name), source_column_count:t.columns.length }));
const csv = ['source_table,source_column_count,target_tables,strategy', ...rows.map(r => [r.source_table,r.source_column_count,r.target_tables,r.strategy].map(v => '"'+String(v).replaceAll('"','""')+'"').join(','))].join('\n')+'\n';
mkdirSync('docs/database/compact', {recursive:true});
writeFileSync(resolve('docs/database/compact/source-to-target.csv'), '\uFEFF'+csv);
writeFileSync(resolve('docs/database/compact/mapping-summary.json'), JSON.stringify({
  source_database:inventory.database_name,source_tables:sourceNames.size,target_tables:manifest.length,
  mapped_source_tables:rows.length,unmapped_source_tables:missing,
  source_column_count:inventory.tables.reduce((sum,t)=>sum+t.columns.length,0),
  source_objects:inventory.objects,
  limitation:'Table-level design mapping; not an executable or lossless column-level data migration.'
},null,2)+'\n');
console.log(`Mapped ${rows.length}/${sourceNames.size} source tables to ${manifest.length} target tables (plus schema-version metadata).`);
