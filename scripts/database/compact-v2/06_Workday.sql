-- Additive upgrade. Run through Upgrade-Workday.ps1 after a verified backup.
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE 'HotelOpsCompact%' THROW 51401,'Compact database required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOpsCompactSchema' AND CONVERT(varchar(20),value)='2.0.0')
 THROW 51402,'Expected schema 2.0.0.',1;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>37 THROW 51403,'Unexpected table budget.',1;
CREATE TABLE dbo.workday_shifts(
 shift_id bigint IDENTITY PRIMARY KEY,
 branch_id bigint NOT NULL REFERENCES dbo.branches(branch_id),
 department_id bigint NOT NULL REFERENCES dbo.departments(department_id),
 role_id bigint NOT NULL REFERENCES dbo.roles(role_id),
 name nvarchar(150) NOT NULL,
 start_minute int NOT NULL, end_minute int NOT NULL, capacity int NOT NULL,
 is_active bit NOT NULL DEFAULT 1,
 row_version rowversion NOT NULL,
 CONSTRAINT CK_workday_shift_time CHECK(start_minute>=0 AND end_minute<=1440 AND start_minute<end_minute),
 CONSTRAINT CK_workday_shift_capacity CHECK(capacity BETWEEN 1 AND 100),
 CONSTRAINT UQ_workday_shift UNIQUE(branch_id,department_id,role_id,start_minute,end_minute)
);
CREATE TABLE dbo.workday_assignments(
 assignment_id bigint IDENTITY PRIMARY KEY,
 shift_id bigint NOT NULL REFERENCES dbo.workday_shifts(shift_id),
 user_id bigint NOT NULL REFERENCES dbo.users(user_id),
 work_date date NOT NULL,
 status varchar(20) NOT NULL DEFAULT 'PLANNED',
 started_at datetime2 NULL, ended_at datetime2 NULL,
 created_by bigint NOT NULL REFERENCES dbo.users(user_id),
 created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 row_version rowversion NOT NULL,
 CONSTRAINT CK_workday_status CHECK(status IN ('PLANNED','WORKING','ENDED','CANCELLED')),
 CONSTRAINT CK_workday_timestamps CHECK(
  (status IN ('PLANNED','CANCELLED') AND started_at IS NULL AND ended_at IS NULL) OR
  (status='WORKING' AND started_at IS NOT NULL AND ended_at IS NULL) OR
  (status='ENDED' AND started_at IS NOT NULL AND ended_at>=started_at))
);
CREATE UNIQUE INDEX UX_workday_one_active ON dbo.workday_assignments(user_id) WHERE status='WORKING';
CREATE UNIQUE INDEX UX_workday_assignment ON dbo.workday_assignments(shift_id,work_date,user_id) WHERE status<>'CANCELLED';
CREATE INDEX IX_workday_week ON dbo.workday_assignments(work_date,shift_id) INCLUDE(user_id,status);
CREATE TABLE dbo.workday_events(
 event_id bigint IDENTITY PRIMARY KEY,
 assignment_id bigint NOT NULL REFERENCES dbo.workday_assignments(assignment_id),
 actor_user_id bigint NOT NULL REFERENCES dbo.users(user_id),
 action varchar(20) NOT NULL,
 operation_key uniqueidentifier NOT NULL UNIQUE,
 reason nvarchar(2000) NULL,
 occurred_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT CK_workday_event_action CHECK(action IN ('ASSIGN','START','END','CANCEL'))
);
GO
CREATE TRIGGER dbo.TR_workday_events_immutable ON dbo.workday_events INSTEAD OF UPDATE,DELETE AS
 THROW 51404,'Workday history is immutable.',1;
GO
-- Reuse the legacy permission vocabulary. Grants are limited to the dedicated demo roles below.
INSERT permissions(code,module_code,description)
 SELECT v.code,'WORKDAY',v.description FROM (VALUES
 ('SHIFT_MANAGE',N'Lập lịch tuần trong chi nhánh'),('ATTENDANCE_RECORD',N'Bắt đầu/kết thúc ca của bản thân'))v(code,description)
 WHERE NOT EXISTS(SELECT 1 FROM permissions p WHERE p.code=v.code);
INSERT role_permissions(role_id,permission_id,data_scope)
 SELECT r.role_id,p.permission_id,CASE p.code WHEN 'SHIFT_MANAGE' THEN 'BRANCH' ELSE 'OWN' END
 FROM roles r CROSS JOIN permissions p
 WHERE ((r.code='DEPT_MANAGER' AND p.code='SHIFT_MANAGE') OR
 (r.code IN ('DEMO_MANAGER','DEMO_HOUSEKEEPER','DEMO_TECHNICIAN','RECEPTION','STOREKEEPER') AND p.code='ATTENDANCE_RECORD'))
 AND NOT EXISTS(SELECT 1 FROM role_permissions rp WHERE rp.role_id=r.role_id AND rp.permission_id=p.permission_id);
-- Seed only known demo branches/departments/roles, resolve all IDs from business codes.
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity)
 SELECT b.branch_id,d.department_id,r.role_id,v.name,v.start_minute,v.end_minute,v.capacity
 FROM (VALUES
 ('HK','DEMO_MANAGER',N'Giám sát buồng • đêm',0,480,1),('HK','DEMO_MANAGER',N'Giám sát buồng • sáng',480,960,1),('HK','DEMO_MANAGER',N'Giám sát buồng • chiều',960,1440,1),
 ('HK','DEMO_HOUSEKEEPER',N'Buồng phòng • đêm',0,480,4),('HK','DEMO_HOUSEKEEPER',N'Buồng phòng • sáng',480,960,4),('HK','DEMO_HOUSEKEEPER',N'Buồng phòng • chiều',960,1440,4),
 ('ENG','DEMO_MANAGER',N'Trưởng kỹ thuật • sáng',480,690,1),('ENG','DEMO_MANAGER',N'Trưởng kỹ thuật • chiều',780,1020,1),
 ('ENG','DEMO_TECHNICIAN',N'Kỹ thuật • sáng',480,690,3),('ENG','DEMO_TECHNICIAN',N'Kỹ thuật • chiều',780,1020,3),
 ('STORE','STOREKEEPER',N'Kho • sáng',480,690,1),('STORE','STOREKEEPER',N'Kho • chiều',780,1020,1),
 ('FO','RECEPTION',N'Lễ tân • đêm',0,480,1),('FO','RECEPTION',N'Lễ tân • sáng',480,960,1),('FO','RECEPTION',N'Lễ tân • chiều',960,1440,1)
 )v(department,role,name,start_minute,end_minute,capacity)
 JOIN departments d ON d.code=v.department JOIN branches b ON b.branch_id=d.branch_id AND b.code IN ('DEMO-SG','DEMO-DN')
 JOIN roles r ON r.code=v.role;
EXEC sys.sp_updateextendedproperty @name=N'HotelOpsCompactSchema',@value=N'2.1.0';
