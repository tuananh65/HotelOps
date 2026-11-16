-- Resume storage from reviewed FS014 source; execute only via identity/backup guarded installer.
SET XACT_ABORT ON;
IF OBJECT_ID('dbo.workdays','U') IS NULL OR OBJECT_ID('dbo.work_intervals','U') IS NULL
 OR OBJECT_ID('dbo.room_cleaning_cycle_state','U') IS NULL OR COL_LENGTH('dbo.cleaning_tasks','row_version')<>8
 THROW 51970,'FS013 foundation required',1;

IF OBJECT_ID('dbo.fs014_cleaning_blocker_resolutions') IS NOT NULL
 THROW 51970,'Existing FS014 schema; no overwrite',1;
CREATE TABLE dbo.fs014_cleaning_blocker_resolutions(
 progress_id bigint NOT NULL CONSTRAINT PK_fs014_blocker PRIMARY KEY
   CONSTRAINT FK_fs014_blocker_progress REFERENCES dbo.cleaning_progress_logs(progress_id),
 -- Reporter/time/reason/task are authoritative in the immutable BLOCKED progress source.
 -- Links are opaque integration identities. No source adapter is accepted in V1.
 incident_id bigint NULL,material_request_id bigint NULL,
 resolved_by bigint NULL CONSTRAINT FK_fs014_blocker_resolver REFERENCES dbo.users(user_id),
 resolved_at datetime2(7) NULL,resolution_note nvarchar(500) NULL,
 CONSTRAINT CK_fs014_blocker_links CHECK((incident_id IS NULL OR incident_id>0) AND (material_request_id IS NULL OR material_request_id>0)),
 CONSTRAINT CK_fs014_blocker_resolution CHECK(
  (resolved_by IS NULL AND resolved_at IS NULL AND resolution_note IS NULL) OR
  (resolved_by IS NOT NULL AND resolved_at IS NOT NULL AND resolution_note IS NOT NULL AND LEN(LTRIM(RTRIM(resolution_note)))>0)));
-- No backfill, synthetic blocker, notification, task status, permission, or room pointer write.
EXEC(N'CREATE TRIGGER dbo.TR_fs014_blocker_version ON dbo.fs014_cleaning_blocker_resolutions AFTER INSERT,UPDATE,DELETE AS
 BEGIN
 SET NOCOUNT ON;
 UPDATE t SET updated_at=SYSUTCDATETIME() FROM dbo.cleaning_tasks t
 WHERE EXISTS(SELECT 1 FROM dbo.cleaning_progress_logs p
 WHERE p.cleaning_task_id=t.cleaning_task_id AND p.progress_id IN(SELECT progress_id FROM inserted UNION SELECT progress_id FROM deleted));
 END;');


