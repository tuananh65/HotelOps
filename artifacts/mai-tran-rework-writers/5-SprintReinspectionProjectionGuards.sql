-- Approved rework workflow package. Execute only through the pinned transactional installer.
SET XACT_ABORT ON;
IF @@TRANCOUNT<>1 OR CONVERT(nvarchar(128),SESSION_CONTEXT(N'ReworkWriter.Database')) IS NULL
 OR DB_NAME()<>CONVERT(nvarchar(128),SESSION_CONTEXT(N'ReworkWriter.Database'))
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'ReworkWriter.DatabaseGuid')))
 THROW 51999,'Pinned database transaction required',1;
DECLARE @results nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.TR_hk_results_v1')),
 @defects nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.TR_hk_defects_v1'));
IF @results NOT LIKE '%r.workflow_status<>''INSPECTING''%'
 OR @defects NOT LIKE '%r.workflow_status NOT IN(''INSPECTING'',''PENDING_CONCLUSION'')%'
 OR @results LIKE '%fe05_reinspection_bindings_v1%' OR @defects LIKE '%fe05_reinspection_bindings_v1%'
 THROW 51999,'Reviewed original trigger definitions required',1;
SET @results=REPLACE(@results,'CREATE TRIGGER','ALTER TRIGGER');
SET @results=REPLACE(@results,'r.workflow_status<>''INSPECTING''',
 N'(r.workflow_status<>''INSPECTING'' AND NOT(r.workflow_status=''ASSIGNED'' AND EXISTS(SELECT 1 FROM dbo.fe05_reinspection_responsibilities_v1 b
 WHERE b.inspection_id=r.inspection_id AND b.inspector_user_id=r.inspector_user_id)))');
SET @results=REPLACE(@results,'SET NOCOUNT ON;',N'SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted x JOIN dbo.room_inspections i ON i.inspection_id=x.inspection_id
  JOIN dbo.fe05_reinspection_bindings_v1 b ON b.inspection_id=i.inspection_id WHERE i.workflow_status=''ASSIGNED'' AND
   (NOT EXISTS(SELECT 1 FROM dbo.fe05_reinspection_defect_results_v1 v JOIN dbo.inspection_defects d ON d.defect_id=v.defect_id
      WHERE v.inspection_id=i.inspection_id AND v.rework_id=b.rework_id AND d.checklist_item_id=x.checklist_item_id)
    OR EXISTS(SELECT 1 FROM dbo.fe05_reinspection_defect_results_v1 v JOIN dbo.inspection_defects d ON d.defect_id=v.defect_id
      WHERE v.inspection_id=i.inspection_id AND d.checklist_item_id=x.checklist_item_id AND(v.fixed IS NULL OR v.recorded_by<>i.inspector_user_id))
    OR x.outcome<>CASE WHEN EXISTS(SELECT 1 FROM dbo.fe05_reinspection_defect_results_v1 v JOIN dbo.inspection_defects d ON d.defect_id=v.defect_id
      WHERE v.inspection_id=i.inspection_id AND d.checklist_item_id=x.checklist_item_id AND v.fixed=0) THEN ''FAIL'' ELSE ''PASS'' END))
  THROW 51999,''Reinspection criterion projection must match exact defect decisions'',1;');
EXEC sys.sp_executesql @results;
SET @defects=REPLACE(@defects,'CREATE TRIGGER','ALTER TRIGGER');
SET @defects=REPLACE(@defects,'r.workflow_status NOT IN(''INSPECTING'',''PENDING_CONCLUSION'')',
 N'(r.workflow_status NOT IN(''INSPECTING'',''PENDING_CONCLUSION'') AND NOT(r.workflow_status=''ASSIGNED'' AND EXISTS(SELECT 1 FROM dbo.fe05_reinspection_responsibilities_v1 b
 WHERE b.inspection_id=r.inspection_id AND b.inspector_user_id=r.inspector_user_id)))');
SET @defects=REPLACE(@defects,'SET NOCOUNT ON;',N'SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted x JOIN dbo.room_inspections i ON i.inspection_id=x.inspection_id
  JOIN dbo.fe05_reinspection_bindings_v1 b ON b.inspection_id=i.inspection_id WHERE i.workflow_status=''ASSIGNED''
  AND NOT EXISTS(SELECT 1 FROM dbo.fe05_reinspection_defect_results_v1 v JOIN dbo.inspection_defects original ON original.defect_id=v.defect_id
    WHERE v.inspection_id=i.inspection_id AND v.rework_id=b.rework_id AND v.fixed=0 AND v.recorded_by=i.inspector_user_id
      AND original.checklist_item_id=x.checklist_item_id AND original.description=x.description))
  THROW 51999,''Remaining defect must come from a Not Fixed decision'',1;');
EXEC sys.sp_executesql @defects;
