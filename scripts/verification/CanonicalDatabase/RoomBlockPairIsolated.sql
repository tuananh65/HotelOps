SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51833,'Exact owned disposable database required.',1;
IF OBJECT_ID('dbo.room_blocks','U') IS NULL OR ISNULL(COL_LENGTH('dbo.room_blocks','incident_id'),-1)<>8
 THROW 51833,'Room block schema required.',1;
BEGIN TRANSACTION;
IF EXISTS(SELECT room_id,incident_id FROM dbo.room_blocks WITH(UPDLOCK,HOLDLOCK) WHERE released_at IS NULL
 GROUP BY room_id,incident_id HAVING COUNT(*)>1)
 THROW 51833,'Existing duplicate incident-room blocks require review.',1;
IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.room_blocks') AND name='UX_room_blocks_active')
BEGIN
 IF NOT EXISTS(SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID('dbo.room_blocks') AND i.name='UX_room_blocks_active'
  AND i.is_unique=1 AND i.filter_definition='([released_at] IS NULL)'
  AND (SELECT COUNT(*) FROM sys.index_columns c WHERE c.object_id=i.object_id AND c.index_id=i.index_id AND c.key_ordinal>0)=1
  AND EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.columns sc ON sc.object_id=c.object_id AND sc.column_id=c.column_id
   WHERE c.object_id=i.object_id AND c.index_id=i.index_id AND c.key_ordinal=1 AND sc.name='room_id'))
  THROW 51833,'Unexpected prior index definition; do not replace.',1;
 DROP INDEX UX_room_blocks_active ON dbo.room_blocks;
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.room_blocks') AND name='UX_room_blocks_incident_room_active')
 CREATE UNIQUE INDEX UX_room_blocks_incident_room_active ON dbo.room_blocks(room_id,incident_id) WHERE released_at IS NULL;
IF NOT EXISTS(SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID('dbo.room_blocks') AND i.name='UX_room_blocks_incident_room_active'
 AND i.is_unique=1 AND i.is_disabled=0 AND i.filter_definition='([released_at] IS NULL)'
 AND (SELECT COUNT(*) FROM sys.index_columns c WHERE c.object_id=i.object_id AND c.index_id=i.index_id AND c.key_ordinal>0)=2
 AND EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.columns sc ON sc.object_id=c.object_id AND sc.column_id=c.column_id
  WHERE c.object_id=i.object_id AND c.index_id=i.index_id AND c.key_ordinal=1 AND sc.name='room_id')
 AND EXISTS(SELECT 1 FROM sys.index_columns c JOIN sys.columns sc ON sc.object_id=c.object_id AND sc.column_id=c.column_id
  WHERE c.object_id=i.object_id AND c.index_id=i.index_id AND c.key_ordinal=2 AND sc.name='incident_id'))
 THROW 51833,'Incident-room index incompatible.',1;
COMMIT;
SELECT name,is_unique,filter_definition FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.room_blocks') AND name='UX_room_blocks_incident_room_active' FOR JSON PATH;
