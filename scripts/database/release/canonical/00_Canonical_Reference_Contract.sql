-- SRS GBR-085, explicitly confirmed by DEV1 during Task 15.
-- Canonical trial databases only. No change to SEVERITY semantics.
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'HotelOps[_]Canonical[_]%'
 THROW 51920,'Canonical reference contract requires an isolated target.',1;
IF EXISTS(SELECT 1 FROM dbo.reference_values WHERE group_code='PRIORITY' AND value_code NOT IN('LOW','NORMAL','HIGH','CRITICAL'))
 THROW 51921,'Noncanonical priority exists. Review references before conversion; no automatic rewrite.',1;
IF OBJECT_ID('dbo.CK_canonical_priority_codes','C') IS NULL
 ALTER TABLE dbo.reference_values WITH CHECK ADD CONSTRAINT CK_canonical_priority_codes
 CHECK(group_code<>'PRIORITY' OR value_code IN('LOW','NORMAL','HIGH','CRITICAL'));
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE object_id=OBJECT_ID('dbo.CK_canonical_priority_codes') AND (is_disabled=1 OR is_not_trusted=1))
 THROW 51922,'Canonical priority constraint must be enabled and trusted.',1;
