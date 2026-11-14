/* BLOCKED / REVIEW ONLY / NOT EXECUTABLE PROVISIONING.
   No accepted login authentication mechanism, mapped runtime user, central lease token,
   or full 15-case restricted module matrix is available. Do not uncomment guessed grants.
   DEV1 ENV-001 must obtain owner decisions and submit a new reviewed script.
   No password, secret or connection string belongs in this artifact.

   A. Preflight: exact target, actual database_guid (NOT service_broker_guid), owner,
      central exclusive runner lease/token and current hard expiry, catalog provenance.
   B. Login/user: HotelOps_DEV2_FullStack_Runtime is PROPOSED, not accepted.
      fs013_app must remain WITHOUT LOGIN. Never remap it or impersonate it as a shortcut.
   C. Roles: none approved. No sysadmin/db_owner/db_datawriter/ALTER/CONTROL/securityadmin.
   D. Module candidates from existing source, NOT new approved runtime grants:
      dbo.fs013_assign_with_evidence; dbo.fs009_verify_assignment_guard.
      Full 15-case module/context and delivery/inbox permissions remain owner decisions.
      Never allow dbo.fs013_unapproved_writer_probe.
   E. Existing static dbo ownership chain may permit writes inside those modules;
      it does not authorize arbitrary direct graph DML or a new login.
      No EXECUTE AS OWNER, signing or impersonation is newly approved.
   F. Exactly one caller-owned Serializable transaction, source/session/domain receipt
      verified in service; module must not commit. Idempotency/result_json remain DEV1-owned.
   G. Keep DENY INSERT/UPDATE/DELETE on reservations/publications, DENY INSERT/DELETE
      on intents, and existing narrow delivery updates. Do not broaden any permission.
   H. Rerun is always refusal. This file cannot partially provision an identity.
   I. Credential exchange must use owner-controlled secret storage outside Git/ZIP.
*/
SET NOCOUNT ON;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'DESKTOP-JI8U50T\SQLEXPRESS'
 OR DB_NAME()<>N'HotelOps_ENVNOTIF001_Trial_20261109'
 THROW 51980,'Exact ENV001 server/database required',1;
IF NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
 AND database_guid=CONVERT(uniqueidentifier,'a138fa48-cc14-4070-9ca4-793f1b60c5c1'))
 THROW 51981,'Exact actual database_guid required',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
 AND name=N'HotelOps.NotificationSchemaOwner' AND CONVERT(nvarchar(128),value)=N'DEV1 ENV-001 / DEV2')
 THROW 51982,'Exact owner marker required',1;
IF SYSUTCDATETIME()>=CONVERT(datetime2(7),'2026-11-10T11:44:09.1792768',126)
 THROW 51983,'Original hard lease expired; no extension authorized',1;
-- Deliberate unconditional refusal BEFORE any security action, even under provisioning admin.
THROW 51984,'BLOCKED: accepted auth/mapping/module matrix and central lease proof missing; no principal or permission changes authorized',1;
-- Verification query specifications for a separately reviewed owner implementation:
-- sys.server_principals/sys.database_principals SID/type/authentication_type_desc;
-- sys.database_permissions and role memberships; effective HAS_PERMS_BY_NAME checks;
-- sys.sql_modules.execute_as_principal_id, dbo module/schema ownership and signatures;
-- negative direct-DML and unapproved-module tests using actual restricted connection.
-- No live checks or DML were executed by DEV2 in WRITER004A.
