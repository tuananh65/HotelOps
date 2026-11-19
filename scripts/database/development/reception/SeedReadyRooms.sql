SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME()<>'HotelOpsDB' OR @@TRANCOUNT<>1 THROW 51999,'Working database and caller transaction required',1;
DECLARE @branch bigint=(SELECT primary_branch_id FROM dbo.users WHERE username='huy.dinh' AND account_status='ACTIVE'),
 @supervisor bigint=(SELECT user_id FROM dbo.users WHERE username='mai.tran' AND account_status='ACTIVE'),
 @now datetime2=SYSUTCDATETIME(),@floor bigint,@type bigint,@list bigint,@version bigint,@room bigint,@task bigint,@inspection bigint,
 @cleanType bigint=(SELECT reference_id FROM dbo.reference_values WHERE group_code='CLEANING_TYPE' AND value_code='CHECKOUT' AND is_active=1);
IF @branch IS NULL OR @supervisor IS NULL OR @cleanType IS NULL THROW 51999,'Demo participants/configuration missing',1;
IF NOT EXISTS(SELECT 1 FROM dbo.user_roles r JOIN dbo.roles role ON role.role_id=r.role_id
 WHERE r.user_id=@supervisor AND r.branch_id=@branch AND r.is_active=1 AND role.is_active=1 AND role.role_code='HK_SUPERVISOR'
 AND r.assigned_at<=@now AND (r.expires_at IS NULL OR r.expires_at>@now)) THROW 51999,'Demo supervisor branch mismatch',1;
SELECT @type=room_type_id,@list=default_checklist_id FROM dbo.room_types WHERE type_code='DEMO-RECEPTION-READY' AND branch_id=@branch AND is_active=1;
IF @type IS NULL AND EXISTS(SELECT 1 FROM dbo.room_types WHERE type_code='DEMO-RECEPTION-READY') THROW 51999,'Conflicting demo room type; preserve existing data',1;
SELECT TOP(1) @floor=floor_id FROM dbo.floors WHERE branch_id=@branch AND is_active=1 ORDER BY floor_number,floor_id;
IF @floor IS NULL THROW 51999,'No active floor',1;
IF @type IS NULL
BEGIN
INSERT dbo.inspection_checklists(checklist_code,checklist_name,description,created_at)
VALUES('DEMO-RECEPTION-READY',N'[DEMO] Kiểm tra phòng sẵn sàng',N'Dữ liệu mô phỏng theo yêu cầu; không phải nghiệm thu phòng thật.',@now);
SET @list=SCOPE_IDENTITY();
INSERT dbo.inspection_checklist_versions(checklist_id,version_no,status) VALUES(@list,1,'DRAFT'); SET @version=SCOPE_IDENTITY();
INSERT dbo.inspection_checklist_items(version_id,criterion,is_required,sort_order)
VALUES(@version,N'[DEMO] Phòng sạch và đủ đồ dùng',1,1),(@version,N'[DEMO] Thiết bị hoạt động bình thường',1,2);
UPDATE dbo.inspection_checklist_versions SET status='PUBLISHED',published_by=@supervisor,effective_at=@now WHERE version_id=@version;
INSERT dbo.room_types(type_code,type_name,description,standard_capacity,branch_id,standard_cleaning_minutes,default_checklist_id)
VALUES('DEMO-RECEPTION-READY',N'[DEMO] Phòng sẵn sàng cho Lễ tân',N'Phòng mô phỏng, không bán cho khách thật.',2,@branch,30,@list); SET @type=SCOPE_IDENTITY();
-- Inspection list is intentionally demonstration-only and attached to this new room type.
UPDATE dbo.inspection_checklists SET room_type_id=@type WHERE checklist_id=@list;
INSERT dbo.cleaning_item_definitions(room_type_id,label,is_required,sort_order)
VALUES(@type,N'[DEMO] Vệ sinh phòng và bổ sung đồ dùng',1,1),(@type,N'[DEMO] Kiểm tra thiết bị',1,2);
END;
SELECT @version=version_id FROM dbo.inspection_checklist_versions WHERE checklist_id=@list AND status='PUBLISHED';
IF @version IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.cleaning_item_definitions WHERE room_type_id=@type)
 THROW 51999,'Existing demo checklist is incomplete; do not infer replacements',1;
DECLARE @n int=1,@number varchar(30),@code varchar(40);
WHILE @n<=5
BEGIN
 SET @number='DEMO-LT-0'+CONVERT(varchar(1),@n); SET @code='DEMO-LT-20261120-0'+CONVERT(varchar(1),@n);
 IF EXISTS(SELECT 1 FROM dbo.rooms WHERE branch_id=@branch AND room_number=@number AND room_type_id<>@type)
  THROW 51999,'Demo room number conflicts with another type',1;
 IF EXISTS(SELECT 1 FROM dbo.rooms WHERE branch_id=@branch AND room_number=@number)
 BEGIN SET @n+=1; CONTINUE; END;
 INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status,description,created_at)
 VALUES(@branch,@floor,@type,@number,'INSPECTION',N'[DEMO] Phòng mô phỏng đã vệ sinh và kiểm tra đạt; dùng thử thao tác nhận khách.',DATEADD(minute,-60,@now));
 SET @room=SCOPE_IDENTITY();
 INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,created_by,workflow_status,created_at,actual_start_at,actual_end_at,completion_note)
 VALUES(@code,@room,@cleanType,'IN_PROGRESS',@supervisor,'PENDING_INSPECTION',DATEADD(minute,-45,@now),DATEADD(minute,-40,@now),DATEADD(minute,-10,@now),N'[DEMO] Kết quả vệ sinh mô phỏng, không phải công việc của nhân viên.');
 SET @task=SCOPE_IDENTITY();
 INSERT dbo.cleaning_progress_versions(cleaning_task_id) VALUES(@task);
 INSERT dbo.cleaning_task_items(cleaning_task_id,source_item_id,label,is_required,sort_order,is_done,note)
 SELECT @task,item_id,label,is_required,sort_order,1,N'[DEMO] Đã thực hiện trong kịch bản mô phỏng.' FROM dbo.cleaning_item_definitions WHERE room_type_id=@type;
 INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES(@room,@task);
 INSERT dbo.room_inspections(inspection_no,room_id,cleaning_task_id,version_id,inspector_user_id,status,workflow_status,created_at,summary_note)
 VALUES(@code,@room,@task,@version,@supervisor,'IN_PROGRESS','INSPECTING',@now,N'[DEMO] Kết quả kiểm tra mô phỏng theo yêu cầu.');
 SET @inspection=SCOPE_IDENTITY();
 INSERT dbo.inspection_results(inspection_id,checklist_item_id,outcome,note,inspected_at)
 SELECT @inspection,item_id,'PASS',N'[DEMO] Đạt trong kịch bản mô phỏng.',@now FROM dbo.inspection_checklist_items WHERE version_id=@version;
 UPDATE dbo.room_inspections SET status='COMPLETED',workflow_status='PASSED',result='PASS',inspected_at=@now WHERE inspection_id=@inspection;
 UPDATE dbo.cleaning_tasks SET status='COMPLETED',workflow_status='COMPLETED' WHERE cleaning_task_id=@task;
 UPDATE dbo.rooms SET operational_status='READY',updated_at=@now WHERE room_id=@room;
 INSERT dbo.room_status_history(room_id,from_status,to_status,trigger_type,trigger_id,changed_at,reason,effective_from,effective_to)
 VALUES(@room,'INSPECTION','READY','INSPECTION',@inspection,@now,N'[DEMO] Nạp dữ liệu mô phỏng phòng sẵn sàng theo yêu cầu.','PENDING_INSPECTION','READY');
 INSERT dbo.audit_logs(branch_id,entity_type,entity_id,action,new_values,request_id,occurred_at)
 VALUES(@branch,'rooms',CONVERT(varchar(30),@room),'CREATE',N'{"operation":"SEED_DEMO_READY_ROOM","synthetic":true}','reception-demo-ready-20261120',@now);
 SET @n+=1;
END;
SELECT r.room_id,r.room_number,r.operational_status FROM dbo.rooms r WHERE r.room_type_id=@type ORDER BY r.room_number;
