-- Parameterized exact-target/owner/lease checks; never creates ownership markers.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'DESKTOP-JI8U50T\SQLEXPRESS'
 OR DB_NAME()<>N'HotelOps_ENVNOTIF001_Trial_20261109' OR @@TRANCOUNT<>0
 OR ORIGINAL_LOGIN()<>N'DESKTOP-JI8U50T\hax'
 THROW 51962,'Exact notification disposable target and execution identity required.',1;
IF @LeaseEnd<=SYSUTCDATETIME() OR @LeaseStart>SYSUTCDATETIME() OR DATEDIFF(second,@LeaseStart,@LeaseEnd)<>86400
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.NotificationSchemaTrial'
 AND CONVERT(nvarchar(128),value)=CONVERT(nvarchar(36),@LeaseId))
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.NotificationSchemaLeaseEndUtc'
 AND CONVERT(nvarchar(40),value)=@LeaseEndText)
 THROW 51962,'Provisioned owner lease missing, expired or mismatched.',1;
IF OBJECT_ID('dbo.notification_intents_c04','U') IS NOT NULL
 THROW 51962,'Second notification graph is forbidden for this trial.',1;
IF (SELECT COUNT(*) FROM sys.triggers WHERE parent_id=OBJECT_ID('dbo.notification_publications')
 AND name IN('TR_fs009_publication_source','TR_fs009_publication_immutable') AND is_disabled=0 AND is_instead_of_trigger=0)<>2
 OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID('dbo.notification_operation_reservations')
 AND name='TR_fs009_operation_immutable' AND is_disabled=0)
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id IN(OBJECT_ID('dbo.notification_publications'),
 OBJECT_ID('dbo.notification_operation_reservations'),OBJECT_ID('dbo.notification_delivery_intents')) AND (is_disabled=1 OR is_not_trusted=1))
 OR EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID('dbo.notification_publications'),
 OBJECT_ID('dbo.notification_operation_reservations'),OBJECT_ID('dbo.notification_delivery_intents')) AND (is_disabled=1 OR is_not_trusted=1))
 THROW 51962,'Enabled source/immutability triggers and trusted constraints required.',1;
DECLARE @trigger int=OBJECT_ID('dbo.TR_fs009_publication_source');
IF NOT EXISTS(SELECT 1 FROM sys.sql_modules WHERE object_id=@trigger AND definition IS NOT NULL AND uses_ansi_nulls=1 AND uses_quoted_identifier=1)
 OR EXISTS(SELECT 1 FROM sys.database_permissions WHERE class=1 AND major_id=@trigger)
 OR EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=1 AND major_id=@trigger)
 OR EXISTS(SELECT 1 FROM sys.crypt_properties WHERE major_id=@trigger)
 THROW 51962,'Prior trigger needs readable text, standard SET options, no signatures or extra object metadata.',1;
DECLARE @expected TABLE(table_name sysname,index_name sysname,keys nvarchar(200));
INSERT @expected VALUES
 ('notification_operation_reservations','PK_notification_operation_v1','actor_user_id,operation_key'),
 ('notification_publications','UX_fs009_publication_operation','actor_user_id,operation_key'),
 ('notification_publications','UX_fs015_occurrence','occurrence_key'),
 ('notification_publications','UX_fs015_assignment_publication','assignment_id'),
 ('notification_delivery_intents','UQ_delivery_publication_recipient_v1','publication_id,user_id,delivery_channel');
IF EXISTS(SELECT 1 FROM @expected e WHERE NOT EXISTS(
 SELECT 1 FROM sys.indexes ix WHERE ix.object_id=OBJECT_ID(N'dbo.'+e.table_name) AND ix.name=e.index_name
 AND ix.is_unique=1 AND ix.is_disabled=0 AND ix.is_hypothetical=0
 AND e.keys=(SELECT STRING_AGG(CONVERT(nvarchar(max),c.name),N',') WITHIN GROUP(ORDER BY ic.key_ordinal)
 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
 WHERE ic.object_id=ix.object_id AND ic.index_id=ix.index_id AND ic.key_ordinal>0)
 AND NOT EXISTS(SELECT 1 FROM sys.index_columns ic WHERE ic.object_id=ix.object_id AND ic.index_id=ix.index_id AND (ic.is_included_column=1 OR ic.is_descending_key=1))))
 THROW 51962,'Exact single-graph index keys required.',1;
DECLARE @occurrenceFilter nvarchar(max)=(SELECT filter_definition FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_publications') AND name='UX_fs015_occurrence');
DECLARE @assignmentFilter nvarchar(max)=(SELECT filter_definition FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_publications') AND name='UX_fs015_assignment_publication');
SET @occurrenceFilter=REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@occurrenceFilter,'[',''),']',''),'(',''),')',''),' ','');
SET @assignmentFilter=REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@assignmentFilter,'[',''),']',''),'(',''),')',''),' ','');
IF ISNULL(@occurrenceFilter,'')<>'occurrence_keyISNOTNULL' OR ISNULL(@assignmentFilter,'')<>'event_type=''CLEANING_TASK_ASSIGNED'''
 THROW 51962,'Exact occurrence and assignment source filters required.',1;
IF ISNULL(COL_LENGTH('dbo.cleaning_reworks','row_version'),-1)<>8 OR COL_LENGTH('dbo.cleaning_reworks','workflow_status') IS NULL
 OR COL_LENGTH('dbo.cleaning_tasks','due_at') IS NULL OR ISNULL(COL_LENGTH('dbo.notification_publications','occurrence_key'),-1)<>120
 THROW 51962,'Existing FS013/FS015/FS016 source columns required.',1;
SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS server_name,DB_NAME() AS database_name,
 (SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()) AS database_guid;

