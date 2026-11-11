-- CANDIDATE ONLY: owner must approve artifact, SQLCMD variable values and disposable lease before execution.
SET NOCOUNT ON; SET XACT_ABORT ON;
IF ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),N'')<>N'$(TrialServer)' OR DB_NAME()<>N'$(TrialDatabase)' OR DB_NAME() NOT LIKE N'HotelOps[_]UC139[_]Trial[_]%'
  OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid=CONVERT(uniqueidentifier,'$(TrialDatabaseGuid)'))
  OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UC139.Trial' AND CONVERT(nvarchar(128),value)=N'$(LeaseId)')
  THROW 51890,'Disposable trial identity/owner marker required.',1;
IF SYSUTCDATETIME()<CONVERT(datetime2(7),'$(LeaseStartsUtc)',127) OR SYSUTCDATETIME()>=CONVERT(datetime2(7),'$(LeaseEndsUtc)',127)
  OR DATEDIFF(minute,CONVERT(datetime2(7),'$(LeaseStartsUtc)',127),CONVERT(datetime2(7),'$(LeaseEndsUtc)',127)) NOT BETWEEN 1 AND 480
  THROW 51891,'Fresh bounded trial lease required.',1;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.workdays') IS NOT NULL OR OBJECT_ID('dbo.work_intervals') IS NOT NULL OR OBJECT_ID('dbo.time_correction_requests') IS NOT NULL THROW 51892,'Existing WorkTime tables: no overwrite.',1;

CREATE TABLE [dbo].[workdays] (
    [workday_id] bigint NOT NULL IDENTITY,
    [user_id] bigint NOT NULL,
    [branch_id] bigint NOT NULL,
    [department_id] bigint NOT NULL,
    [work_date] date NOT NULL,
    [shift_assignment_id] bigint NULL,
    [is_unscheduled] bit NOT NULL,
    [unscheduled_reason] nvarchar(1000) NULL,
    [status] varchar(10) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [started_at] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [ended_at] datetime2(7) NULL,
    [row_version] rowversion NOT NULL,
    CONSTRAINT [PK_workdays] PRIMARY KEY ([workday_id]),
    CONSTRAINT [UQ_workdays_identity] UNIQUE ([workday_id], [user_id], [branch_id]),
    CONSTRAINT [FK_workdays_assignment] FOREIGN KEY ([shift_assignment_id]) REFERENCES [dbo].[shift_assignments] ([shift_assignment_id]),
    CONSTRAINT [FK_workdays_branch] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([branch_id]),
    CONSTRAINT [FK_workdays_department] FOREIGN KEY ([department_id]) REFERENCES [dbo].[departments] ([department_id]),
    CONSTRAINT [FK_workdays_user] FOREIGN KEY ([user_id]) REFERENCES [dbo].[users] ([user_id])
);


CREATE TABLE [dbo].[work_intervals] (
    [interval_id] bigint NOT NULL IDENTITY,
    [workday_id] bigint NOT NULL,
    [user_id] bigint NOT NULL,
    [branch_id] bigint NOT NULL,
    [work_item_kind] varchar(24) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [work_item_id] bigint NOT NULL,
    [opened_by] bigint NOT NULL,
    [opened_at] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [closed_by] bigint NULL,
    [closed_at] datetime2(7) NULL,
    [effective_started_at] datetime2(7) NOT NULL,
    [effective_ended_at] datetime2(7) NULL,
    [row_version] rowversion NOT NULL,
    CONSTRAINT [PK_work_intervals] PRIMARY KEY ([interval_id]),
    CONSTRAINT [FK_work_intervals_closed_by] FOREIGN KEY ([closed_by]) REFERENCES [dbo].[users] ([user_id]),
    CONSTRAINT [FK_work_intervals_opened_by] FOREIGN KEY ([opened_by]) REFERENCES [dbo].[users] ([user_id]),
    CONSTRAINT [FK_work_intervals_workday] FOREIGN KEY ([workday_id], [user_id], [branch_id]) REFERENCES [dbo].[workdays] ([workday_id], [user_id], [branch_id])
);


CREATE TABLE [dbo].[time_correction_requests] (
    [correction_id] bigint NOT NULL IDENTITY,
    [interval_id] bigint NOT NULL,
    [requested_by] bigint NOT NULL,
    [requested_at] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
    [original_started_at] datetime2(7) NOT NULL,
    [original_ended_at] datetime2(7) NOT NULL,
    [original_interval_version] binary(8) NOT NULL,
    [proposed_started_at] datetime2(7) NOT NULL,
    [proposed_ended_at] datetime2(7) NOT NULL,
    [reason] nvarchar(1000) NOT NULL,
    [status] varchar(10) COLLATE Latin1_General_100_BIN2 NOT NULL,
    [decided_by] bigint NULL,
    [decided_at] datetime2(7) NULL,
    [decision_reason] nvarchar(1000) NULL,
    [applied_started_at] datetime2(7) NULL,
    [applied_ended_at] datetime2(7) NULL,
    [row_version] rowversion NOT NULL,
    CONSTRAINT [PK_time_correction_requests] PRIMARY KEY ([correction_id]),
    CONSTRAINT [FK_time_corrections_decider] FOREIGN KEY ([decided_by]) REFERENCES [dbo].[users] ([user_id]),
    CONSTRAINT [FK_time_corrections_interval] FOREIGN KEY ([interval_id]) REFERENCES [dbo].[work_intervals] ([interval_id]),
    CONSTRAINT [FK_time_corrections_requester] FOREIGN KEY ([requested_by]) REFERENCES [dbo].[users] ([user_id])
);


CREATE INDEX [IX_time_correction_requests_decided_by] ON [dbo].[time_correction_requests] ([decided_by]);


CREATE INDEX [IX_time_correction_requests_requested_by] ON [dbo].[time_correction_requests] ([requested_by]);


CREATE INDEX [IX_time_corrections_interval] ON [dbo].[time_correction_requests] ([interval_id], [requested_at], [correction_id]);


CREATE INDEX [IX_work_intervals_closed_by] ON [dbo].[work_intervals] ([closed_by]);


CREATE INDEX [IX_work_intervals_item] ON [dbo].[work_intervals] ([work_item_kind], [work_item_id], [interval_id]);


CREATE INDEX [IX_work_intervals_opened_by] ON [dbo].[work_intervals] ([opened_by]);


CREATE INDEX [IX_work_intervals_user_time] ON [dbo].[work_intervals] ([user_id], [effective_started_at], [interval_id]);


CREATE INDEX [IX_work_intervals_workday_id_user_id_branch_id] ON [dbo].[work_intervals] ([workday_id], [user_id], [branch_id]);


CREATE UNIQUE INDEX [UX_work_intervals_one_open] ON [dbo].[work_intervals] ([user_id]) WHERE [closed_at] IS NULL;


CREATE INDEX [IX_workdays_branch_id] ON [dbo].[workdays] ([branch_id]);


CREATE INDEX [IX_workdays_department_id] ON [dbo].[workdays] ([department_id]);


CREATE INDEX [IX_workdays_shift_assignment_id] ON [dbo].[workdays] ([shift_assignment_id]);


CREATE INDEX [IX_workdays_user_date] ON [dbo].[workdays] ([user_id], [work_date], [workday_id]);


CREATE UNIQUE INDEX [UX_workdays_one_working] ON [dbo].[workdays] ([user_id]) WHERE [status] = 'WORKING';

EXEC sys.sp_addextendedproperty @name=N'HotelOps.UC139.SchemaCandidate',@value=N'$(LeaseId)';
IF OBJECT_ID('dbo.workdays','U') IS NULL THROW 51895,'Missing mapped WorkTime table.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.workdays'))<>12 THROW 51896,'WorkTime column count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.workdays') AND c.name=N'workday_id' AND t.name=N'bigint' AND c.is_nullable=0 AND c.is_identity=1) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.workdays') AND c.name=N'branch_id' AND t.name=N'bigint' AND c.is_nullable=0) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.workdays') AND c.name=N'department_id' AND t.name=N'bigint' AND c.is_nullable=0) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.workdays') AND c.name=N'ended_at' AND t.name=N'datetime2' AND c.is_nullable=1 AND c.scale=7) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.workdays') AND c.name=N'is_unscheduled' AND t.name=N'bit' AND c.is_nullable=0) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.workdays') AND c.name=N'row_version' AND t.name=N'timestamp' AND c.is_nullable=0 AND c.system_type_id=189 AND c.max_length=8) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.workdays') AND c.name=N'shift_assignment_id' AND t.name=N'bigint' AND c.is_nullable=1) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.workdays') AND c.name=N'started_at' AND t.name=N'datetime2' AND c.is_nullable=0 AND c.scale=7) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.workdays') AND c.name=N'status' AND t.name=N'varchar' AND c.is_nullable=0 AND c.max_length=10 AND c.collation_name=N'Latin1_General_100_BIN2') THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.workdays') AND c.name=N'unscheduled_reason' AND t.name=N'nvarchar' AND c.is_nullable=1 AND c.max_length=2000) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.workdays') AND c.name=N'user_id' AND t.name=N'bigint' AND c.is_nullable=0) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.workdays') AND c.name=N'work_date' AND t.name=N'date' AND c.is_nullable=0) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.workdays') AND referenced_object_id=OBJECT_ID('dbo.branches') AND name=N'FK_workdays_branch' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0) THROW 51897,'WorkTime FK missing/untrusted.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.foreign_keys f ON c.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID('dbo.workdays') AND f.name=N'FK_workdays_branch' AND c.constraint_column_id=1 AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'branch_id' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'branch_id') THROW 51897,'WorkTime FK column identity mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.workdays') AND referenced_object_id=OBJECT_ID('dbo.departments') AND name=N'FK_workdays_department' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0) THROW 51897,'WorkTime FK missing/untrusted.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.foreign_keys f ON c.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID('dbo.workdays') AND f.name=N'FK_workdays_department' AND c.constraint_column_id=1 AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'department_id' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'department_id') THROW 51897,'WorkTime FK column identity mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.workdays') AND referenced_object_id=OBJECT_ID('dbo.shift_assignments') AND name=N'FK_workdays_assignment' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0) THROW 51897,'WorkTime FK missing/untrusted.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.foreign_keys f ON c.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID('dbo.workdays') AND f.name=N'FK_workdays_assignment' AND c.constraint_column_id=1 AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'shift_assignment_id' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'shift_assignment_id') THROW 51897,'WorkTime FK column identity mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.workdays') AND referenced_object_id=OBJECT_ID('dbo.users') AND name=N'FK_workdays_user' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0) THROW 51897,'WorkTime FK missing/untrusted.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.foreign_keys f ON c.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID('dbo.workdays') AND f.name=N'FK_workdays_user' AND c.constraint_column_id=1 AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'user_id' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'user_id') THROW 51897,'WorkTime FK column identity mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID('dbo.workdays') AND name=N'PK_workdays') THROW 51898,'WorkTime key missing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID('dbo.workdays') AND name=N'UQ_workdays_identity') THROW 51898,'WorkTime key missing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.workdays') AND name=N'IX_workdays_branch_id' AND is_unique=0 AND is_disabled=0 AND filter_definition IS NULL) THROW 51899,'WorkTime serialization index mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.workdays') AND name=N'IX_workdays_department_id' AND is_unique=0 AND is_disabled=0 AND filter_definition IS NULL) THROW 51899,'WorkTime serialization index mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.workdays') AND name=N'IX_workdays_shift_assignment_id' AND is_unique=0 AND is_disabled=0 AND filter_definition IS NULL) THROW 51899,'WorkTime serialization index mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.workdays') AND name=N'UX_workdays_one_working' AND is_unique=1 AND is_disabled=0 AND REPLACE(REPLACE(REPLACE(filter_definition,'(',''),')',''),' ','')=N'[status]=''WORKING''') THROW 51899,'WorkTime serialization index mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.workdays') AND name=N'IX_workdays_user_date' AND is_unique=0 AND is_disabled=0 AND filter_definition IS NULL) THROW 51899,'WorkTime serialization index mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.workdays') AND i.name=N'PK_workdays' AND c.key_ordinal>0)<>1 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.workdays') AND x.name=N'PK_workdays' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'workday_id') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.workdays') AND i.name=N'UQ_workdays_identity' AND c.key_ordinal>0)<>3 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.workdays') AND x.name=N'UQ_workdays_identity' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'workday_id') THROW 51899,'WorkTime index column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.workdays') AND x.name=N'UQ_workdays_identity' AND c.key_ordinal=2 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'user_id') THROW 51899,'WorkTime index column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.workdays') AND x.name=N'UQ_workdays_identity' AND c.key_ordinal=3 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'branch_id') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.workdays') AND i.name=N'IX_workdays_branch_id' AND c.key_ordinal>0)<>1 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.workdays') AND x.name=N'IX_workdays_branch_id' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'branch_id') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.workdays') AND i.name=N'IX_workdays_department_id' AND c.key_ordinal>0)<>1 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.workdays') AND x.name=N'IX_workdays_department_id' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'department_id') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.workdays') AND i.name=N'IX_workdays_shift_assignment_id' AND c.key_ordinal>0)<>1 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.workdays') AND x.name=N'IX_workdays_shift_assignment_id' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'shift_assignment_id') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.workdays') AND i.name=N'UX_workdays_one_working' AND c.key_ordinal>0)<>1 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.workdays') AND x.name=N'UX_workdays_one_working' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'user_id') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.workdays') AND i.name=N'IX_workdays_user_date' AND c.key_ordinal>0)<>3 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.workdays') AND x.name=N'IX_workdays_user_date' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'user_id') THROW 51899,'WorkTime index column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.workdays') AND x.name=N'IX_workdays_user_date' AND c.key_ordinal=2 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'work_date') THROW 51899,'WorkTime index column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.workdays') AND x.name=N'IX_workdays_user_date' AND c.key_ordinal=3 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'workday_id') THROW 51899,'WorkTime index column mismatch.',1;
IF OBJECT_ID('dbo.work_intervals','U') IS NULL THROW 51895,'Missing mapped WorkTime table.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.work_intervals'))<>13 THROW 51896,'WorkTime column count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.work_intervals') AND c.name=N'interval_id' AND t.name=N'bigint' AND c.is_nullable=0 AND c.is_identity=1) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.work_intervals') AND c.name=N'branch_id' AND t.name=N'bigint' AND c.is_nullable=0) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.work_intervals') AND c.name=N'closed_at' AND t.name=N'datetime2' AND c.is_nullable=1 AND c.scale=7) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.work_intervals') AND c.name=N'closed_by' AND t.name=N'bigint' AND c.is_nullable=1) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.work_intervals') AND c.name=N'effective_ended_at' AND t.name=N'datetime2' AND c.is_nullable=1 AND c.scale=7) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.work_intervals') AND c.name=N'effective_started_at' AND t.name=N'datetime2' AND c.is_nullable=0 AND c.scale=7) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.work_intervals') AND c.name=N'opened_at' AND t.name=N'datetime2' AND c.is_nullable=0 AND c.scale=7) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.work_intervals') AND c.name=N'opened_by' AND t.name=N'bigint' AND c.is_nullable=0) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.work_intervals') AND c.name=N'row_version' AND t.name=N'timestamp' AND c.is_nullable=0 AND c.system_type_id=189 AND c.max_length=8) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.work_intervals') AND c.name=N'user_id' AND t.name=N'bigint' AND c.is_nullable=0) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.work_intervals') AND c.name=N'work_item_id' AND t.name=N'bigint' AND c.is_nullable=0) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.work_intervals') AND c.name=N'work_item_kind' AND t.name=N'varchar' AND c.is_nullable=0 AND c.max_length=24 AND c.collation_name=N'Latin1_General_100_BIN2') THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.work_intervals') AND c.name=N'workday_id' AND t.name=N'bigint' AND c.is_nullable=0) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.work_intervals') AND referenced_object_id=OBJECT_ID('dbo.users') AND name=N'FK_work_intervals_closed_by' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0) THROW 51897,'WorkTime FK missing/untrusted.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.foreign_keys f ON c.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID('dbo.work_intervals') AND f.name=N'FK_work_intervals_closed_by' AND c.constraint_column_id=1 AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'closed_by' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'user_id') THROW 51897,'WorkTime FK column identity mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.work_intervals') AND referenced_object_id=OBJECT_ID('dbo.users') AND name=N'FK_work_intervals_opened_by' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0) THROW 51897,'WorkTime FK missing/untrusted.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.foreign_keys f ON c.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID('dbo.work_intervals') AND f.name=N'FK_work_intervals_opened_by' AND c.constraint_column_id=1 AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'opened_by' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'user_id') THROW 51897,'WorkTime FK column identity mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.work_intervals') AND referenced_object_id=OBJECT_ID('dbo.workdays') AND name=N'FK_work_intervals_workday' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0) THROW 51897,'WorkTime FK missing/untrusted.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.foreign_keys f ON c.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID('dbo.work_intervals') AND f.name=N'FK_work_intervals_workday' AND c.constraint_column_id=1 AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'workday_id' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'workday_id') THROW 51897,'WorkTime FK column identity mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.foreign_keys f ON c.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID('dbo.work_intervals') AND f.name=N'FK_work_intervals_workday' AND c.constraint_column_id=2 AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'user_id' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'user_id') THROW 51897,'WorkTime FK column identity mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.foreign_keys f ON c.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID('dbo.work_intervals') AND f.name=N'FK_work_intervals_workday' AND c.constraint_column_id=3 AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'branch_id' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'branch_id') THROW 51897,'WorkTime FK column identity mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID('dbo.work_intervals') AND name=N'PK_work_intervals') THROW 51898,'WorkTime key missing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.work_intervals') AND name=N'IX_work_intervals_closed_by' AND is_unique=0 AND is_disabled=0 AND filter_definition IS NULL) THROW 51899,'WorkTime serialization index mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.work_intervals') AND name=N'IX_work_intervals_opened_by' AND is_unique=0 AND is_disabled=0 AND filter_definition IS NULL) THROW 51899,'WorkTime serialization index mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.work_intervals') AND name=N'UX_work_intervals_one_open' AND is_unique=1 AND is_disabled=0 AND REPLACE(REPLACE(REPLACE(filter_definition,'(',''),')',''),' ','')=N'[closed_at]ISNULL') THROW 51899,'WorkTime serialization index mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.work_intervals') AND name=N'IX_work_intervals_user_time' AND is_unique=0 AND is_disabled=0 AND filter_definition IS NULL) THROW 51899,'WorkTime serialization index mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.work_intervals') AND name=N'IX_work_intervals_item' AND is_unique=0 AND is_disabled=0 AND filter_definition IS NULL) THROW 51899,'WorkTime serialization index mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.work_intervals') AND name=N'IX_work_intervals_workday_id_user_id_branch_id' AND is_unique=0 AND is_disabled=0 AND filter_definition IS NULL) THROW 51899,'WorkTime serialization index mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.work_intervals') AND i.name=N'PK_work_intervals' AND c.key_ordinal>0)<>1 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.work_intervals') AND x.name=N'PK_work_intervals' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'interval_id') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.work_intervals') AND i.name=N'IX_work_intervals_closed_by' AND c.key_ordinal>0)<>1 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.work_intervals') AND x.name=N'IX_work_intervals_closed_by' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'closed_by') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.work_intervals') AND i.name=N'IX_work_intervals_opened_by' AND c.key_ordinal>0)<>1 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.work_intervals') AND x.name=N'IX_work_intervals_opened_by' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'opened_by') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.work_intervals') AND i.name=N'UX_work_intervals_one_open' AND c.key_ordinal>0)<>1 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.work_intervals') AND x.name=N'UX_work_intervals_one_open' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'user_id') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.work_intervals') AND i.name=N'IX_work_intervals_user_time' AND c.key_ordinal>0)<>3 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.work_intervals') AND x.name=N'IX_work_intervals_user_time' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'user_id') THROW 51899,'WorkTime index column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.work_intervals') AND x.name=N'IX_work_intervals_user_time' AND c.key_ordinal=2 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'effective_started_at') THROW 51899,'WorkTime index column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.work_intervals') AND x.name=N'IX_work_intervals_user_time' AND c.key_ordinal=3 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'interval_id') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.work_intervals') AND i.name=N'IX_work_intervals_item' AND c.key_ordinal>0)<>3 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.work_intervals') AND x.name=N'IX_work_intervals_item' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'work_item_kind') THROW 51899,'WorkTime index column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.work_intervals') AND x.name=N'IX_work_intervals_item' AND c.key_ordinal=2 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'work_item_id') THROW 51899,'WorkTime index column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.work_intervals') AND x.name=N'IX_work_intervals_item' AND c.key_ordinal=3 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'interval_id') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.work_intervals') AND i.name=N'IX_work_intervals_workday_id_user_id_branch_id' AND c.key_ordinal>0)<>3 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.work_intervals') AND x.name=N'IX_work_intervals_workday_id_user_id_branch_id' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'workday_id') THROW 51899,'WorkTime index column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.work_intervals') AND x.name=N'IX_work_intervals_workday_id_user_id_branch_id' AND c.key_ordinal=2 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'user_id') THROW 51899,'WorkTime index column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.work_intervals') AND x.name=N'IX_work_intervals_workday_id_user_id_branch_id' AND c.key_ordinal=3 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'branch_id') THROW 51899,'WorkTime index column mismatch.',1;
IF OBJECT_ID('dbo.time_correction_requests','U') IS NULL THROW 51895,'Missing mapped WorkTime table.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.time_correction_requests'))<>17 THROW 51896,'WorkTime column count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'correction_id' AND t.name=N'bigint' AND c.is_nullable=0 AND c.is_identity=1) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'applied_ended_at' AND t.name=N'datetime2' AND c.is_nullable=1 AND c.scale=7) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'applied_started_at' AND t.name=N'datetime2' AND c.is_nullable=1 AND c.scale=7) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'decided_at' AND t.name=N'datetime2' AND c.is_nullable=1 AND c.scale=7) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'decided_by' AND t.name=N'bigint' AND c.is_nullable=1) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'decision_reason' AND t.name=N'nvarchar' AND c.is_nullable=1 AND c.max_length=2000) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'interval_id' AND t.name=N'bigint' AND c.is_nullable=0) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'original_ended_at' AND t.name=N'datetime2' AND c.is_nullable=0 AND c.scale=7) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'original_interval_version' AND t.name=N'binary' AND c.is_nullable=0 AND c.max_length=8) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'original_started_at' AND t.name=N'datetime2' AND c.is_nullable=0 AND c.scale=7) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'proposed_ended_at' AND t.name=N'datetime2' AND c.is_nullable=0 AND c.scale=7) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'proposed_started_at' AND t.name=N'datetime2' AND c.is_nullable=0 AND c.scale=7) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'reason' AND t.name=N'nvarchar' AND c.is_nullable=0 AND c.max_length=2000) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'requested_at' AND t.name=N'datetime2' AND c.is_nullable=0 AND c.scale=7) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'requested_by' AND t.name=N'bigint' AND c.is_nullable=0) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'row_version' AND t.name=N'timestamp' AND c.is_nullable=0 AND c.system_type_id=189 AND c.max_length=8) THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE c.object_id=OBJECT_ID('dbo.time_correction_requests') AND c.name=N'status' AND t.name=N'varchar' AND c.is_nullable=0 AND c.max_length=10 AND c.collation_name=N'Latin1_General_100_BIN2') THROW 51896,'Mapped WorkTime column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.time_correction_requests') AND referenced_object_id=OBJECT_ID('dbo.users') AND name=N'FK_time_corrections_decider' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0) THROW 51897,'WorkTime FK missing/untrusted.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.foreign_keys f ON c.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID('dbo.time_correction_requests') AND f.name=N'FK_time_corrections_decider' AND c.constraint_column_id=1 AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'decided_by' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'user_id') THROW 51897,'WorkTime FK column identity mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.time_correction_requests') AND referenced_object_id=OBJECT_ID('dbo.work_intervals') AND name=N'FK_time_corrections_interval' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0) THROW 51897,'WorkTime FK missing/untrusted.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.foreign_keys f ON c.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID('dbo.time_correction_requests') AND f.name=N'FK_time_corrections_interval' AND c.constraint_column_id=1 AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'interval_id' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'interval_id') THROW 51897,'WorkTime FK column identity mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.time_correction_requests') AND referenced_object_id=OBJECT_ID('dbo.users') AND name=N'FK_time_corrections_requester' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0) THROW 51897,'WorkTime FK missing/untrusted.',1;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_key_columns c JOIN sys.foreign_keys f ON c.constraint_object_id=f.object_id WHERE f.parent_object_id=OBJECT_ID('dbo.time_correction_requests') AND f.name=N'FK_time_corrections_requester' AND c.constraint_column_id=1 AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'requested_by' AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'user_id') THROW 51897,'WorkTime FK column identity mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID('dbo.time_correction_requests') AND name=N'PK_time_correction_requests') THROW 51898,'WorkTime key missing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.time_correction_requests') AND name=N'IX_time_correction_requests_decided_by' AND is_unique=0 AND is_disabled=0 AND filter_definition IS NULL) THROW 51899,'WorkTime serialization index mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.time_correction_requests') AND name=N'IX_time_correction_requests_requested_by' AND is_unique=0 AND is_disabled=0 AND filter_definition IS NULL) THROW 51899,'WorkTime serialization index mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.time_correction_requests') AND name=N'IX_time_corrections_interval' AND is_unique=0 AND is_disabled=0 AND filter_definition IS NULL) THROW 51899,'WorkTime serialization index mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.time_correction_requests') AND i.name=N'PK_time_correction_requests' AND c.key_ordinal>0)<>1 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.time_correction_requests') AND x.name=N'PK_time_correction_requests' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'correction_id') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.time_correction_requests') AND i.name=N'IX_time_correction_requests_decided_by' AND c.key_ordinal>0)<>1 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.time_correction_requests') AND x.name=N'IX_time_correction_requests_decided_by' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'decided_by') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.time_correction_requests') AND i.name=N'IX_time_correction_requests_requested_by' AND c.key_ordinal>0)<>1 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.time_correction_requests') AND x.name=N'IX_time_correction_requests_requested_by' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'requested_by') THROW 51899,'WorkTime index column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.index_columns c JOIN sys.indexes i ON i.object_id=c.object_id AND i.index_id=c.index_id WHERE i.object_id=OBJECT_ID('dbo.time_correction_requests') AND i.name=N'IX_time_corrections_interval' AND c.key_ordinal>0)<>3 THROW 51899,'WorkTime index key count mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.time_correction_requests') AND x.name=N'IX_time_corrections_interval' AND c.key_ordinal=1 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'interval_id') THROW 51899,'WorkTime index column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.time_correction_requests') AND x.name=N'IX_time_corrections_interval' AND c.key_ordinal=2 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'requested_at') THROW 51899,'WorkTime index column mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.indexes x ON x.object_id=c.object_id AND x.index_id=c.index_id WHERE x.object_id=OBJECT_ID('dbo.time_correction_requests') AND x.name=N'IX_time_corrections_interval' AND c.key_ordinal=3 AND c.is_descending_key=0 AND COL_NAME(c.object_id,c.column_id)=N'correction_id') THROW 51899,'WorkTime index column mismatch.',1;
SELECT 'WORKTIME_SCHEMA_CANDIDATE_VERIFIED' AS result;

COMMIT;
