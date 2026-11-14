-- Runs only after SprintApproveFixture's exact identity guard in the same rollback transaction.
SET NOCOUNT ON;
DECLARE @actor bigint=(SELECT user_id FROM dbo.users WHERE username='D3-EVIDENCE');
DECLARE @original bigint=(SELECT inspection_id FROM dbo.room_inspections WHERE inspection_no='D3-EVIDENCE');
DECLARE @task bigint,@room bigint,@version bigint;
SELECT @task=cleaning_task_id,@room=room_id,@version=version_id FROM dbo.room_inspections WHERE inspection_id=@original;
IF @@TRANCOUNT<>1 OR @actor IS NULL OR @original IS NULL THROW 51997,'Owned caller fixture required',1;
UPDATE dbo.room_inspections SET workflow_status='INSPECTING' WHERE inspection_id=@original;
UPDATE dbo.inspection_results SET outcome='FAIL' WHERE inspection_id=@original;
INSERT dbo.inspection_defects(inspection_id,checklist_item_id,description)
SELECT @original,checklist_item_id,N'Synthetic original defect' FROM dbo.inspection_results WHERE inspection_id=@original;
DECLARE @defect bigint=SCOPE_IDENTITY();
UPDATE dbo.room_inspections SET workflow_status='FAILED',status='COMPLETED',result='FAIL',inspected_at=SYSUTCDATETIME() WHERE inspection_id=@original;
UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED' WHERE cleaning_task_id=@task;
INSERT dbo.users(employee_code,username,email,full_name,created_at)
VALUES('D3-REMEDIATOR','D3-REMEDIATOR','d3-remediator@example.invalid',N'Synthetic remediator',SYSUTCDATETIME());
DECLARE @cleaner bigint=SCOPE_IDENTITY();
INSERT dbo.cleaning_reworks(cleaning_task_id,inspection_id,assignee_user_id,reason,status,workflow_status,requested_at)
VALUES(@task,@original,@cleaner,N'Synthetic rework','IN_PROGRESS','PENDING_REINSPECTION',SYSUTCDATETIME());
DECLARE @rework bigint=SCOPE_IDENTITY();
INSERT dbo.cleaning_rework_defects(rework_id,defect_id,remediation_note,remediated_by,remediated_at)
VALUES(@rework,@defect,N'Synthetic remediation',@cleaner,SYSUTCDATETIME());
IF @differentVersion=1
BEGIN
 DECLARE @checklist bigint=(SELECT checklist_id FROM dbo.inspection_checklist_versions WHERE version_id=@version);
 INSERT dbo.inspection_checklist_versions(checklist_id,version_no,status) VALUES(@checklist,2,'DRAFT');
 SET @version=SCOPE_IDENTITY();
 INSERT dbo.inspection_checklist_items(version_id,criterion,is_required,sort_order) VALUES(@version,N'Synthetic revised criterion',1,1);
 UPDATE dbo.inspection_checklist_versions SET status='PUBLISHED',published_by=@actor,effective_at=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE version_id=@version;
END;
INSERT dbo.room_inspections(inspection_no,room_id,cleaning_task_id,version_id,inspector_user_id,status,workflow_status,created_at)
VALUES('D3-REINSPECT',@room,@task,@version,@actor,'IN_PROGRESS','INSPECTING',SYSUTCDATETIME());
DECLARE @inspection bigint=SCOPE_IDENTITY();
INSERT dbo.inspection_results(inspection_id,checklist_item_id,outcome)
SELECT @inspection,item_id,'PASS' FROM dbo.inspection_checklist_items WHERE version_id=@version;
SET NOCOUNT OFF;
SELECT @inspection AS InspectionId,@rework AS ReworkId,@defect AS DefectId;
