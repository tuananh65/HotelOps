-- Maintenance Reopen + Cycle History V1; additive Database First contract.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
SET NOCOUNT ON;
SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName;
IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'HotelOpsDB'
 THROW 51000, 'Maintenance cycle patch requires HotelOpsDB.', 1;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:MaintenanceCycles:Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51001, 'Schema lock unavailable.', 1;
 IF OBJECT_ID('dbo.maintenance_work_cycles','U') IS NOT NULL OR COL_LENGTH('dbo.maintenance_work_logs','cycle_id') IS NOT NULL
  THROW 51002, 'Unexpected cycle objects exist; compare metadata instead of overwriting.', 1;
 CREATE TABLE dbo.maintenance_work_cycles(
  cycle_id bigint IDENTITY CONSTRAINT PK_maintenance_work_cycles PRIMARY KEY,
  maintenance_work_id bigint NOT NULL CONSTRAINT FK_maintenance_cycles_work REFERENCES dbo.maintenance_works(maintenance_work_id),
  cycle_no int NOT NULL,
  is_legacy_snapshot bit NOT NULL,
  recorded_at datetime2(7) NOT NULL,
  recorded_by bigint NOT NULL CONSTRAINT FK_maintenance_cycles_recorder REFERENCES dbo.users(user_id),
  opened_at datetime2(7) NULL,
  opened_by bigint NULL CONSTRAINT FK_maintenance_cycles_opener REFERENCES dbo.users(user_id),
  reopen_reason nvarchar(max) NULL,
  starting_work_status varchar(40) NULL,
  starting_incident_status varchar(40) NULL,
  finalized_at datetime2(7) NULL,
  terminal_status varchar(40) NULL,
  ended_at datetime2(7) NULL,
  ended_by bigint NULL CONSTRAINT FK_maintenance_cycles_ender REFERENCES dbo.users(user_id),
  terminal_reason nvarchar(max) NULL,
  assignee_id bigint NULL CONSTRAINT FK_maintenance_cycles_assignee REFERENCES dbo.users(user_id),
  department_id bigint NULL CONSTRAINT FK_maintenance_cycles_department REFERENCES dbo.departments(department_id),
  priority_id bigint NULL CONSTRAINT FK_maintenance_cycles_priority REFERENCES dbo.reference_values(reference_id),
  title nvarchar(200) NULL,
  instruction nvarchar(max) NULL,
  planned_start_at datetime2(7) NULL,
  planned_end_at datetime2(7) NULL,
  actual_start_at datetime2(7) NULL,
  resolved_at datetime2(7) NULL,
  resolution_note nvarchar(max) NULL,
  reviewed_by bigint NULL CONSTRAINT FK_maintenance_cycles_reviewer REFERENCES dbo.users(user_id),
  reviewed_at datetime2(7) NULL,
  incident_resolved_at datetime2(7) NULL,
  incident_closed_at datetime2(7) NULL,
  incident_closure_note nvarchar(max) NULL,
  CONSTRAINT UQ_maintenance_cycles_work_no UNIQUE(maintenance_work_id,cycle_no),
  CONSTRAINT UQ_maintenance_cycles_id_work UNIQUE(cycle_id,maintenance_work_id),
  CONSTRAINT CK_maintenance_cycles_number CHECK(cycle_no>0),
  CONSTRAINT CK_maintenance_cycles_origin CHECK(
   (is_legacy_snapshot=1 AND cycle_no=1 AND finalized_at IS NOT NULL AND terminal_status IS NOT NULL AND terminal_status='CLOSED'
    AND opened_at IS NULL AND opened_by IS NULL AND reopen_reason IS NULL AND starting_work_status IS NULL AND starting_incident_status IS NULL)
   OR (is_legacy_snapshot=0 AND cycle_no>=2 AND opened_at IS NOT NULL AND opened_by IS NOT NULL
    AND reopen_reason IS NOT NULL AND LEN(LTRIM(RTRIM(reopen_reason)))>=10
    AND starting_work_status IS NOT NULL AND starting_work_status='PENDING'
    AND starting_incident_status IS NOT NULL AND starting_incident_status='OPEN')),
  CONSTRAINT CK_maintenance_cycles_terminal CHECK(
   (finalized_at IS NULL AND terminal_status IS NULL AND ended_at IS NULL AND ended_by IS NULL AND terminal_reason IS NULL
    AND assignee_id IS NULL AND department_id IS NULL AND priority_id IS NULL AND title IS NULL AND instruction IS NULL
    AND planned_start_at IS NULL AND planned_end_at IS NULL AND actual_start_at IS NULL AND resolved_at IS NULL AND resolution_note IS NULL
    AND reviewed_by IS NULL AND reviewed_at IS NULL AND incident_resolved_at IS NULL AND incident_closed_at IS NULL AND incident_closure_note IS NULL)
   OR (finalized_at IS NOT NULL AND terminal_status IS NOT NULL AND terminal_status IN ('CLOSED','CANCELLED') AND title IS NOT NULL
    AND (is_legacy_snapshot=1 OR (ended_at IS NOT NULL AND ended_by IS NOT NULL AND terminal_reason IS NOT NULL
     AND LEN(LTRIM(RTRIM(terminal_reason)))>=10 AND ended_at>=opened_at AND finalized_at>=ended_at
     AND (terminal_status='CANCELLED' OR (reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL AND incident_closed_at IS NOT NULL))))))
 );
 -- The NULL second key preserves one active row and avoids reverse-engineering
 -- the Work FK as one-to-one merely because a filtered single-column UQ exists.
 CREATE UNIQUE INDEX UX_maintenance_cycles_active ON dbo.maintenance_work_cycles(maintenance_work_id,finalized_at) WHERE finalized_at IS NULL;
 CREATE INDEX IX_maintenance_cycles_recorder ON dbo.maintenance_work_cycles(recorded_by);
 CREATE INDEX IX_maintenance_cycles_opener ON dbo.maintenance_work_cycles(opened_by);
 CREATE INDEX IX_maintenance_cycles_ender ON dbo.maintenance_work_cycles(ended_by);
 CREATE INDEX IX_maintenance_cycles_assignee ON dbo.maintenance_work_cycles(assignee_id);
 CREATE INDEX IX_maintenance_cycles_department ON dbo.maintenance_work_cycles(department_id);
 CREATE INDEX IX_maintenance_cycles_priority ON dbo.maintenance_work_cycles(priority_id);
 CREATE INDEX IX_maintenance_cycles_reviewer ON dbo.maintenance_work_cycles(reviewed_by);
 ALTER TABLE dbo.maintenance_work_logs ADD cycle_id bigint NULL;
 ALTER TABLE dbo.maintenance_work_logs WITH CHECK ADD CONSTRAINT FK_maintenance_logs_cycle_work
  FOREIGN KEY(cycle_id,maintenance_work_id) REFERENCES dbo.maintenance_work_cycles(cycle_id,maintenance_work_id);
 CREATE INDEX IX_maintenance_logs_cycle_history ON dbo.maintenance_work_logs(cycle_id,maintenance_work_id,logged_at,log_id);
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
