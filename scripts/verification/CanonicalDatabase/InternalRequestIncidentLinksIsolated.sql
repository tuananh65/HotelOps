SET XACT_ABORT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'meita'
 OR DB_NAME()<>N'HotelOps_DEV3_142A_20261114_cc78c13f'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)=N'cc78c13ffc124efcad67b3d804baa84c')
 THROW 51898,'Exact owned disposable database required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestDecision.V1' AND CONVERT(nvarchar(20),value)='1')
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.internal_requests') AND name='row_version' AND system_type_id=189 AND is_nullable=0)
 THROW 51898,'Verified request concurrency baseline required.',1;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestIncidentLinks.V1')
BEGIN
 IF OBJECT_ID('dbo.internal_request_incident_links') IS NOT NULL
  THROW 51898,'Unexpected pre-existing link schema; preserve and review.',1;
 -- Additive explicit relationship; existing related_incident_id provenance is preserved.
 -- Request row_version remains authoritative. No default infers mandatory/optional.
 -- Audit FK is restrictive: JOB06 must preserve referenced audit until approved archival.
 CREATE TABLE dbo.internal_request_incident_links(
  internal_request_id bigint NOT NULL,
  incident_id bigint NOT NULL,
  is_mandatory bit NOT NULL,
  audit_id bigint NOT NULL,
  CONSTRAINT PK_internal_request_incident_links PRIMARY KEY(internal_request_id,incident_id),
  CONSTRAINT FK_ir_incident_link_request FOREIGN KEY(internal_request_id) REFERENCES dbo.internal_requests(internal_request_id),
  CONSTRAINT FK_ir_incident_link_incident FOREIGN KEY(incident_id) REFERENCES dbo.incidents(incident_id),
  CONSTRAINT FK_ir_incident_link_audit FOREIGN KEY(audit_id) REFERENCES dbo.audit_logs(audit_id)
 );
 EXEC sys.sp_addextendedproperty @name=N'HotelOps.InternalRequestIncidentLinks.V1',@value=N'1';
END;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestIncidentLinks.V1' AND CONVERT(nvarchar(20),value)='1')
 OR ISNULL(COL_LENGTH('dbo.internal_request_incident_links','is_mandatory'),0)<>1
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.internal_request_incident_links')
   AND name IN('FK_ir_incident_link_request','FK_ir_incident_link_incident','FK_ir_incident_link_audit')
   AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>3
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_incident_links')
   AND name='PK_internal_request_incident_links' AND is_primary_key=1 AND is_disabled=0)
 THROW 51898,'Incident link schema incompatible.',1;
COMMIT;
