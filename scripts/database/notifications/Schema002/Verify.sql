-- Read-only post-install contract. Preflight.sql must pass in this session first.
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.N01.SingleGraphCandidate'
 AND CONVERT(nvarchar(128),value)=N'IMPLEMENT06-FS016-V1')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE04.NotificationReconciliation'
 AND CONVERT(nvarchar(128),value)=N'FE04-P0-FS013-N01-CANDIDATE-V1')
 THROW 51962,'Reconciliation and N01 markers missing.',1;
DECLARE @filter nvarchar(max)=(SELECT filter_definition FROM sys.indexes
 WHERE object_id=OBJECT_ID('dbo.notification_publications') AND name='UX_fs009_publication_operation');
SET @filter=REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(@filter,'[',''),']',''),'(',''),')',''),' ','');
IF ISNULL(@filter,'')<>'event_type=''CLEANING_TASK_ASSIGNED'''
 THROW 51962,'Assignment operation uniqueness must be filtered only to assignment events.',1;
IF (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.notification_publications')
 AND name IN('CK_n01_publication_event','CK_n01_publication_source') AND is_disabled=0 AND is_not_trusted=0)<>2
 OR NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.notification_operation_reservations')
 AND name='CK_n01_operation_metadata' AND is_disabled=0 AND is_not_trusted=0)
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.notification_publications')
 AND name='FK_n01_publication_inspection' AND is_disabled=0 AND is_not_trusted=0)
 OR ISNULL(COL_LENGTH('dbo.notification_publications','inspection_id'),-1)<>8
 OR ISNULL(COL_LENGTH('dbo.notification_publications','n01_payload_hash'),-1)<>32
 OR ISNULL(COL_LENGTH('dbo.notification_operation_reservations','n01_operation_hash'),-1)<>32
 OR ISNULL(COL_LENGTH('dbo.notification_operation_reservations','n01_receipt_json'),0)<>-1
 THROW 51962,'N01 additive metadata/typed arms missing.',1;
IF EXISTS(SELECT actor_user_id,operation_key FROM dbo.notification_publications
 WHERE event_type='CLEANING_TASK_ASSIGNED' GROUP BY actor_user_id,operation_key HAVING COUNT(*)>1)
 OR EXISTS(SELECT 1 FROM dbo.notification_publications p LEFT JOIN dbo.notification_operation_reservations o
 ON o.actor_user_id=p.actor_user_id AND o.operation_key=p.operation_key WHERE o.operation_key IS NULL)
 OR EXISTS(SELECT 1 FROM dbo.notification_delivery_intents i LEFT JOIN dbo.notification_publications p
 ON p.publication_id=i.publication_id WHERE p.publication_id IS NULL)
 THROW 51962,'Single-graph identity/lineage violation.',1;
-- Live trigger behavior and write/rollback/concurrency are separate acceptance cases, not proven by catalog queries.

