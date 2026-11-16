-- Additive incident provenance. ApplyChanges=0 rolls back install; retain links after use.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%'
 OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000')
 THROW 51846,'Verified development identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51846,'ApplyChanges must be 0 or 1.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:PreventiveIncident:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51850,'Occurrence incident schema lock unavailable.',1;
IF OBJECT_ID('dbo.preventive_occurrence_details','U') IS NULL THROW 51850,'Occurrence schema required.',1;
IF OBJECT_ID('dbo.preventive_occurrence_incidents','U') IS NULL
CREATE TABLE dbo.preventive_occurrence_incidents(
 incident_id bigint NOT NULL PRIMARY KEY REFERENCES dbo.incidents(incident_id),
 run_id bigint NOT NULL REFERENCES dbo.preventive_occurrence_details(run_id),
 check_key nvarchar(64) NOT NULL CHECK(LEN(LTRIM(RTRIM(check_key)))>0),
 actor_id bigint NOT NULL REFERENCES dbo.users(user_id),
 audit_id bigint NOT NULL UNIQUE REFERENCES dbo.audit_logs(audit_id),
 linked_at datetime2(7) NOT NULL
);
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.preventive_occurrence_incidents')
 AND name IN('incident_id','run_id','check_key','actor_id','audit_id','linked_at'))<>6 THROW 51850,'Link schema mismatch.',1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.preventive_occurrence_incidents')
 AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0 AND update_referential_action=0)<>4 THROW 51850,'Link integrity required.',1;
EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_pm_incident_link_immutable ON dbo.preventive_occurrence_incidents AFTER UPDATE,DELETE AS
 BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted) THROW 51850,''Occurrence incident links are immutable.'',1; END');
IF N'$(ApplyChanges)'=N'1' COMMIT TRANSACTION; ELSE ROLLBACK TRANSACTION;