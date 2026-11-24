-- P0 TEST-only policy. Preserve every cleaning result, turnover and occupancy.
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOpsCompactWorkdayTestP0%' THROW 51560,'P0 TEST database required.',1;
-- Each issue retains its own hold. Operational holds are never released by another issue.
IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.room_blocks') AND name='UX_room_blocks_room_id' AND is_unique=1)
 DROP INDEX UX_room_blocks_room_id ON dbo.room_blocks;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.room_blocks') AND name='IX_room_blocks_open')
 CREATE INDEX IX_room_blocks_open ON dbo.room_blocks(room_id,source_work_id) WHERE released_at IS NULL;
GO
CREATE OR ALTER VIEW dbo.v_room_readiness AS
 SELECT room.room_id,room.branch_id,room.operational_status,room.turnover_no,
   CONVERT(bit,CASE WHEN room.operational_status='OCCUPIED' THEN 1 ELSE 0 END) AS is_occupied,
   CASE WHEN room.is_active=0 THEN 'INACTIVE'
    WHEN room.operational_status='OUT_OF_SERVICE' THEN 'BLOCKED'
    WHEN EXISTS(SELECT 1 FROM dbo.room_blocks b WHERE b.room_id=room.room_id AND b.blocked_at<=SYSUTCDATETIME()
      AND (b.released_at IS NULL OR b.released_at>SYSUTCDATETIME())) THEN 'BLOCKED'
    WHEN EXISTS(SELECT 1 FROM dbo.work_items issue WHERE issue.room_id=room.room_id
      AND issue.kind IN ('INCIDENT','MAINTENANCE') AND issue.status NOT IN ('CLOSED','CANCELLED')) THEN 'BLOCKED'
    WHEN cleaning.work_id IS NULL AND room.current_cleaning_work_id IS NULL
      AND room.operational_status IN ('READY','AVAILABLE','OCCUPIED','RESERVED')
      AND NOT EXISTS(SELECT 1 FROM dbo.work_items pending WHERE pending.room_id=room.room_id AND pending.turnover_no=room.turnover_no
        AND pending.kind IN ('CLEANING','REWORK','INSPECTION') AND pending.status NOT IN ('COMPLETED','PASSED','CANCELLED'))
      AND EXISTS(SELECT 1 FROM dbo.room_events baseline WHERE baseline.room_id=room.room_id
        AND baseline.turnover_no=room.turnover_no AND baseline.event_type='ADMIN_BASELINE_RESET') THEN 'READY'
    WHEN cleaning.work_id IS NULL THEN 'PENDING_CLEANING'
    WHEN cleaning.status='IN_PROGRESS' THEN 'CLEANING_IN_PROGRESS'
    WHEN cleaning.status='REWORK_REQUIRED' OR inspection.status='FAILED' OR EXISTS(
      SELECT 1 FROM dbo.work_items r WHERE r.room_id=room.room_id AND r.turnover_no=room.turnover_no
      AND r.kind='REWORK' AND r.status NOT IN ('COMPLETED','CANCELLED')) THEN 'REWORK_REQUIRED'
    WHEN cleaning.status='COMPLETED' AND inspection.status='PASSED' AND inspection.verified_by IS NOT NULL
      AND NOT EXISTS(SELECT 1 FROM dbo.work_items pending WHERE pending.room_id=room.room_id AND pending.turnover_no=room.turnover_no
        AND ((pending.kind='CLEANING' AND pending.status NOT IN ('COMPLETED','CANCELLED'))
          OR (pending.kind='INSPECTION' AND pending.status NOT IN ('PASSED','FAILED','CANCELLED'))))
      AND NOT EXISTS(SELECT 1 FROM dbo.inspection_findings f JOIN dbo.work_items i ON i.work_id=f.inspection_work_id
        WHERE i.parent_work_id=cleaning.work_id AND f.status NOT IN ('RESOLVED','CANCELLED')) THEN 'READY'
    WHEN cleaning.status='PENDING_INSPECTION' THEN 'PENDING_INSPECTION'
    ELSE 'PENDING_CLEANING' END AS readiness
 FROM dbo.rooms room LEFT JOIN dbo.work_items cleaning ON cleaning.work_id=room.current_cleaning_work_id
   AND cleaning.kind='CLEANING' AND cleaning.room_id=room.room_id AND cleaning.turnover_no=room.turnover_no
 OUTER APPLY(SELECT TOP(1) i.status,i.verified_by FROM dbo.work_items i
   WHERE i.parent_work_id=cleaning.work_id AND i.kind='INSPECTION' AND i.status<>'CANCELLED'
   ORDER BY i.created_at DESC,i.work_id DESC) inspection;
GO
