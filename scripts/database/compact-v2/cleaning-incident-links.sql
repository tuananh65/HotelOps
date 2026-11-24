SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps.CleaningIncidentLinks',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @lock<0 THROW 51501,'Unable to lock incident link migration.',1;
-- Recover the already recorded relationship; never infer a report from room or date alone.
INSERT dbo.work_links(branch_id,source_work_id,target_work_id,relation,created_by,created_at)
SELECT incident.branch_id,incident.work_id,cleaning.work_id,'SOURCE',e.actor_user_id,SYSUTCDATETIME()
FROM dbo.work_events e
JOIN dbo.work_items incident ON incident.work_id=e.work_id AND incident.kind='INCIDENT'
JOIN dbo.work_items cleaning ON cleaning.work_id=TRY_CONVERT(bigint,JSON_VALUE(e.snapshot_json,'$.CleaningWorkId'))
 AND cleaning.branch_id=incident.branch_id AND cleaning.room_id=incident.room_id AND cleaning.turnover_no=incident.turnover_no
 AND cleaning.kind IN('CLEANING','REWORK','INSPECTION')
WHERE e.event_type='ROOM_ISSUE_REPORTED' AND ISJSON(e.snapshot_json)=1
 AND NOT EXISTS(SELECT 1 FROM dbo.work_links l WITH(UPDLOCK,HOLDLOCK) WHERE l.source_work_id=incident.work_id AND l.target_work_id=cleaning.work_id AND l.relation='SOURCE');
DECLARE @added int=@@ROWCOUNT;
COMMIT;
SELECT @added AS AddedLinks;
