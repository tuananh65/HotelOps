-- Reviewed change: allow documented physical inspection alongside legacy digital results.
-- No table, status, permission, room data or historical checklist changes.
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOpsCompact%' THROW 51000,'Compact database required.',1;
DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.TR_work_items_review'));
IF @definition IS NULL THROW 51320,'Review trigger missing.',1;
IF @definition NOT LIKE '%PAPER_INSPECTION_RECORDED%'
BEGIN
 DECLARE @start int=CHARINDEX(' IF EXISTS(SELECT 1 FROM inserted i WHERE i.kind=''INSPECTION''',@definition);
 DECLARE @finish int=CHARINDEX(' IF EXISTS(SELECT 1 FROM inserted w JOIN dbo.work_assignments',@definition);
 IF @start=0 OR @finish<=@start OR @definition NOT LIKE '%THROW 51212%'
   THROW 51321,'Unexpected review trigger; manual review required.',1;
 DECLARE @replacement nvarchar(max)=N' IF EXISTS(SELECT 1 FROM inserted i WHERE i.kind=''INSPECTION'' AND i.status=''PASSED''
   AND ((NOT EXISTS(SELECT 1 FROM dbo.work_events e WHERE e.work_id=i.work_id
       AND e.event_type=''PAPER_INSPECTION_RECORDED'' AND e.actor_user_id=i.verified_by AND e.occurred_at<=i.verified_at)
       AND (i.checklist_template_id IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.work_checklist_items c WHERE c.work_id=i.work_id)
        OR EXISTS(SELECT 1 FROM dbo.work_checklist_items c WHERE c.work_id=i.work_id AND c.required_snapshot=1 AND (c.outcome IS NULL OR c.outcome<>''PASS''))))
    OR EXISTS(SELECT 1 FROM dbo.inspection_findings f JOIN dbo.work_items source ON source.work_id=f.inspection_work_id
       WHERE source.parent_work_id=i.parent_work_id AND f.status NOT IN (''RESOLVED'',''CANCELLED''))))
   THROW 51212,''Passed inspection requires recorded physical inspection or completed digital checks, and resolved findings.'',1;
';
 SET @definition=STUFF(@definition,@start,@finish-@start,@replacement);
 SET @definition=REPLACE(@definition,'CREATE TRIGGER','ALTER TRIGGER');
 EXEC sys.sp_executesql @definition;
END;
IF EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_work_items_review') AND is_disabled=1)
 THROW 51322,'Review trigger must remain enabled.',1;
