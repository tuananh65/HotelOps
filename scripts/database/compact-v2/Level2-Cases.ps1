# Loaded by Test-Compact.ps1; shares only rollback fixture SQL strings, never a live transaction.
$open=@'
DECLARE @t datetime2=DATEADD(SECOND,1,(SELECT assigned_at FROM dbo.work_assignments WHERE assignment_id=@day));
INSERT dbo.work_intervals(branch_id,work_id,assignment_id,user_id,opened_by,opened_at,effective_started_at)
 VALUES(@b,@clean,@day,@u,@u,@t,@t);
'@
$pass=@'
UPDATE dbo.work_checklist_items SET outcome='PASS',checked_by=@u2,checked_at=SYSUTCDATETIME() WHERE work_checklist_item_id=@wi;
UPDATE dbo.work_items SET status='CANCELLED' WHERE work_id=@rework;
UPDATE dbo.work_items SET status='COMPLETED' WHERE work_id=@clean;
UPDATE dbo.work_items SET status='PASSED',result='PASS',verified_by=@u2,verified_at=SYSUTCDATETIME() WHERE work_id=@inspect;
UPDATE dbo.rooms SET current_cleaning_work_id=@clean,operational_status='READY' WHERE room_id=@room;
'@
$material=$post+@'
DECLARE @store bigint,@mr bigint,@mrline bigint,@issue bigint,@issueline bigint;
INSERT dbo.users(employee_code,username,full_name,employment_status,account_status)
 VALUES('QA_STORE',N'qa_store',N'Storekeeper','ACTIVE','INACTIVE'); SET @store=SCOPE_IDENTITY();
INSERT dbo.inventory_documents(branch_id,document_no,kind,status,created_by,approved_by,approved_at)
 VALUES(@b,'MR1','MATERIAL_REQUEST','APPROVED',@u,@u2,SYSUTCDATETIME()); SET @mr=SCOPE_IDENTITY();
INSERT dbo.inventory_document_lines(branch_id,document_id,line_no,item_id,unit_id,conversion_factor,requested_qty,approved_qty)
 VALUES(@b,@mr,1,@item,@unit,1,6,6); SET @mrline=SCOPE_IDENTITY();
INSERT dbo.inventory_documents(branch_id,document_no,kind,status,source_warehouse_id,source_document_id,created_by)
 VALUES(@b,'ISS1','ISSUE','DRAFT',@wh,@mr,@store); SET @issue=SCOPE_IDENTITY();
INSERT dbo.inventory_document_lines(branch_id,document_id,line_no,item_id,unit_id,conversion_factor,actual_qty,source_line_id)
 VALUES(@b,@issue,1,@item,@unit,1,2,@mrline); SET @issueline=SCOPE_IDENTITY();
UPDATE dbo.inventory_documents SET status='POSTED',posted_by=@store,posted_at=SYSUTCDATETIME() WHERE document_id=@issue;
'@
@(
 @{name='no workday shift or time-correction table';sql="IF OBJECT_ID('dbo.workdays') IS NOT NULL OR OBJECT_ID('dbo.shifts') IS NOT NULL OR OBJECT_ID('dbo.time_corrections') IS NOT NULL THROW 51990,'Removed tables remain.',1;";error=0},
 @{name='start and pause work without a workday';sql=($open+"UPDATE dbo.work_intervals SET closed_by=@u,closed_at=DATEADD(MINUTE,10,@t),effective_ended_at=DATEADD(MINUTE,10,@t),close_reason='PAUSE' WHERE assignment_id=@day; IF (SELECT completed_seconds FROM dbo.v_work_duration WHERE work_id=@clean)<>600 THROW 51990,'Duration incorrect.',1;");error=0},
 @{name='assignment user identity preserved';sql="INSERT dbo.work_intervals(branch_id,work_id,assignment_id,user_id,opened_by,opened_at,effective_started_at) VALUES(@b,@clean,@day,@u2,@u2,SYSUTCDATETIME(),SYSUTCDATETIME());";error=547},
 @{name='one open task interval per user';sql=($open+"INSERT dbo.work_intervals(branch_id,work_id,assignment_id,user_id,opened_by,opened_at,effective_started_at) VALUES(@b,@clean,@day,@u,@u,@t,@t);");error=2601},
 @{name='closed task time immutable';sql=($open+"UPDATE dbo.work_intervals SET closed_by=@u,closed_at=DATEADD(MINUTE,10,@t),effective_ended_at=DATEADD(MINUTE,10,@t),close_reason='COMPLETE' WHERE assignment_id=@day; UPDATE dbo.work_intervals SET effective_ended_at=DATEADD(MINUTE,20,@t) WHERE assignment_id=@day;");error=51201},
 @{name='overlapping closed task intervals rejected';sql=($open+"UPDATE dbo.work_intervals SET closed_by=@u,closed_at=DATEADD(MINUTE,10,@t),effective_ended_at=DATEADD(MINUTE,10,@t),close_reason='PAUSE' WHERE assignment_id=@day; INSERT dbo.work_intervals(branch_id,work_id,assignment_id,user_id,opened_by,opened_at,effective_started_at,closed_by,closed_at,effective_ended_at,close_reason) VALUES(@b,@clean,@day,@u,@u,DATEADD(MINUTE,5,@t),DATEADD(MINUTE,5,@t),@u,DATEADD(MINUTE,15,@t),DATEADD(MINUTE,15,@t),'COMPLETE');");error=51202},
 @{name='cannot end assignment around open execution';sql=($open+"UPDATE dbo.work_assignments SET assignment_status='REASSIGNED',ended_at=DATEADD(MINUTE,20,@t) WHERE assignment_id=@day;");error=51203},
 @{name='cannot submit work with running time';sql=($open+"UPDATE dbo.work_items SET status='PENDING_INSPECTION' WHERE work_id=@clean;");error=51205},
 @{name='room cannot become ready just from cleaning complete';sql="UPDATE dbo.work_items SET status='COMPLETED' WHERE work_id=@clean; UPDATE dbo.rooms SET current_cleaning_work_id=@clean,operational_status='READY' WHERE room_id=@room;";error=51209},
 @{name='inspection needs completed checklist';sql="UPDATE dbo.work_items SET status='PASSED',result='PASS',verified_by=@u2,verified_at=SYSUTCDATETIME() WHERE work_id=@inspect;";error=51212},
 @{name='cleaner cannot verify their own work';sql="UPDATE dbo.work_checklist_items SET outcome='PASS',checked_by=@u,checked_at=SYSUTCDATETIME() WHERE work_checklist_item_id=@wi; UPDATE dbo.work_items SET status='PASSED',result='PASS',verified_by=@u,verified_at=SYSUTCDATETIME() WHERE work_id=@inspect;";error=51204},
 @{name='independent inspection permits ready and arrival';sql=($pass+"UPDATE dbo.rooms SET operational_status='OCCUPIED' WHERE room_id=@room; IF NOT EXISTS(SELECT 1 FROM dbo.v_room_readiness WHERE room_id=@room AND readiness='READY' AND is_occupied=1) THROW 51990,'Occupancy and readiness conflated.',1;");error=0},
 @{name='final inspection checklist immutable';sql=($pass+"UPDATE dbo.work_checklist_items SET outcome='FAIL' WHERE work_checklist_item_id=@wi;");error=51213},
 @{name='blocking incident prevents arrival';sql=($pass+"INSERT dbo.room_blocks(branch_id,room_id,reason,blocked_by) VALUES(@b,@room,N'Blocking incident',@u); UPDATE dbo.rooms SET operational_status='OCCUPIED' WHERE room_id=@room;");error=51209},
 @{name='departure invalidates previous cycle';sql=($pass+"UPDATE dbo.rooms SET operational_status='OCCUPIED' WHERE room_id=@room; UPDATE dbo.rooms SET operational_status='DIRTY',turnover_no=turnover_no+1,current_cleaning_work_id=NULL WHERE room_id=@room; IF NOT EXISTS(SELECT 1 FROM dbo.v_room_readiness WHERE room_id=@room AND readiness='PENDING_CLEANING') THROW 51990,'Old inspection remained effective.',1;");error=0},
 @{name='departure without turnover invalidation rejected';sql=($pass+"UPDATE dbo.rooms SET operational_status='OCCUPIED' WHERE room_id=@room; UPDATE dbo.rooms SET operational_status='DIRTY' WHERE room_id=@room;");error=51209},
 @{name='explicit backlog handover confirmation retained';sql=@'
INSERT dbo.work_events(branch_id,work_id,event_type,actor_user_id,recipient_user_id,operation_key,reason)
 VALUES(@b,@clean,'HANDOVER_OFFERED',@u,@u2,NEWID(),N'Outstanding room cleaning');
DECLARE @offer bigint=SCOPE_IDENTITY();
INSERT dbo.work_events(branch_id,work_id,event_type,actor_user_id,related_event_id,operation_key)
 VALUES(@b,@clean,'HANDOVER_ACCEPTED',@u2,@offer,NEWID());
IF NOT EXISTS(SELECT 1 FROM dbo.v_backlog_handovers WHERE work_event_id=@offer AND decision='HANDOVER_ACCEPTED') THROW 51990,'Handover not visible.',1;
'@;error=0},
 @{name='handover cannot be acknowledged by sender';sql=@'
INSERT dbo.work_events(branch_id,work_id,event_type,actor_user_id,recipient_user_id,operation_key,reason)
 VALUES(@b,@clean,'HANDOVER_OFFERED',@u,@u2,NEWID(),N'Outstanding room cleaning');
DECLARE @offer bigint=SCOPE_IDENTITY();
INSERT dbo.work_events(branch_id,work_id,event_type,actor_user_id,related_event_id,operation_key)
 VALUES(@b,@clean,'HANDOVER_ACCEPTED',@u,@offer,NEWID());
'@;error=51208},
 @{name='internal request cannot complete unfinished linked task';sql=@'
INSERT dbo.work_items(branch_id,work_no,kind,status,title,created_by) VALUES(@b,'IR1','INTERNAL_REQUEST','IN_PROGRESS',N'Request',@u);
DECLARE @ir bigint=SCOPE_IDENTITY();
INSERT dbo.work_links(branch_id,source_work_id,target_work_id,relation,created_by) VALUES(@b,@ir,@clean,'DEPENDS_ON',@u);
UPDATE dbo.work_items SET status='COMPLETED' WHERE work_id=@ir;
'@;error=51207},
 @{name='partial issue computes remainder without duplicate entry';sql=($material+"INSERT dbo.stock_movements(branch_id,line_id,warehouse_id,item_id,signed_base_qty,created_by) VALUES(@b,@issueline,@wh,@item,-2,@store); IF NOT EXISTS(SELECT 1 FROM dbo.v_material_fulfillment WHERE line_id=@mrline AND issued_base_qty=2 AND missing_base_qty=4) THROW 51990,'Remainder incorrect.',1;");error=0},
 @{name='approver cannot issue approved material';sql=($material+"INSERT dbo.stock_movements(branch_id,line_id,warehouse_id,item_id,signed_base_qty,created_by) VALUES(@b,@issueline,@wh,@item,-2,@u2);");error=51210},
 @{name='issue cannot exceed approved amount despite stock';sql=($material+"UPDATE dbo.inventory_document_lines SET actual_qty=7 WHERE line_id=@issueline; INSERT dbo.stock_movements(branch_id,line_id,warehouse_id,item_id,signed_base_qty,created_by) VALUES(@b,@issueline,@wh,@item,-7,@store);");error=51211},
 @{name='cannot rewrite fulfilled approval quantities';sql=($material+"INSERT dbo.stock_movements(branch_id,line_id,warehouse_id,item_id,signed_base_qty,created_by) VALUES(@b,@issueline,@wh,@item,-2,@store); UPDATE dbo.inventory_document_lines SET approved_qty=99 WHERE line_id=@mrline;");error=51214},
 @{name='cannot add findings to passed inspection';sql=($pass+"INSERT dbo.inspection_findings(branch_id,inspection_work_id,description) VALUES(@b,@inspect,N'New unreviewed finding');");error=51215}
)
