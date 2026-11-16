SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'meita'
 OR DB_NAME() <> N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51831,'Exact owned disposable database required.',1;
IF ISNULL(COL_LENGTH('dbo.reference_values','group_code'),-1)<>60 OR ISNULL(COL_LENGTH('dbo.reference_values','value_code'),-1)<>60
 OR ISNULL(COL_LENGTH('dbo.reference_values','display_name'),-1)<>300 OR OBJECT_ID('dbo.task_progress_versions','U') IS NULL
 OR OBJECT_ID('dbo.work_intervals','U') IS NULL OR OBJECT_ID('dbo.operational_task_progress','U') IS NULL
 THROW 51831,'Required schema incompatible.',1;
BEGIN TRANSACTION;
DECLARE @reasons TABLE(code varchar(60) PRIMARY KEY,label nvarchar(150),sort_order int);
INSERT @reasons VALUES('WAITING_MATERIAL',N'Waiting for material',1),('WAITING_EQUIPMENT',N'Waiting for equipment',2),
 ('WAITING_APPROVAL',N'Waiting for approval',3),('ACCESS_BLOCKED',N'Access blocked',4),('SAFETY_ISSUE',N'Safety issue',5),('OTHER',N'Other',6);
IF EXISTS(SELECT 1 FROM dbo.reference_values r WITH(UPDLOCK,HOLDLOCK) LEFT JOIN @reasons s ON s.code=r.value_code
 WHERE r.group_code='TASK_PAUSE_REASON' AND
 ((s.code IS NULL AND NOT(r.value_code='END_OF_SHIFT' AND r.display_name=N'End of Shift')) OR r.display_name<>s.label))
 THROW 51831,'Existing pause catalog conflicts; review without overwriting historical meaning.',1;
INSERT dbo.reference_values(group_code,value_code,display_name,sort_order,is_active)
 SELECT 'TASK_PAUSE_REASON',s.code,s.label,s.sort_order,1 FROM @reasons s
 WHERE NOT EXISTS(SELECT 1 FROM dbo.reference_values r WITH(UPDLOCK,HOLDLOCK) WHERE r.group_code='TASK_PAUSE_REASON' AND r.value_code=s.code);
-- Existing deactivations are preserved on rerun; no forced reactivation.
IF OBJECT_ID('dbo.task_pause_events','U') IS NULL
 CREATE TABLE dbo.task_pause_events(
 progress_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.operational_task_progress(progress_id),
 reason_id bigint NOT NULL REFERENCES dbo.reference_values(reference_id),
 reason_code varchar(60) NOT NULL,
 reason_label nvarchar(150) NOT NULL,
 note nvarchar(1000) NULL,
 interval_id bigint NOT NULL UNIQUE REFERENCES dbo.work_intervals(interval_id),
 CONSTRAINT CK_task_pause_code CHECK(reason_code IN('WAITING_MATERIAL','WAITING_EQUIPMENT','WAITING_APPROVAL','ACCESS_BLOCKED','SAFETY_ISSUE','OTHER')),
 CONSTRAINT CK_task_pause_note CHECK(reason_code<>'OTHER' OR (note IS NOT NULL AND LEN(LTRIM(RTRIM(note)))>0)));
IF ISNULL(COL_LENGTH('dbo.task_pause_events','note'),-1)<>2000 OR ISNULL(COL_LENGTH('dbo.task_pause_events','reason_code'),-1)<>60
 OR ISNULL(COL_LENGTH('dbo.task_pause_events','reason_label'),-1)<>300
 THROW 51831,'Existing pause event schema incompatible.',1;
COMMIT;
SELECT DB_NAME() AS databaseName,(SELECT COUNT(*) FROM dbo.reference_values WHERE group_code='TASK_PAUSE_REASON') AS reasonCount,
 (SELECT COUNT(*) FROM dbo.task_pause_events) AS pauseEvents FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
