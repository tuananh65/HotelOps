-- FE04 execution foundation; run ONLY through the identity/backup guarded development installer.
-- WorkTime mapping source: ScheduledWorkdayTrialHost/candidate/01-install-candidate.sql.
-- Lifecycle source: release/fs013-candidate/01_Install.sql. No legacy task adoption or trial owner marker.
SET XACT_ABORT ON;
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

IF OBJECT_ID('dbo.room_cleaning_cycle_state') IS NULL
BEGIN
  IF EXISTS(SELECT room_id FROM dbo.cleaning_tasks WHERE status NOT IN('COMPLETED','CANCELLED') GROUP BY room_id HAVING COUNT(*)>1)
    THROW 51880,'Existing duplicate open cleaning tasks require explicit reconciliation.',1;
  CREATE UNIQUE INDEX UX_cleaning_tasks_open_room_v1 ON dbo.cleaning_tasks(room_id) WHERE status <> 'COMPLETED' AND status <> 'CANCELLED';
  CREATE UNIQUE INDEX UX_cleaning_task_room_v1 ON dbo.cleaning_tasks(cleaning_task_id,room_id);
  CREATE TABLE dbo.room_cleaning_cycle_state(
    room_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.rooms(room_id),
    cleaning_task_id bigint NOT NULL,
    row_version rowversion NOT NULL,
    CONSTRAINT FK_room_cycle_task_v1 FOREIGN KEY(cleaning_task_id,room_id) REFERENCES dbo.cleaning_tasks(cleaning_task_id,room_id));
  CREATE TABLE dbo.room_readiness_events(
    event_id bigint IDENTITY PRIMARY KEY,
    room_id bigint NOT NULL,
    cleaning_task_id bigint NOT NULL,
    readiness varchar(40) NOT NULL CHECK(readiness='PENDING_CLEANING'),
    has_effective_block bit NOT NULL,
    actor_user_id bigint NOT NULL REFERENCES dbo.users(user_id),
    occurred_at datetime2(7) NOT NULL,
    CONSTRAINT FK_readiness_task_room_v1 FOREIGN KEY(cleaning_task_id,room_id) REFERENCES dbo.cleaning_tasks(cleaning_task_id,room_id));
  EXEC sys.sp_addextendedproperty @name=N'HotelOps.CleaningLifecycle.V1',@value=N'FS013-V1';
END;
IF OBJECT_ID('dbo.room_readiness_events') IS NULL OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.cleaning_tasks') AND name='UX_cleaning_tasks_open_room_v1' AND is_unique=1)
  THROW 51881,'Partial lifecycle schema is not supported.',1;
-- Existing FS013 domain checks, with explicit non-NULL closed endpoints.
ALTER TABLE dbo.workdays ADD CONSTRAINT CK_dev3_workday_state CHECK(status IN('WORKING','ENDED')),
 CONSTRAINT CK_dev3_workday_time CHECK((status='WORKING' AND ended_at IS NULL) OR(status='ENDED' AND ended_at IS NOT NULL AND ended_at>=started_at));
ALTER TABLE dbo.work_intervals ADD CONSTRAINT CK_dev3_interval_time CHECK(
 (closed_at IS NULL AND closed_by IS NULL AND effective_ended_at IS NULL) OR
 (closed_at IS NOT NULL AND closed_by IS NOT NULL AND effective_ended_at IS NOT NULL AND closed_at>=opened_at AND effective_ended_at>=effective_started_at));
DECLARE @check sysname=(SELECT name FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.room_readiness_events') AND definition LIKE '%PENDING_CLEANING%');
IF @check IS NULL THROW 51960,'Readiness prerequisite missing',1;
DECLARE @drop nvarchar(max)=N'ALTER TABLE dbo.room_readiness_events DROP CONSTRAINT '+QUOTENAME(@check);
EXEC(@drop);
ALTER TABLE dbo.room_readiness_events WITH CHECK ADD CONSTRAINT CK_dev3_readiness_start CHECK(readiness IN('PENDING_CLEANING','CLEANING_IN_PROGRESS'));