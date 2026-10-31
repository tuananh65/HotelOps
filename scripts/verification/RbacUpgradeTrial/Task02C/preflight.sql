SELECT DB_NAME() database_name,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) server_name;
SELECT d.department_id,d.branch_id,d.department_code,d.is_active,u.user_id,u.account_status,u.employment_status,u.deleted_at
FROM dbo.departments d LEFT JOIN dbo.users u ON u.department_id=d.department_id WHERE d.department_code IN ('FB','LAU','SEC');
SELECT warehouse_id,warehouse_code,department_id,manager_user_id,is_active FROM dbo.warehouses WHERE warehouse_id IN (4,8,9);
SELECT w.warehouse_id,m.item_id,COALESCE(SUM(m.signed_base_qty),0) on_hand,COUNT(m.movement_id) movement_count
FROM dbo.warehouses w LEFT JOIN dbo.stock_movements m ON m.warehouse_id=w.warehouse_id
WHERE w.warehouse_id IN (4,8,9) GROUP BY w.warehouse_id,m.item_id;
SELECT document_id,document_type,status,source_warehouse_id,destination_warehouse_id,created_at,posted_at,cancelled_at
FROM dbo.stock_documents WHERE source_warehouse_id IN (4,8,9) OR destination_warehouse_id IN (4,8,9);
SELECT request_id,status,requester_id,source_warehouse_id FROM dbo.material_requests WHERE source_warehouse_id IN (4,8,9);
SELECT fulfillment_id,request_id,warehouse_id,status,fulfilled_by,received_by FROM dbo.material_fulfillments WHERE warehouse_id IN (4,8,9);
SELECT stocktake_id,warehouse_id,status,created_at FROM dbo.stocktakes WHERE warehouse_id IN (4,8,9);
SELECT name FROM sys.tables WHERE name LIKE '%reserv%' OR name LIKE '%balance%' OR name LIKE '%stock%';
SELECT fk.name fk_name,OBJECT_NAME(fkc.parent_object_id) table_name,COL_NAME(fkc.parent_object_id,fkc.parent_column_id) column_name
FROM sys.foreign_key_columns fkc JOIN sys.foreign_keys fk ON fk.object_id=fkc.constraint_object_id WHERE fkc.referenced_object_id=OBJECT_ID('dbo.warehouses');
SELECT internal_request_id,status,request_type_id,requester_id,requester_department_id,receiving_department_id,completed_at,closed_at,cancellation_reason
FROM dbo.internal_requests WHERE internal_request_id IN (4,5,8,13,14);
SELECT event_id,internal_request_id,event_type,old_status,new_status,actor_id,occurred_at FROM dbo.internal_request_events WHERE internal_request_id=13;
SELECT assignment_id,internal_request_id,assignee_id,status FROM dbo.internal_request_assignments WHERE internal_request_id=13;
SELECT shift_id,department_id,shift_date,status,scheduled_start_at,scheduled_end_at FROM dbo.work_shifts WHERE shift_id IN (4,5);
SELECT a.shift_assignment_id,a.shift_id,a.user_id,a.assignment_status,a.confirmed_at,att.actual_check_in_at,att.actual_check_out_at,att.attendance_status
FROM dbo.shift_assignments a LEFT JOIN dbo.shift_attendance att ON att.shift_assignment_id=a.shift_assignment_id WHERE a.shift_id IN (4,5) OR a.user_id IN (11,17,18,20);
SELECT u.user_id,COUNT(s.session_id) total_sessions,SUM(CASE WHEN s.session_id IS NOT NULL AND s.revoked_at IS NULL THEN 1 ELSE 0 END) unrevoked_sessions
FROM dbo.users u LEFT JOIN dbo.user_sessions s ON s.user_id=u.user_id WHERE u.user_id IN (11,17,18,20) GROUP BY u.user_id;
SELECT name,definition FROM sys.check_constraints WHERE parent_object_id IN
(OBJECT_ID('dbo.users'),OBJECT_ID('dbo.work_shifts'),OBJECT_ID('dbo.shift_assignments'),OBJECT_ID('dbo.shift_attendance'),OBJECT_ID('dbo.internal_requests'),OBJECT_ID('dbo.internal_request_events'),OBJECT_ID('dbo.audit_logs'),OBJECT_ID('dbo.stock_documents'),OBJECT_ID('dbo.stocktakes'));
SELECT OBJECT_NAME(parent_id) table_name,name,is_disabled,OBJECT_DEFINITION(object_id) definition FROM sys.triggers WHERE is_ms_shipped=0;
