-- Optional UC102/103 + JOB05 metadata; never execute on shared HotelOpsDB.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
 THROW 51871,'Exact owned disposable database required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskPlanning.V1' AND CONVERT(nvarchar(20),value)='1')
 OR OBJECT_ID('dbo.operational_task_definitions') IS NULL
 THROW 51871,'Verified TaskPlanning capability required.',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskSeries.V1')
BEGIN
 IF OBJECT_ID('dbo.operational_task_series') IS NOT NULL OR OBJECT_ID('dbo.task_series_occurrences') IS NOT NULL
  THROW 51871,'Unreviewed series storage already exists.',1;
 CREATE TABLE dbo.operational_task_series(
  series_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_operational_task_series PRIMARY KEY,
  branch_id bigint NOT NULL,department_id bigint NOT NULL,created_by bigint NOT NULL,default_assignee_id bigint NOT NULL,
  rule_code varchar(10) NOT NULL,weekday_mask int NOT NULL,month_day int NULL,
  first_due_at datetime2(7) NOT NULL,next_due_at datetime2(7) NULL,end_date date NULL,
  is_active bit NOT NULL,template_json nvarchar(max) NOT NULL,
  creation_key uniqueidentifier NOT NULL,creation_hash binary(32) NOT NULL,
  created_at datetime2(7) NOT NULL,stopped_at datetime2(7) NULL,stopped_by bigint NULL,row_version rowversion NOT NULL,
  CONSTRAINT FK_task_series_branch FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id),
  CONSTRAINT FK_task_series_department FOREIGN KEY(department_id) REFERENCES dbo.departments(department_id),
  CONSTRAINT FK_task_series_creator FOREIGN KEY(created_by) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_task_series_executor FOREIGN KEY(default_assignee_id) REFERENCES dbo.users(user_id),
  CONSTRAINT FK_task_series_stopper FOREIGN KEY(stopped_by) REFERENCES dbo.users(user_id),
  CONSTRAINT UQ_task_series_creation_key UNIQUE(creation_key),
  CONSTRAINT CK_task_series_rule CHECK(
   (rule_code='DAILY' AND weekday_mask=0 AND month_day IS NULL)
   OR (rule_code='WEEKLY' AND weekday_mask BETWEEN 1 AND 127 AND month_day IS NULL)
   OR (rule_code='MONTHLY' AND weekday_mask=0 AND month_day IS NOT NULL AND month_day BETWEEN 1 AND 31)),
  CONSTRAINT CK_task_series_template CHECK(ISJSON(template_json)=1),
  CONSTRAINT CK_task_series_next CHECK(next_due_at IS NULL OR next_due_at>=first_due_at),
  CONSTRAINT CK_task_series_stop CHECK((is_active=1 AND stopped_at IS NULL AND stopped_by IS NULL)
    OR (is_active=0 AND stopped_at IS NOT NULL AND stopped_by IS NOT NULL))
 );
 CREATE INDEX IX_task_series_due ON dbo.operational_task_series(is_active,next_due_at,series_id);
 CREATE TABLE dbo.task_series_occurrences(
  series_id bigint NOT NULL,due_date date NOT NULL,due_at datetime2(7) NOT NULL,task_id bigint NOT NULL,created_at datetime2(7) NOT NULL,
  CONSTRAINT PK_task_series_occurrences PRIMARY KEY(series_id,due_date),
  CONSTRAINT UQ_task_series_occurrence_task UNIQUE(task_id),
  CONSTRAINT FK_task_series_occurrence_series FOREIGN KEY(series_id) REFERENCES dbo.operational_task_series(series_id),
  CONSTRAINT FK_task_series_occurrence_task FOREIGN KEY(task_id) REFERENCES dbo.operational_tasks(task_id)
 );
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.TaskSeries.V1',@value=N'1';
END;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_task_series_definition_v1 ON dbo.operational_task_series AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.series_id=d.series_id WHERE i.series_id IS NULL
  OR i.branch_id<>d.branch_id OR i.department_id<>d.department_id OR i.created_by<>d.created_by
  OR i.default_assignee_id<>d.default_assignee_id OR i.rule_code<>d.rule_code OR i.weekday_mask<>d.weekday_mask
  OR ISNULL(i.month_day,0)<>ISNULL(d.month_day,0) OR i.first_due_at<>d.first_due_at
  OR i.end_date<>d.end_date OR (i.end_date IS NULL AND d.end_date IS NOT NULL) OR (i.end_date IS NOT NULL AND d.end_date IS NULL)
  OR i.template_json COLLATE Latin1_General_100_BIN2<>d.template_json COLLATE Latin1_General_100_BIN2
  OR i.creation_key<>d.creation_key OR i.creation_hash<>d.creation_hash OR i.created_at<>d.created_at
  OR d.is_active=0 OR i.next_due_at<d.next_due_at OR (d.next_due_at IS NULL AND i.next_due_at IS NOT NULL))
  THROW 51871,''Series definition is immutable; stopped series cannot resume or rewind.'',1;
END;');
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_task_series_occurrence_v1 ON dbo.task_series_occurrences AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51871,''Generated occurrence identity is immutable.'',1;
END;');
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskSeries.V1' AND CONVERT(nvarchar(20),value)='1')
 OR COL_LENGTH('dbo.operational_task_series','row_version')<>8
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN('FK_task_series_branch','FK_task_series_department','FK_task_series_creator','FK_task_series_executor','FK_task_series_stopper','FK_task_series_occurrence_series','FK_task_series_occurrence_task') AND is_disabled=0 AND is_not_trusted=0)<>7
 THROW 51871,'Conflicting recurring capability.',1;
COMMIT;
SELECT 'TASK_SERIES_INSTALLED' AS result;
