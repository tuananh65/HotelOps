-- Physical trigger checks with synthetic metadata only; no evidence bytes or workflow acceptance.
SET NOCOUNT ON; SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON;
SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run'
   AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51992,'Exact sprint-owned database required',1;
IF EXISTS(SELECT 1 FROM dbo.users) OR EXISTS(SELECT 1 FROM dbo.branches) OR EXISTS(SELECT 1 FROM dbo.attachments)
 THROW 51992,'Empty synthetic fixture required',1;
DECLARE @foreignTest bit=0;
BEGIN TRY
 BEGIN TRANSACTION;
 INSERT dbo.users(employee_code,username,email,full_name,created_at) VALUES('D3-EVIDENCE','D3-EVIDENCE','d3-evidence@example.invalid',N'Synthetic evidence probe',SYSUTCDATETIME());
 DECLARE @actor bigint=SCOPE_IDENTITY();
 INSERT dbo.branches(branch_code,branch_name) VALUES('D3-EVIDENCE',N'Synthetic evidence branch');
 DECLARE @branch bigint=SCOPE_IDENTITY();
 INSERT dbo.floors(branch_id,floor_code,floor_name) VALUES(@branch,'D3-F',N'Synthetic floor');
 DECLARE @floor bigint=SCOPE_IDENTITY();
 INSERT dbo.room_types(type_code,type_name,standard_capacity) VALUES('D3-RT',N'Synthetic room type',1);
 DECLARE @type bigint=SCOPE_IDENTITY();
 INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status) VALUES(@branch,@floor,@type,'D3-001','INSPECTION');
 DECLARE @room bigint=SCOPE_IDENTITY();
 INSERT dbo.reference_values(group_code,value_code,display_name) VALUES('CLEANING_TYPE','D3-SYNTHETIC',N'Synthetic cleaning type');
 DECLARE @cleanType bigint=SCOPE_IDENTITY();
 INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,created_by,workflow_status,created_at) VALUES('D3-EVIDENCE',@room,@cleanType,'IN_PROGRESS',@actor,'PENDING_INSPECTION',SYSUTCDATETIME());
 DECLARE @task bigint=SCOPE_IDENTITY();
 INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES(@room,@task);
 INSERT dbo.inspection_checklists(checklist_code,checklist_name,room_type_id) VALUES('D3-EVIDENCE',N'Synthetic checklist',@type);
 DECLARE @list bigint=SCOPE_IDENTITY();
 INSERT dbo.inspection_checklist_versions(checklist_id,version_no,status) VALUES(@list,1,'DRAFT');
 DECLARE @version bigint=SCOPE_IDENTITY();
 INSERT dbo.inspection_checklist_items(version_id,criterion,is_required,sort_order) VALUES(@version,N'Synthetic criterion',1,1);
 DECLARE @item bigint=SCOPE_IDENTITY();
 UPDATE dbo.inspection_checklist_versions SET status='PUBLISHED',published_by=@actor,effective_at=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE version_id=@version;
 INSERT dbo.room_inspections(inspection_no,room_id,cleaning_task_id,version_id,inspector_user_id,status,workflow_status,created_at)
 VALUES('D3-EVIDENCE',@room,@task,@version,@actor,'IN_PROGRESS','INSPECTING',SYSUTCDATETIME());
 DECLARE @inspection bigint=SCOPE_IDENTITY();
 INSERT dbo.inspection_results(inspection_id,checklist_item_id,outcome) VALUES(@inspection,@item,'PASS');
 DECLARE @result bigint=SCOPE_IDENTITY();
 DECLARE @before binary(8)=(SELECT row_version FROM dbo.room_inspections WHERE inspection_id=@inspection);
 INSERT dbo.attachments(entity_type,entity_id,file_name,storage_key,uploaded_by,uploaded_at)
 VALUES('INSPECTION',@inspection,'synthetic.txt','sprint142a-synthetic-no-file',@actor,SYSUTCDATETIME());
 DECLARE @file bigint=SCOPE_IDENTITY();
 IF @before=(SELECT row_version FROM dbo.room_inspections WHERE inspection_id=@inspection) THROW 51993,'Attachment insert did not invalidate revision',1;
 PRINT 'PASS attachment insert invalidates inspection revision';
 SET @before=(SELECT row_version FROM dbo.room_inspections WHERE inspection_id=@inspection);
 INSERT dbo.inspection_result_attachments(result_id,attachment_id) VALUES(@result,@file);
 IF @before=(SELECT row_version FROM dbo.room_inspections WHERE inspection_id=@inspection) THROW 51993,'Evidence link did not invalidate revision',1;
 PRINT 'PASS evidence link invalidates inspection revision';
 SET @before=(SELECT row_version FROM dbo.room_inspections WHERE inspection_id=@inspection);
 DELETE dbo.inspection_result_attachments WHERE result_id=@result AND attachment_id=@file;
 IF @before=(SELECT row_version FROM dbo.room_inspections WHERE inspection_id=@inspection) THROW 51993,'Evidence unlink did not invalidate revision',1;
 PRINT 'PASS evidence unlink invalidates inspection revision';
 SET @before=(SELECT row_version FROM dbo.room_inspections WHERE inspection_id=@inspection);
 UPDATE dbo.attachments SET deleted_at=SYSUTCDATETIME() WHERE attachment_id=@file;
 IF @before=(SELECT row_version FROM dbo.room_inspections WHERE inspection_id=@inspection) THROW 51993,'Soft deletion did not invalidate revision',1;
 PRINT 'PASS evidence soft deletion invalidates inspection revision';
 UPDATE dbo.attachments SET deleted_at=NULL,entity_type='ROOM',entity_id=@room WHERE attachment_id=@file;
 SET @foreignTest=1;
 INSERT dbo.inspection_result_attachments(result_id,attachment_id) VALUES(@result,@file);
 THROW 51993,'Foreign evidence association accepted',1;
END TRY
BEGIN CATCH
 DECLARE @error int=ERROR_NUMBER();
 IF XACT_STATE()<>0 ROLLBACK;
 IF @foreignTest<>1 OR @error<>51731 THROW;
 PRINT 'PASS foreign evidence association rejected';
END CATCH;
IF EXISTS(SELECT 1 FROM dbo.users) OR EXISTS(SELECT 1 FROM dbo.branches) OR EXISTS(SELECT 1 FROM dbo.attachments)
 OR EXISTS(SELECT 1 FROM dbo.room_inspections) OR EXISTS(SELECT 1 FROM dbo.cleaning_tasks)
 THROW 51993,'Synthetic rollback cleanup failed',1;
PRINT 'PASS synthetic transaction rollback cleanup';
