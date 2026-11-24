/* Administrative reset proposal. Run only using Reset-DemoReady.ps1 after a verified backup.
   No synthetic cleaning/inspection results. A reset event establishes a clean baseline,
   valid only for its exact turnover with no current cleaning pointer. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;
DECLARE @now datetime2(7)=SYSUTCDATETIME(),@actor bigint,@operation uniqueidentifier=NEWID();
SELECT TOP(1) @actor=u.user_id FROM users u JOIN user_roles ur ON ur.user_id=u.user_id JOIN roles r ON r.role_id=ur.role_id
 JOIN role_permissions rp ON rp.role_id=r.role_id JOIN permissions p ON p.permission_id=rp.permission_id
 WHERE p.code='USER_MANAGE' AND rp.data_scope='ALL' AND u.account_status='ACTIVE' AND r.is_active=1
 AND ur.revoked_at IS NULL AND (ur.expires_at IS NULL OR ur.expires_at>@now) ORDER BY u.user_id;
IF @actor IS NULL THROW 51400,'No active administrator for reset audit.',1;
SELECT work_id+0 AS work_id INTO #demoWork FROM work_items WITH(UPDLOCK,HOLDLOCK) WHERE title LIKE '[[]DEMO]%' OR work_no LIKE 'DEMO%' OR description LIKE 'DEMO %';
WHILE 1=1 BEGIN
 INSERT #demoWork SELECT w.work_id FROM work_items w JOIN #demoWork p ON p.work_id=w.parent_work_id WHERE NOT EXISTS(SELECT 1 FROM #demoWork d WHERE d.work_id=w.work_id);
 IF @@ROWCOUNT=0 BREAK;
END;
SELECT document_id+0 AS document_id INTO #demoDocs FROM inventory_documents WITH(UPDLOCK,HOLDLOCK) WHERE document_no LIKE 'DEMO%' OR reason LIKE 'DEMO %' OR source_work_id IN(SELECT work_id FROM #demoWork);
WHILE 1=1 BEGIN
 INSERT #demoDocs SELECT d.document_id FROM inventory_documents d WHERE (d.source_document_id IN(SELECT document_id FROM #demoDocs)
 OR EXISTS(SELECT 1 FROM inventory_document_lines line JOIN inventory_document_lines source ON source.line_id=line.source_line_id WHERE line.document_id=d.document_id AND source.document_id IN(SELECT document_id FROM #demoDocs)))
 AND NOT EXISTS(SELECT 1 FROM #demoDocs x WHERE x.document_id=d.document_id);
 IF @@ROWCOUNT=0 BREAK;
END;
IF EXISTS(SELECT 1 FROM work_items WHERE room_id IS NOT NULL AND kind IN('CLEANING','INSPECTION','REWORK') AND status NOT IN('COMPLETED','CANCELLED','PASSED','FAILED') AND work_id NOT IN(SELECT work_id FROM #demoWork))
 THROW 51401,'Non-demo active room work exists; review before reset.',1;
IF EXISTS(SELECT 1 FROM rooms WHERE is_active=0) THROW 51401,'Inactive rooms require explicit review before resetting all rooms.',1;
/* Readiness exception represents initialization, never a fabricated inspection. */
DECLARE @view nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.v_room_readiness'));
IF @view NOT LIKE '%ADMIN_BASELINE_RESET%'
BEGIN
 IF @view NOT LIKE '%WHEN cleaning.work_id IS NULL THEN ''PENDING_CLEANING''%' THROW 51402,'Unexpected readiness view definition.',1;
 SET @view=REPLACE(REPLACE(@view,'CREATE VIEW','ALTER VIEW'),'CREATE OR ALTER VIEW','ALTER VIEW');
 SET @view=REPLACE(@view,'WHEN cleaning.work_id IS NULL THEN ''PENDING_CLEANING''',
 'WHEN cleaning.work_id IS NULL AND room.current_cleaning_work_id IS NULL AND room.operational_status IN (''READY'',''AVAILABLE'',''OCCUPIED'') AND EXISTS(SELECT 1 FROM dbo.room_events reset_event WHERE reset_event.room_id=room.room_id AND reset_event.turnover_no=room.turnover_no AND reset_event.event_type=''ADMIN_BASELINE_RESET'') THEN ''READY'' WHEN cleaning.work_id IS NULL THEN ''PENDING_CLEANING''');
 EXEC sp_executesql @view;
END;
/* Only table-local history protection is suspended inside this locked transaction.
   Every original trigger state is restored before commit, and rollback restores DDL. */
SELECT object_id,name,parent_id INTO #triggers FROM sys.triggers WHERE is_disabled=0 AND parent_id IN(
 OBJECT_ID('work_events'),OBJECT_ID('work_intervals'),OBJECT_ID('work_checklist_items'),OBJECT_ID('inspection_findings'),
 OBJECT_ID('room_events'),OBJECT_ID('asset_events'),OBJECT_ID('inventory_documents'),OBJECT_ID('inventory_document_lines'),OBJECT_ID('stock_movements'),OBJECT_ID('document_events'));
DECLARE @sql nvarchar(max)='';
SELECT @sql=@sql+'DISABLE TRIGGER '+QUOTENAME(name)+' ON '+QUOTENAME(OBJECT_SCHEMA_NAME(parent_id))+'.'+QUOTENAME(OBJECT_NAME(parent_id))+';' FROM #triggers;
EXEC sp_executesql @sql;
UPDATE rooms SET operational_status='DIRTY',turnover_no=turnover_no+1,current_cleaning_work_id=NULL;
UPDATE room_blocks SET released_at=@now,released_by=@actor WHERE released_at IS NULL OR released_at>@now;
UPDATE room_blocks SET source_work_id=NULL WHERE source_work_id IN(SELECT work_id FROM #demoWork);
UPDATE room_events SET source_work_id=NULL WHERE source_work_id IN(SELECT work_id FROM #demoWork);
DELETE FROM notifications WHERE work_id IN(SELECT work_id FROM #demoWork) OR document_id IN(SELECT document_id FROM #demoDocs);
DELETE FROM attachments WHERE work_id IN(SELECT work_id FROM #demoWork) OR document_id IN(SELECT document_id FROM #demoDocs)
 OR finding_id IN(SELECT finding_id FROM inspection_findings WHERE inspection_work_id IN(SELECT work_id FROM #demoWork) OR rework_work_id IN(SELECT work_id FROM #demoWork));
DELETE FROM asset_events WHERE source_work_id IN(SELECT work_id FROM #demoWork);
DELETE FROM inspection_findings WHERE inspection_work_id IN(SELECT work_id FROM #demoWork) OR rework_work_id IN(SELECT work_id FROM #demoWork);
DELETE FROM work_checklist_items WHERE work_id IN(SELECT work_id FROM #demoWork);
DELETE FROM work_intervals WHERE work_id IN(SELECT work_id FROM #demoWork);
DELETE FROM work_assignments WHERE work_id IN(SELECT work_id FROM #demoWork);
DELETE FROM work_events WHERE work_id IN(SELECT work_id FROM #demoWork);
DELETE FROM work_links WHERE source_work_id IN(SELECT work_id FROM #demoWork) OR target_work_id IN(SELECT work_id FROM #demoWork);
DELETE FROM document_events WHERE document_id IN(SELECT document_id FROM #demoDocs);
DELETE FROM stock_movements WHERE line_id IN(SELECT line_id FROM inventory_document_lines WHERE document_id IN(SELECT document_id FROM #demoDocs));
DELETE FROM inventory_document_lines WHERE document_id IN(SELECT document_id FROM #demoDocs);
DELETE FROM inventory_documents WHERE document_id IN(SELECT document_id FROM #demoDocs);
DELETE FROM work_items WHERE work_id IN(SELECT work_id FROM #demoWork);
INSERT room_events(room_id,branch_id,turnover_no,event_type,from_status,to_status,actor_user_id,operation_key,reason,occurred_at)
 SELECT room_id,branch_id,turnover_no,'ADMIN_BASELINE_RESET','DIRTY','READY',@actor,@operation,N'Khởi tạo lại trạng thái phòng theo yêu cầu quản trị; không phải kết quả nghiệm thu vệ sinh.',@now FROM rooms;
UPDATE rooms SET operational_status='READY';
SET @sql='';SELECT @sql=@sql+'ENABLE TRIGGER '+QUOTENAME(name)+' ON '+QUOTENAME(OBJECT_SCHEMA_NAME(parent_id))+'.'+QUOTENAME(OBJECT_NAME(parent_id))+';' FROM #triggers;
EXEC sp_executesql @sql;
IF EXISTS(SELECT 1 FROM v_room_readiness WHERE readiness<>'READY' OR is_occupied=1) THROW 51403,'Not all rooms reset to ready.',1;
IF EXISTS(SELECT 1 FROM v_stock_balances WHERE base_qty<0) THROW 51403,'Demo cleanup would leave negative inventory.',1;
INSERT audit_logs(actor_user_id,entity_type,entity_key,action,operation_key,after_json,occurred_at)
 VALUES(@actor,'AdministrativeReset','ALL_ROOMS','RESET_DEMO_READY',@operation,
 (SELECT (SELECT COUNT(*) FROM #demoWork) deletedDemoWorks,(SELECT COUNT(*) FROM #demoDocs) deletedDemoDocuments,(SELECT COUNT(*) FROM rooms) readyRooms FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@now);
SELECT (SELECT COUNT(*) FROM #demoWork) deletedDemoWorks,(SELECT COUNT(*) FROM #demoDocs) deletedDemoDocuments,(SELECT COUNT(*) FROM rooms) readyRooms;
COMMIT;
