-- DESIGN CANDIDATE ONLY. D1-D4 design acceptance does not authorize execution.
-- Run only as a separate provisioning administrator AFTER exact artifact approval.
SET NOCOUNT ON; SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'MEITA\MSSQLSERVER01'
 OR DB_NAME() NOT LIKE N'HotelOps[_]UC012[_]Trial[_]%'
 THROW 51800,'Exact disposable target required',1;
DECLARE @guid uniqueidentifier=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'UC012.DatabaseGuid'));
IF @guid IS NULL OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid=@guid)
 THROW 51801,'Actual approved GUID required',1;
IF NULLIF(CONVERT(nvarchar(256),SESSION_CONTEXT(N'UC012.ExecutionApproval')),N'') IS NULL
 THROW 51802,'Separate owner execution approval required',1;
-- Session values are evidence bindings, never independent proof of owner authorization.

IF EXISTS(SELECT 1 FROM sys.tables WHERE is_ms_shipped=0) THROW 51803,'Empty database required; no upgrade',1;
BEGIN TRY
 BEGIN TRANSACTION;
CREATE TABLE [branches] (
    [branch_id] bigint NOT NULL IDENTITY,
    [branch_code] varchar(30) NOT NULL,
    [branch_name] nvarchar(200) NOT NULL,
    [address] nvarchar(max) NULL,
    [phone] varchar(20) NULL,
    [email] varchar(150) NULL,
    [timezone] varchar(60) NULL DEFAULT 'Asia/Ho_Chi_Minh',
    [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
    [created_at] datetime2 NULL,
    [updated_at] datetime2 NULL,
    CONSTRAINT [PK__branches__E55E37DEE9AE3269] PRIMARY KEY ([branch_id])
);

CREATE TABLE [permissions] (
    [permission_id] bigint NOT NULL IDENTITY,
    [permission_code] varchar(100) NOT NULL,
    [permission_name] nvarchar(150) NOT NULL,
    [module_code] varchar(60) NOT NULL,
    [resource_code] varchar(60) NOT NULL,
    [action_code] varchar(40) NOT NULL,
    [description] nvarchar(max) NULL,
    CONSTRAINT [PK__permissi__E5331AFA6BFD8720] PRIMARY KEY ([permission_id])
);

CREATE TABLE [roles] (
    [role_id] bigint NOT NULL IDENTITY,
    [role_code] varchar(60) NOT NULL,
    [role_name] nvarchar(150) NOT NULL,
    [description] nvarchar(max) NULL,
    [is_system_role] bit NOT NULL,
    [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
    [created_at] datetime2 NULL,
    CONSTRAINT [PK__roles__760965CC49349513] PRIMARY KEY ([role_id])
);

CREATE TABLE [audit_logs] (
    [audit_id] bigint NOT NULL IDENTITY,
    [branch_id] bigint NULL,
    [actor_user_id] bigint NULL,
    [entity_type] varchar(100) NOT NULL,
    [entity_id] varchar(100) NOT NULL,
    [action] varchar(40) NOT NULL,
    [old_values] nvarchar(max) NULL,
    [new_values] nvarchar(max) NULL,
    [changed_fields] nvarchar(max) NULL,
    [ip_address] varchar(45) NULL,
    [request_id] varchar(100) NULL,
    [user_agent] nvarchar(max) NULL,
    [occurred_at] datetime2 NOT NULL,
    CONSTRAINT [PK__audit_lo__5AF33E333EAC675C] PRIMARY KEY ([audit_id]),
    CONSTRAINT [FK_audit_logs_branch_id] FOREIGN KEY ([branch_id]) REFERENCES [branches] ([branch_id])
);

CREATE TABLE [departments] (
    [department_id] bigint NOT NULL IDENTITY,
    [branch_id] bigint NOT NULL,
    [department_code] varchar(30) NOT NULL,
    [department_name] nvarchar(150) NOT NULL,
    [manager_user_id] bigint NULL,
    [scope_description] nvarchar(max) NULL,
    [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK__departme__C22324222C79F96D] PRIMARY KEY ([department_id]),
    CONSTRAINT [FK_departments_branch_id] FOREIGN KEY ([branch_id]) REFERENCES [branches] ([branch_id])
);

CREATE TABLE [users] (
    [user_id] bigint NOT NULL IDENTITY,
    [employee_code] varchar(40) NOT NULL,
    [username] varchar(100) NOT NULL,
    [email] varchar(150) NOT NULL,
    [password_hash] varchar(255) NULL,
    [failed_login_count] int NOT NULL CONSTRAINT [DF_users_failed_login_count] DEFAULT 0,
    [locked_until] datetime2(7) NULL,
    [temporary_password_expires_at] datetime2(7) NULL,
    [full_name] nvarchar(200) NOT NULL,
    [phone] varchar(20) NULL,
    [avatar_url] nvarchar(max) NULL,
    [primary_branch_id] bigint NULL,
    [department_id] bigint NULL,
    [manager_user_id] bigint NULL,
    [employment_status] varchar(30) NOT NULL DEFAULT 'ACTIVE',
    [account_status] varchar(30) NOT NULL DEFAULT 'ACTIVE',
    [last_login_at] datetime2 NULL,
    [created_at] datetime2 NOT NULL,
    [updated_at] datetime2 NULL,
    [deleted_at] datetime2 NULL,
    [row_version] rowversion NOT NULL,
    CONSTRAINT [PK__users__B9BE370FD4563718] PRIMARY KEY ([user_id]),
    CONSTRAINT [CK_users_failed_login_count] CHECK ([failed_login_count] >= 0),
    CONSTRAINT [FK_users_department_id] FOREIGN KEY ([department_id]) REFERENCES [departments] ([department_id]),
    CONSTRAINT [FK_users_manager_user_id] FOREIGN KEY ([manager_user_id]) REFERENCES [users] ([user_id]),
    CONSTRAINT [FK_users_primary_branch_id] FOREIGN KEY ([primary_branch_id]) REFERENCES [branches] ([branch_id])
);

CREATE TABLE [role_permissions] (
    [role_permission_id] bigint NOT NULL IDENTITY,
    [role_id] bigint NOT NULL,
    [permission_id] bigint NOT NULL,
    [data_scope] varchar(20) NOT NULL DEFAULT (('BRANCH')),
    [granted_at] datetime2 NULL,
    [granted_by] bigint NULL,
    CONSTRAINT [PK__role_per__B1E85A10C926A139] PRIMARY KEY ([role_permission_id]),
    CONSTRAINT [FK_role_permissions_granted_by] FOREIGN KEY ([granted_by]) REFERENCES [users] ([user_id]),
    CONSTRAINT [FK_role_permissions_permission_id] FOREIGN KEY ([permission_id]) REFERENCES [permissions] ([permission_id]),
    CONSTRAINT [FK_role_permissions_role_id] FOREIGN KEY ([role_id]) REFERENCES [roles] ([role_id])
);

CREATE TABLE [user_branch_access] (
    [access_id] bigint NOT NULL IDENTITY,
    [user_id] bigint NOT NULL,
    [branch_id] bigint NOT NULL,
    [access_level] varchar(30) NOT NULL DEFAULT 'STANDARD',
    [is_primary] bit NOT NULL,
    [granted_by] bigint NULL,
    [granted_at] datetime2 NOT NULL,
    [revoked_at] datetime2 NULL,
    CONSTRAINT [PK__user_bra__10FA1E20F44C43BC] PRIMARY KEY ([access_id]),
    CONSTRAINT [FK_user_branch_access_branch_id] FOREIGN KEY ([branch_id]) REFERENCES [branches] ([branch_id]),
    CONSTRAINT [FK_user_branch_access_granted_by] FOREIGN KEY ([granted_by]) REFERENCES [users] ([user_id]),
    CONSTRAINT [FK_user_branch_access_user_id] FOREIGN KEY ([user_id]) REFERENCES [users] ([user_id])
);

CREATE TABLE [user_roles] (
    [user_role_id] bigint NOT NULL IDENTITY,
    [user_id] bigint NOT NULL,
    [role_id] bigint NOT NULL,
    [branch_id] bigint NULL,
    [assigned_by] bigint NULL,
    [assigned_at] datetime2 NOT NULL,
    [expires_at] datetime2 NULL,
    [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK__user_rol__B8D9ABA2A37D4098] PRIMARY KEY ([user_role_id]),
    CONSTRAINT [FK_user_roles_assigned_by] FOREIGN KEY ([assigned_by]) REFERENCES [users] ([user_id]),
    CONSTRAINT [FK_user_roles_branch_id] FOREIGN KEY ([branch_id]) REFERENCES [branches] ([branch_id]),
    CONSTRAINT [FK_user_roles_role_id] FOREIGN KEY ([role_id]) REFERENCES [roles] ([role_id]),
    CONSTRAINT [FK_user_roles_user_id] FOREIGN KEY ([user_id]) REFERENCES [users] ([user_id])
);

CREATE TABLE [user_sessions] (
    [session_id] bigint NOT NULL IDENTITY,
    [user_id] bigint NOT NULL,
    [session_key_hash] varchar(255) NOT NULL,
    [device_info] nvarchar(max) NULL,
    [ip_address] varchar(45) NULL,
    [user_agent] nvarchar(max) NULL,
    [created_at] datetime2 NOT NULL,
    [expires_at] datetime2 NOT NULL,
    [revoked_at] datetime2 NULL,
    [last_used_at] datetime2 NULL,
    CONSTRAINT [PK__user_ses__69B13FDCB96F3735] PRIMARY KEY ([session_id]),
    CONSTRAINT [FK_user_sessions_user_id] FOREIGN KEY ([user_id]) REFERENCES [users] ([user_id])
);

CREATE INDEX [audit_logs_index_45] ON [audit_logs] ([entity_type], [entity_id], [occurred_at]);

CREATE INDEX [audit_logs_index_46] ON [audit_logs] ([actor_user_id], [occurred_at]);

CREATE INDEX [IX_audit_logs_branch_id] ON [audit_logs] ([branch_id]);

CREATE UNIQUE INDEX [UQ__branches__A9F83E3BF9DD6797] ON [branches] ([branch_code]);

CREATE UNIQUE INDEX [departments_index_4] ON [departments] ([branch_id], [department_code]);

CREATE INDEX [IX_departments_manager_user_id] ON [departments] ([manager_user_id]);

CREATE UNIQUE INDEX [UQ__permissi__A98A808E2C8B304E] ON [permissions] ([permission_code]);

CREATE INDEX [IX_role_permissions_granted_by] ON [role_permissions] ([granted_by]);

CREATE INDEX [IX_role_permissions_permission_id] ON [role_permissions] ([permission_id]);

CREATE UNIQUE INDEX [role_permissions_index_40] ON [role_permissions] ([role_id], [permission_id]);

CREATE UNIQUE INDEX [UQ__roles__BAE630754150AC14] ON [roles] ([role_code]);

CREATE INDEX [IX_user_branch_access_branch_id] ON [user_branch_access] ([branch_id]);

CREATE INDEX [IX_user_branch_access_granted_by] ON [user_branch_access] ([granted_by]);

CREATE UNIQUE INDEX [user_branch_access_index_41] ON [user_branch_access] ([user_id], [branch_id]);

CREATE INDEX [IX_user_roles_assigned_by] ON [user_roles] ([assigned_by]);

CREATE INDEX [IX_user_roles_branch_id] ON [user_roles] ([branch_id]);

CREATE INDEX [IX_user_roles_role_id] ON [user_roles] ([role_id]);

CREATE UNIQUE INDEX [user_roles_index_39] ON [user_roles] ([user_id], [role_id], [branch_id]) WHERE [branch_id] IS NOT NULL;

CREATE INDEX [IX_user_sessions_user_id] ON [user_sessions] ([user_id]);

CREATE UNIQUE INDEX [UQ__user_ses__63546C90989E9852] ON [user_sessions] ([session_key_hash]);

CREATE INDEX [IX_users_department_id] ON [users] ([department_id]);

CREATE INDEX [IX_users_manager_user_id] ON [users] ([manager_user_id]);

CREATE UNIQUE INDEX [UQ__users__AB6E61641B43B5D5] ON [users] ([email]);

CREATE UNIQUE INDEX [UQ__users__B0AA73458F47F1B4] ON [users] ([employee_code]);

CREATE UNIQUE INDEX [UQ__users__F3DBC57220477F7A] ON [users] ([username]);

CREATE INDEX [users_index_38] ON [users] ([primary_branch_id], [department_id]);

ALTER TABLE [audit_logs] ADD CONSTRAINT [FK_audit_logs_actor_user_id] FOREIGN KEY ([actor_user_id]) REFERENCES [users] ([user_id]);

ALTER TABLE [departments] ADD CONSTRAINT [FK_departments_manager_user_id] FOREIGN KEY ([manager_user_id]) REFERENCES [users] ([user_id]);
ALTER TABLE dbo.branches ADD row_version rowversion NOT NULL;
-- Database creation must already use this reviewed CI collation; never alter existing data.
IF (SELECT collation_name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.branches') AND name='branch_code') NOT LIKE '%[_]CI[_]%'
 THROW 51804,'CI collation required',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') AND name='row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0)
 THROW 51805,'User RowVersion required',1;
EXEC sys.sp_addextendedproperty @name=N'HotelOps.UC012.Schema', @value=N'UC012.AuthBranchClosure.v1';
 COMMIT;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
