-- Historical synthetic rows only; recovery and all later transitions use HTTP.
SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51999,'Exact TEST database required',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id=@branch AND code=@prefix AND is_active=1)
 THROW 51999,'Owned active fixture branch required',1;
IF NOT EXISTS(SELECT 1 FROM users WHERE user_id=@actor AND username=@prefix+'-hk' AND account_status='ACTIVE')
 OR NOT EXISTS(SELECT 1 FROM users WHERE user_id=@supervisor AND username=@prefix+'-supervisor')
 THROW 51999,'Owned fixture actors required',1;
IF NOT EXISTS(SELECT 1 FROM rooms r WITH(UPDLOCK,HOLDLOCK)
 JOIN work_items w ON w.work_id=r.current_cleaning_work_id
 JOIN work_assignments a ON a.work_id=w.work_id AND a.assignee_user_id=@actor AND a.assignment_status='ACTIVE'
 WHERE r.room_id=@room AND r.branch_id=@branch AND r.is_active=1 AND w.work_id=@cleaning
 AND w.kind='CLEANING' AND w.status='IN_PROGRESS' AND w.branch_id=@branch AND w.room_id=@room
 AND w.turnover_no=r.turnover_no AND w.created_by=@supervisor AND w.title='TEST legacy cleaning')
 THROW 51999,'Exact owned current cleaning ancestry required',1;
IF NOT EXISTS(SELECT 1 FROM assets WHERE asset_id=@asset AND branch_id=@branch AND room_id=@room AND is_active=1 AND asset_code LIKE @prefix+'-%')
 THROW 51999,'Owned room asset required',1;
DECLARE @marker nvarchar(200)='Synthetic legacy fixture '+@runId;
IF EXISTS(SELECT 1 FROM work_items WHERE branch_id=@branch AND description=@marker)
 THROW 51999,'Legacy fixture already seeded; preserve existing history',1;
DECLARE @turnover int=(SELECT turnover_no FROM rooms WHERE room_id=@room),@time datetime2(7)=SYSUTCDATETIME();
DECLARE @seeded TABLE(work_id bigint,title nvarchar(250));
INSERT work_items(branch_id,work_no,kind,status,title,description,room_id,asset_id,turnover_no,created_by,created_at)
 OUTPUT inserted.work_id,inserted.title INTO @seeded
 VALUES(@branch,CONVERT(varchar(36),NEWID()),'INCIDENT','OPEN',@title,@marker,@room,@asset,@turnover,@actor,@time);
DECLARE @incident bigint=(SELECT work_id FROM @seeded),@i int=0;
-- INCIDENT cannot have parent_work_id (CK_work_items_parent_kind). Its recorded
-- cleaning context uses the existing SOURCE relationship, as in report creation.
INSERT work_links(branch_id,source_work_id,target_work_id,relation,created_by,created_at)
 VALUES(@branch,@incident,@cleaning,'SOURCE',@actor,@time);
WHILE @i<26
BEGIN
 -- Pagination includes terminal history. Seed noise already cancelled rather than
 -- asking a maintenance actor to cancel unrelated legacy incidents at runtime.
 -- These synthetic rows never represent live room blockers or recovery targets.
 INSERT work_items(branch_id,work_no,kind,status,title,description,room_id,asset_id,turnover_no,created_by,created_at,closed_at,completion_note)
 OUTPUT inserted.work_id,inserted.title INTO @seeded
 VALUES(@branch,CONVERT(varchar(36),NEWID()),'INCIDENT','CANCELLED',@paging+' '+CONVERT(varchar(3),@i),@marker,@room,@asset,@turnover,@actor,DATEADD(millisecond,@i+1,@time),DATEADD(millisecond,@i+1,@time),N'Synthetic terminal pagination history; no physical incident');
 SET @i=@i+1;
END;
-- Legacy incident keeps its original physical room block; no modern workflow events.
INSERT room_blocks(branch_id,room_id,source_work_id,reason,blocked_by,blocked_at)
 VALUES(@branch,@room,@incident,@title,@actor,@time);
COMMIT;
SELECT @incident workId,@actor createdBy,@title title,CONVERT(varchar(33),@time,126) createdAt,
 JSON_QUERY((SELECT work_id workId FROM @seeded WHERE work_id<>@incident ORDER BY work_id FOR JSON PATH)) noise
 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
