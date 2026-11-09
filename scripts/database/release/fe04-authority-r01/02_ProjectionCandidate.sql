-- Isolated candidate, not a deployment. No change to CK_rooms_operational_status.
SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
 AND name=N'HotelOps.FE04.UnblockR01.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_DISPOSABLE')
 THROW 51994,'Explicitly approved FE04 disposable target required',1;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.room_readiness_events')
 AND name='CK_fs016_readiness' AND is_disabled=0 AND is_not_trusted=0)
 OR EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE04.InspectionProjection.V1')
 THROW 51994,'Exact FS016 readiness baseline and fresh candidate required',1;
BEGIN TRANSACTION;
ALTER TABLE dbo.room_readiness_events DROP CONSTRAINT CK_fs016_readiness;
ALTER TABLE dbo.room_readiness_events WITH CHECK ADD CONSTRAINT CK_fs016_readiness
 CHECK(readiness IN('PENDING_CLEANING','CLEANING_IN_PROGRESS','PENDING_INSPECTION','REWORK_REQUIRED','READY'));
-- BLOCKED is an effective RoomBlock overlay, never inserted into rooms/readiness.
-- FAILED preserves operational_status; REWORK_REQUIRED is represented by readiness.
-- Requiring operational_status='REWORK_REQUIRED'/'BLOCKED' needs a separate approved
-- cross-module status/schema decision. This candidate does not invent those DB values.
EXEC sys.sp_addextendedproperty @name=N'HotelOps.FE04.InspectionProjection.V1',@value=N'READINESS_OVERLAY_V1';
COMMIT;
