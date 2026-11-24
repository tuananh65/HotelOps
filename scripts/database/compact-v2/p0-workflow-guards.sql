SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOpsCompactWorkdayTestP0%' THROW 51560,'P0 TEST database required.',1;
DECLARE @guard nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.TR_inventory_documents_posted'));
IF @guard NOT LIKE '%ROOM_REMEDIATION_AUTHORIZED%' THROW 51561,'Expected existing remediation guard.',1;
SET @guard=N'ALTER '+SUBSTRING(@guard,CHARINDEX('TRIGGER',@guard),LEN(@guard));
SET @guard=REPLACE(@guard,'r.code=''DEPT_MANAGER''','r.code=''HOUSEKEEPING_SUPERVISOR''');
IF @guard NOT LIKE '%ROOM_ISSUE_INSPECTION_FAILED%'
 SET @guard=REPLACE(@guard,'''ROOM_ISSUE_APPROVED'',''ROOM_ISSUE_MATERIAL_REQUESTED''','''ROOM_ISSUE_APPROVED'',''ROOM_ISSUE_MATERIAL_REQUESTED'',''ROOM_ISSUE_INSPECTION_FAILED''');
EXEC sys.sp_executesql @guard;
SET @guard=OBJECT_DEFINITION(OBJECT_ID('dbo.TR_work_items_review'));
IF @guard NOT LIKE '%OM_OPERATIONAL_SELF_REVIEW%'
BEGIN
 DECLARE @needle nvarchar(max)=N'OR (w.kind IN (''OPERATIONAL'',''MAINTENANCE'') AND a.work_id=w.work_id)';
 IF CHARINDEX(@needle,@guard)=0 THROW 51562,'Expected existing independent verifier guard.',1;
 SET @guard=REPLACE(@guard,@needle,N'OR (w.kind IN (''OPERATIONAL'',''MAINTENANCE'') AND a.work_id=w.work_id
    AND NOT (w.kind=''OPERATIONAL'' AND EXISTS(SELECT 1 FROM dbo.work_events self_review WHERE self_review.work_id=w.work_id
      AND self_review.event_type=''OM_OPERATIONAL_SELF_REVIEW'' AND self_review.actor_user_id=w.verified_by)
      AND EXISTS(SELECT 1 FROM dbo.user_roles ur JOIN dbo.roles r ON r.role_id=ur.role_id
        WHERE ur.user_id=w.verified_by AND ur.branch_id=w.branch_id AND r.code=''OPERATION_MANAGER'' AND r.is_active=1
        AND ur.revoked_at IS NULL AND ur.assigned_at<=SYSUTCDATETIME() AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME()))))');
 SET @guard=N'ALTER '+SUBSTRING(@guard,CHARINDEX('TRIGGER',@guard),LEN(@guard));
 EXEC sys.sp_executesql @guard;
END;
