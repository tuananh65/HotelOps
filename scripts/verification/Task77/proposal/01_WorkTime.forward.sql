-- REVIEW CANDIDATE ONLY. No EF migration. Do not execute before explicit approval.
-- Runner must set this session flag AFTER approval and guarded disposable-trial preparation.
SET XACT_ABORT ON;
IF COALESCE(TRY_CONVERT(int, SESSION_CONTEXT(N'HotelOps.Task77Approved')), 0) <> 1
    THROW 51770, 'Task77 shared schema approval is required.', 1;
IF LEN(DB_NAME()) <> 49 OR LEFT(DB_NAME(),19) <> N'HotelOps_Canonical_'
    OR SUBSTRING(DB_NAME(),20,8) LIKE N'%[^0-9]%' OR SUBSTRING(DB_NAME(),28,1) <> N'_'
    OR SUBSTRING(DB_NAME(),29,6) LIKE N'%[^0-9]%' OR SUBSTRING(DB_NAME(),35,1) <> N'_'
    OR SUBSTRING(DB_NAME(),36,8) COLLATE Latin1_General_100_BIN2 LIKE N'%[^a-f0-9]%'
    OR RIGHT(DB_NAME(),6) <> N'_Fresh'
    OR NOT EXISTS (SELECT 1 FROM sys.extended_properties
        WHERE class = 0 AND name = N'HotelOps.UserDirectory.Trial' AND CONVERT(nvarchar(128), value) = N'Task17')
    THROW 51771, 'Only a guarded disposable canonical trial is allowed.', 1;
IF @@TRANCOUNT <> 0 THROW 51772, 'Schema runner must not have an ambient transaction.', 1;
IF OBJECT_ID(N'dbo.workdays') IS NOT NULL OR OBJECT_ID(N'dbo.work_intervals') IS NOT NULL
    OR OBJECT_ID(N'dbo.time_correction_requests') IS NOT NULL
    THROW 51773, 'Existing time tables require explicit compatibility review; no overwrite or replay.', 1;

BEGIN TRANSACTION;
CREATE TABLE dbo.workdays
(
    workday_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_workdays PRIMARY KEY,
    user_id bigint NOT NULL,
    branch_id bigint NOT NULL,
    department_id bigint NOT NULL,
    work_date date NOT NULL,
    shift_assignment_id bigint NULL,
    is_unscheduled bit NOT NULL,
    unscheduled_reason nvarchar(1000) NULL,
    status varchar(10) COLLATE Latin1_General_100_BIN2 NOT NULL,
    started_at datetime2(7) NOT NULL CONSTRAINT DF_workdays_started DEFAULT SYSUTCDATETIME(),
    ended_at datetime2(7) NULL,
    row_version rowversion NOT NULL,
    CONSTRAINT FK_workdays_user FOREIGN KEY(user_id) REFERENCES dbo.users(user_id),
    CONSTRAINT FK_workdays_branch FOREIGN KEY(branch_id) REFERENCES dbo.branches(branch_id),
    CONSTRAINT FK_workdays_department FOREIGN KEY(department_id) REFERENCES dbo.departments(department_id),
    CONSTRAINT FK_workdays_assignment FOREIGN KEY(shift_assignment_id) REFERENCES dbo.shift_assignments(shift_assignment_id),
    CONSTRAINT UQ_workdays_identity UNIQUE(workday_id, user_id, branch_id),
    CONSTRAINT CK_workdays_state CHECK
        ((status = 'WORKING' AND ended_at IS NULL) OR (status = 'ENDED' AND ended_at IS NOT NULL AND ended_at >= started_at)),
    CONSTRAINT CK_workdays_schedule CHECK
        ((is_unscheduled = 0 AND shift_assignment_id IS NOT NULL AND unscheduled_reason IS NULL)
          OR (is_unscheduled = 1 AND shift_assignment_id IS NULL AND LEN(LTRIM(RTRIM(unscheduled_reason))) >= 10
              AND unscheduled_reason IS NOT NULL))
);
CREATE UNIQUE INDEX UX_workdays_one_working ON dbo.workdays(user_id) WHERE status = 'WORKING';
CREATE INDEX IX_workdays_user_date ON dbo.workdays(user_id, work_date, workday_id);

CREATE TABLE dbo.work_intervals
(
    interval_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_work_intervals PRIMARY KEY,
    workday_id bigint NOT NULL,
    user_id bigint NOT NULL,
    branch_id bigint NOT NULL,
    work_item_kind varchar(24) COLLATE Latin1_General_100_BIN2 NOT NULL,
    work_item_id bigint NOT NULL,
    opened_by bigint NOT NULL,
    opened_at datetime2(7) NOT NULL CONSTRAINT DF_work_intervals_opened DEFAULT SYSUTCDATETIME(),
    closed_by bigint NULL,
    closed_at datetime2(7) NULL,
    effective_started_at datetime2(7) NOT NULL,
    effective_ended_at datetime2(7) NULL,
    row_version rowversion NOT NULL,
    CONSTRAINT FK_work_intervals_workday FOREIGN KEY(workday_id, user_id, branch_id)
        REFERENCES dbo.workdays(workday_id, user_id, branch_id),
    CONSTRAINT FK_work_intervals_opened_by FOREIGN KEY(opened_by) REFERENCES dbo.users(user_id),
    CONSTRAINT FK_work_intervals_closed_by FOREIGN KEY(closed_by) REFERENCES dbo.users(user_id),
    CONSTRAINT CK_work_intervals_identity CHECK(work_item_id > 0 AND work_item_kind IN
        ('CLEANING', 'REWORK', 'INCIDENT', 'MAINTENANCE_OCCURRENCE', 'TASK')),
    CONSTRAINT CK_work_intervals_state CHECK
        ((closed_at IS NULL AND closed_by IS NULL AND effective_ended_at IS NULL AND effective_started_at = opened_at)
          OR (closed_at IS NOT NULL AND closed_by IS NOT NULL AND closed_at >= opened_at
              AND effective_ended_at IS NOT NULL AND effective_ended_at >= effective_started_at))
);
-- GLOBAL user key: deliberately not scoped by branch or work-item module.
CREATE UNIQUE INDEX UX_work_intervals_one_open ON dbo.work_intervals(user_id) WHERE closed_at IS NULL;
CREATE INDEX IX_work_intervals_user_time ON dbo.work_intervals(user_id, effective_started_at, interval_id);
CREATE INDEX IX_work_intervals_item ON dbo.work_intervals(work_item_kind, work_item_id, interval_id);

CREATE TABLE dbo.time_correction_requests
(
    correction_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_time_correction_requests PRIMARY KEY,
    interval_id bigint NOT NULL,
    requested_by bigint NOT NULL,
    requested_at datetime2(7) NOT NULL CONSTRAINT DF_time_corrections_requested DEFAULT SYSUTCDATETIME(),
    original_started_at datetime2(7) NOT NULL,
    original_ended_at datetime2(7) NOT NULL,
    original_interval_version binary(8) NOT NULL,
    proposed_started_at datetime2(7) NOT NULL,
    proposed_ended_at datetime2(7) NOT NULL,
    reason nvarchar(1000) NOT NULL,
    status varchar(10) COLLATE Latin1_General_100_BIN2 NOT NULL,
    decided_by bigint NULL,
    decided_at datetime2(7) NULL,
    decision_reason nvarchar(1000) NULL,
    applied_started_at datetime2(7) NULL,
    applied_ended_at datetime2(7) NULL,
    row_version rowversion NOT NULL,
    CONSTRAINT FK_time_corrections_interval FOREIGN KEY(interval_id) REFERENCES dbo.work_intervals(interval_id),
    CONSTRAINT FK_time_corrections_requester FOREIGN KEY(requested_by) REFERENCES dbo.users(user_id),
    CONSTRAINT FK_time_corrections_decider FOREIGN KEY(decided_by) REFERENCES dbo.users(user_id),
    CONSTRAINT CK_time_corrections_values CHECK
        (original_ended_at >= original_started_at AND proposed_ended_at >= proposed_started_at
          AND LEN(LTRIM(RTRIM(reason))) >= 10),
    CONSTRAINT CK_time_corrections_state CHECK
        ((status = 'PENDING' AND decided_by IS NULL AND decided_at IS NULL
            AND applied_started_at IS NULL AND applied_ended_at IS NULL AND decision_reason IS NULL)
          OR (status = 'APPLIED' AND decided_by IS NOT NULL AND decided_by <> requested_by AND decided_at IS NOT NULL AND decided_at >= requested_at
            AND applied_started_at IS NOT NULL AND applied_ended_at IS NOT NULL AND applied_ended_at >= applied_started_at)
          OR (status = 'REJECTED' AND decided_by IS NOT NULL AND decided_by <> requested_by AND decided_at IS NOT NULL AND decided_at >= requested_at
            AND applied_started_at IS NULL AND applied_ended_at IS NULL
            AND decision_reason IS NOT NULL AND LEN(LTRIM(RTRIM(decision_reason))) >= 10))
);
CREATE INDEX IX_time_corrections_interval ON dbo.time_correction_requests(interval_id, requested_at, correction_id);
EXEC sys.sp_addextendedproperty @name=N'HotelOps.WorkTime.Contract', @value=N'Task77-v1';
COMMIT TRANSACTION;
-- No seeds, grants, ShiftAttendance updates, triggers, existing-table changes or deployment to shared DBs.
