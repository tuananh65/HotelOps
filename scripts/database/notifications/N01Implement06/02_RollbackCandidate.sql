-- REVIEW-ONLY, NOT EXECUTED. Never deletes publications/receipts to make rollback succeed.
SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'KIENPC'
 OR DB_NAME()<>N'HotelOps_DEV2_FullStack_Trial'
 OR CONVERT(nvarchar(128),SESSION_CONTEXT(N'HotelOps.N01.ApprovedTrial'))<>N'N01-IMPLEMENT06-APPROVED-ISOLATED-SCHEMA'
 OR SESSION_CONTEXT(N'HotelOps.N01.ApprovedTrial') IS NULL OR @@TRANCOUNT<>0
 THROW 51960,'Dedicated owner-approved isolated schema session required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.N01.SingleGraphCandidate'
 AND CONVERT(nvarchar(128),value)=N'IMPLEMENT06-FS016-V1') THROW 51960,'Exact candidate required.',1;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM dbo.notification_publications WITH(UPDLOCK,HOLDLOCK)
 WHERE event_type IN('inspection.failed','cleaning.rework.requested') OR inspection_id IS NOT NULL OR n01_payload_hash IS NOT NULL)
 OR EXISTS(SELECT 1 FROM dbo.notification_operation_reservations WITH(UPDLOCK,HOLDLOCK)
 WHERE n01_operation_hash IS NOT NULL OR n01_receipt_json IS NOT NULL)
 THROW 51960,'N01 records exist: preserve data, stop and request owner reconciliation; do not delete history.',1;
ALTER TABLE dbo.notification_publications DROP CONSTRAINT FK_n01_publication_inspection,CK_n01_publication_event,CK_n01_publication_source;
ALTER TABLE dbo.notification_publications DROP COLUMN inspection_id,n01_payload_hash;
ALTER TABLE dbo.notification_operation_reservations DROP CONSTRAINT CK_n01_operation_metadata;
ALTER TABLE dbo.notification_operation_reservations DROP COLUMN n01_operation_hash,n01_receipt_json;
ALTER TABLE dbo.notification_publications WITH CHECK ADD
 CONSTRAINT CK_fs016_publication_event CHECK(event_type IN('CLEANING_TASK_ASSIGNED','CLEANING_BLOCKER_REPORTED','CLEANING_TASK_COMPLETED','REWORK_SUBMITTED')),
 CONSTRAINT CK_fs016_publication_source CHECK(
   (event_type='REWORK_SUBMITTED' AND rework_id IS NOT NULL AND assignment_id IS NULL) OR
   (event_type<>'REWORK_SUBMITTED' AND rework_id IS NULL AND assignment_id IS NOT NULL));
EXEC sys.sp_dropextendedproperty @name=N'HotelOps.N01.SingleGraphCandidate';
COMMIT;
