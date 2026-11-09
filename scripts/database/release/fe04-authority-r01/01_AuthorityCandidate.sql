-- Candidate only. A separately approved disposable copy must contain the FS016 substrate.
-- No issuer seed, writer procedure, runtime activation or authoritative fixture is created.
SET XACT_ABORT ON;
IF DB_NAME() IN(N'master',N'model',N'msdb',N'tempdb')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
   AND name=N'HotelOps.FE04.UnblockR01.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_DISPOSABLE')
 THROW 51994,'Explicitly approved FE04 disposable target marker required',1;
IF OBJECT_ID(N'dbo.fs016_rework_source_proofs',N'U') IS NULL
 OR OBJECT_ID(N'dbo.notification_operation_reservations',N'U') IS NULL
 OR COL_LENGTH(N'dbo.fs016_rework_source_proofs',N'contract_version') IS NOT NULL
 THROW 51994,'Existing FS016 substrate and fresh candidate required; no overwrite',1;
BEGIN TRANSACTION;
CREATE TABLE dbo.fe04_rework_authority_issuers(
 issuer varchar(80) COLLATE Latin1_General_100_BIN2 NOT NULL,
 version int NOT NULL CHECK(version=1),writer_object_id int NOT NULL CHECK(writer_object_id>0),
 module_digest binary(32) NOT NULL,source_commit varchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL,
 is_active bit NOT NULL DEFAULT(0),PRIMARY KEY(issuer,version),
 CHECK(LEN(source_commit)=40 AND source_commit NOT LIKE '%[^0-9a-f]%'));
ALTER TABLE dbo.fs016_rework_source_proofs ADD
 contract_version int NULL,issuer varchar(80) COLLATE Latin1_General_100_BIN2 NULL,issuer_version int NULL,
 actor_user_id bigint NULL,operation_key uniqueidentifier NULL,writer_transaction_id bigint NULL,
 cleaning_task_id bigint NULL,room_id bigint NULL,branch_id bigint NULL,version_id bigint NULL,evidence_digest binary(32) NULL;
DECLARE @originCheck sysname=(SELECT name FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.fs016_rework_source_proofs')
 AND definition LIKE '%origin%' AND definition LIKE '%SYNTHETIC_TEST_ONLY%');
IF @originCheck IS NULL THROW 51994,'Original synthetic origin constraint required',1;
DECLARE @dropOriginSql nvarchar(max)=N'ALTER TABLE dbo.fs016_rework_source_proofs DROP CONSTRAINT '+QUOTENAME(@originCheck);
EXEC(@dropOriginSql);
-- Strict discriminated replacement, not removal of the original origin protection.
EXEC(N'ALTER TABLE dbo.fs016_rework_source_proofs ADD CONSTRAINT CK_fe04_authority_origin CHECK(
 (origin=''SYNTHETIC_TEST_ONLY'' AND contract_version IS NULL AND issuer IS NULL AND issuer_version IS NULL
  AND actor_user_id IS NULL AND operation_key IS NULL AND writer_transaction_id IS NULL
  AND cleaning_task_id IS NULL AND room_id IS NULL AND branch_id IS NULL AND version_id IS NULL AND evidence_digest IS NULL)
 OR (origin=''FE05_COMMITTED_V1'' AND contract_version IS NOT NULL AND contract_version=1
  AND issuer IS NOT NULL AND issuer=''FE05.UC045.UC046'' AND issuer_version IS NOT NULL AND issuer_version=1
  AND actor_user_id IS NOT NULL AND actor_user_id>0 AND operation_key IS NOT NULL
  AND operation_key<>''00000000-0000-0000-0000-000000000000''
  AND writer_transaction_id IS NOT NULL AND writer_transaction_id>0
  AND cleaning_task_id IS NOT NULL AND cleaning_task_id>0 AND room_id IS NOT NULL AND room_id>0
  AND branch_id IS NOT NULL AND branch_id>0 AND version_id IS NOT NULL AND version_id>0 AND evidence_digest IS NOT NULL));
 ALTER TABLE dbo.fs016_rework_source_proofs ADD CONSTRAINT FK_fe04_authority_issuer
  FOREIGN KEY(issuer,issuer_version) REFERENCES dbo.fe04_rework_authority_issuers(issuer,version);
 ALTER TABLE dbo.fs016_rework_source_proofs ADD CONSTRAINT FK_fe04_authority_operation
  FOREIGN KEY(actor_user_id,operation_key) REFERENCES dbo.notification_operation_reservations(actor_user_id,operation_key);
 CREATE UNIQUE INDEX UX_fe04_authority_operation ON dbo.fs016_rework_source_proofs(actor_user_id,operation_key)
  WHERE origin=''FE05_COMMITTED_V1'';');
EXEC(N'CREATE TRIGGER dbo.TR_fe04_authority_immutable ON dbo.fs016_rework_source_proofs AFTER INSERT,UPDATE,DELETE AS
 BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted WHERE origin=''FE05_COMMITTED_V1'')
   THROW 51994,''Committed source proof is immutable; changed sources require a new authoritative occurrence'',1;
 IF EXISTS(SELECT 1 FROM inserted WHERE origin=''FE05_COMMITTED_V1'' AND writer_transaction_id<>CURRENT_TRANSACTION_ID())
   THROW 51994,''Proof must bind its actual writer transaction'',1;
 END');
-- Only separately reviewed FE05 SQL modules WITH EXECUTE AS OWNER may issue real proofs.
-- The ordinary API identity must not own these objects or be db_owner/sysadmin.
DENY INSERT,UPDATE,DELETE ON dbo.fs016_rework_source_proofs TO public;
DENY INSERT,UPDATE,DELETE ON dbo.fe04_rework_authority_issuers TO public;
EXEC sys.sp_addextendedproperty @name=N'HotelOps.FE04.ReworkAuthority.V1',@value=N'FE05_COMMITTED_V1';
COMMIT;
