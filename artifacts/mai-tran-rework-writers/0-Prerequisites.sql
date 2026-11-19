-- Approved rework workflow package. Execute only through the pinned transactional installer.
SET XACT_ABORT ON;
IF @@TRANCOUNT<>1 OR CONVERT(nvarchar(128),SESSION_CONTEXT(N'ReworkWriter.Database')) IS NULL
 OR DB_NAME()<>CONVERT(nvarchar(128),SESSION_CONTEXT(N'ReworkWriter.Database'))
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'ReworkWriter.DatabaseGuid')))
 THROW 51999,'Pinned database transaction required',1;
IF DATABASE_PRINCIPAL_ID('hotelops_rework_pipeline') IS NOT NULL OR DATABASE_PRINCIPAL_ID('hotelops_rework_owner') IS NOT NULL
 THROW 51999,'No overwrite of rework principals',1;
IF OBJECT_ID('dbo.fe04_rework_authority_issuers','U') IS NULL THROW 51999,'Initial writer registry required',1;
CREATE ROLE hotelops_rework_pipeline;
CREATE USER hotelops_rework_owner WITHOUT LOGIN;
DECLARE @old nvarchar(max)=(SELECT definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.cleaning_reworks') AND name='CK_rework_workflow_v1' AND is_disabled=0 AND is_not_trusted=0);
IF @old IS NULL OR @old LIKE '%ACCEPTED%' OR EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.ReworkExecution.V1') THROW 51999,'Expected original rework execution baseline',1;
EXEC(N'ALTER TABLE dbo.cleaning_reworks DROP CONSTRAINT CK_rework_workflow_v1; ALTER TABLE dbo.cleaning_reworks WITH CHECK ADD CONSTRAINT CK_rework_workflow_v1 CHECK ('+@old+N' OR (workflow_status=''ACCEPTED'' AND status=''PENDING''));');
EXEC sys.sp_addextendedproperty @name=N'HotelOps.ReworkExecution.V1',@value=N'ACCEPT_START_V1';
