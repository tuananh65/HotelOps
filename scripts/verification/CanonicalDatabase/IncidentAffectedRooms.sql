-- UC056 approved explicit Incident affected-room mapping. Additive, disposable identity guarded.
SET XACT_ABORT ON;
IF N'$(ExpectedDatabase)' NOT LIKE N'HotelOps[_]DEV3[_]%' OR DB_NAME()<>N'$(ExpectedDatabase)'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$(ExpectedServer)'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
   AND database_guid=TRY_CONVERT(uniqueidentifier,N'$(ExpectedDatabaseGuid)')
   AND database_guid<>'00000000-0000-0000-0000-000000000000') THROW 51849,'Verified disposable identity required.',1;
IF N'$(ApplyChanges)' NOT IN(N'0',N'1') THROW 51849,'ApplyChanges must be 0 or 1.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'HotelOps:IncidentAffectedRooms:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @lock<0 THROW 51849,'Migration lock unavailable.',1;
IF OBJECT_ID('dbo.incident_affected_rooms','U') IS NULL
BEGIN
 CREATE TABLE dbo.incident_affected_rooms(
 incident_id bigint NOT NULL,room_id bigint NOT NULL,added_by bigint NOT NULL,added_at datetime2(7) NOT NULL,reason nvarchar(max) NOT NULL,
 CONSTRAINT PK_incident_affected_rooms PRIMARY KEY(incident_id,room_id),
 CONSTRAINT FK_incident_affected_rooms_incident FOREIGN KEY(incident_id) REFERENCES dbo.incidents(incident_id),
 CONSTRAINT FK_incident_affected_rooms_room FOREIGN KEY(room_id) REFERENCES dbo.rooms(room_id),
 CONSTRAINT FK_incident_affected_rooms_actor FOREIGN KEY(added_by) REFERENCES dbo.users(user_id));
END;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incident_affected_rooms') AND
 ((name IN('incident_id','room_id','added_by') AND system_type_id=127 AND is_nullable=0)
 OR (name='added_at' AND system_type_id=42 AND scale=7 AND is_nullable=0)
 OR (name='reason' AND system_type_id=231 AND max_length=-1 AND is_nullable=0)))<>5
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.incident_affected_rooms') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>3
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.incident_affected_rooms') AND name='PK_incident_affected_rooms' AND is_primary_key=1 AND is_disabled=0)
 THROW 51849,'Affected rooms schema mismatch.',1;
IF COL_LENGTH('dbo.room_status_history','effective_from') IS NULL
 ALTER TABLE dbo.room_status_history ADD effective_from varchar(40) NULL;
IF COL_LENGTH('dbo.room_status_history','effective_to') IS NULL
 ALTER TABLE dbo.room_status_history ADD effective_to varchar(40) NULL;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.room_status_history')
 AND name IN('effective_from','effective_to') AND system_type_id=167 AND max_length=40 AND is_nullable=1)<>2
 THROW 51849,'Effective readiness history schema mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IncidentAffectedRooms.V1')
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.IncidentAffectedRooms.V1',@value=N'1';
ELSE IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.IncidentAffectedRooms.V1' AND CONVERT(nvarchar(10),value)='1')
 THROW 51849,'Conflicting affected rooms capability.',1;
IF N'$(ApplyChanges)'=N'1' COMMIT; ELSE ROLLBACK;
