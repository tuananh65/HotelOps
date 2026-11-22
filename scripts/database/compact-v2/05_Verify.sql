/* Read-only catalog/integrity validation. Does not seed or mutate business rows. */
SET NOCOUNT ON;
IF DB_NAME() NOT LIKE N'HotelOpsCompact%' THROW 51000,'Compact database required.',1;
DECLARE @expected nvarchar(max)=N'["branches","departments","locations","room_types","rooms","users","memberships","roles","permissions","role_permissions","user_roles","user_sessions","reference_values","assets","checklist_templates","checklist_items","work_templates","work_items","work_links","work_assignments","work_events","work_checklist_items","inspection_findings","room_blocks","room_events","warehouses","inventory_items","inventory_policies","inventory_documents","inventory_document_lines","stock_movements","document_events","work_intervals","asset_events","attachments","audit_logs","notifications"]';
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>37
 THROW 51100,'Expected exactly 37 user tables.',1;
IF EXISTS(SELECT value FROM OPENJSON(@expected) EXCEPT SELECT name FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo') AND is_ms_shipped=0)
 THROW 51101,'Table manifest mismatch.',1;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOpsCompactSchema' AND CONVERT(varchar(20),value)='2.0.0')
 THROW 51102,'Missing compact schema version.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1 OR delete_referential_action<>0)
 THROW 51103,'Foreign key disabled, untrusted or cascading.',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
 THROW 51104,'Check constraint disabled or untrusted.',1;
IF EXISTS(SELECT 1 FROM sys.triggers WHERE parent_class=1 AND is_disabled=1)
 THROW 51105,'Integrity trigger disabled.',1;
IF (SELECT COUNT(*) FROM sys.foreign_keys)<>152 OR (SELECT COUNT(*) FROM sys.check_constraints)<>77
 THROW 51106,'Constraint inventory differs from v2.',1;
IF OBJECT_ID(N'dbo.workdays') IS NOT NULL OR OBJECT_ID(N'dbo.shifts') IS NOT NULL OR OBJECT_ID(N'dbo.time_corrections') IS NOT NULL THROW 51107,'Workday tables must be absent.',1;
IF COL_LENGTH(N'dbo.work_intervals',N'workday_id') IS NOT NULL THROW 51108,'Work time cannot depend on workdays.',1;
DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;
DBCC CHECKDB WITH NO_INFOMSGS;
SELECT DB_NAME() AS database_name,
 (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) AS table_count,
 (SELECT COUNT(*) FROM sys.foreign_keys) AS foreign_key_count,
 (SELECT COUNT(*) FROM sys.check_constraints) AS check_count,
 (SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1) AS trigger_count,
 (SELECT COUNT(*) FROM sys.views WHERE is_ms_shipped=0) AS view_count;
