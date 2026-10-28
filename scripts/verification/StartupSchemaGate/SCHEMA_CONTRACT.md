# Required schema contract

Source: User.ProfileConcurrency.cs, HotelOpsDbContext User mapping and UserProfileUpdate/rowversion-forward.sql. The real table is dbo.users; column row_version is SQL rowversion (timestamp synonym), system_type_id 189, length 8, NOT NULL, not computed. EF maps byte[] RowVersion with IsRowVersion(). A binary(8) value is not a rowversion and must be rejected.

Validator reads sys.columns joined to sys.tables and sys.schemas on the configured SQL connection. Only an exact count of one matching column passes. Missing table/column, wrong schema/type/length/nullability/computed property and hidden metadata do not pass. An extended-property trial receipt is not required by the application: receipts protect test DDL, not the runtime SQL contract. EF metadata alone is never proof that SQL has the column.

This is the incremental startup prerequisite introduced by Task 26, not certification of all 82 tables, indexes, audit structure, permissions or business rules. Profile also depends on users uniqueness, audit_logs, validated SQL sessions, RBAC and memberships; canonical schema/trial regressions verify those dependencies separately. No auto-migration/seed/repair is performed.

Metadata visibility is available through object SELECT on dbo.users. Gate execution needs no ALTER, CREATE DATABASE, db_owner, sysadmin or VIEW SERVER STATE. Without metadata visibility it returns MissingRequiredSchema; it cannot reliably distinguish hidden from absent objects and does not guess. Query/permission errors fail closed. Whole-application runtime DML privileges remain a separate deployment responsibility.
