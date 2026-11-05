/* Trial-only candidate. Default is rollback. Commit is prohibited while live dependencies remain.
   Only department.is_active is in scope. No reassignment, deletion, status fabrication or seed changes.
   The current trial has unresolved live dependencies, so COMMIT must not be selected yet.
*/
USE [HotelOps_RBAC_Upgrade_Trial_20261021_380b6cc4b282];
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
DECLARE @Commit bit=0;
IF DB_NAME()<>N'HotelOps_RBAC_Upgrade_Trial_20261021_380b6cc4b282'
   OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'DESKTOP-JI8U50T\SQLEXPRESS'
    THROW 52000,'Wrong trial server/database. No changes allowed.',1;
IF @@TRANCOUNT<>0 THROW 52001,'Existing transaction is not allowed.',1;
BEGIN TRY
    BEGIN TRANSACTION;
    -- Lock the exact expected department set and prevent concurrent additions to it.
    IF (SELECT COUNT(*) FROM dbo.departments WITH (UPDLOCK,HOLDLOCK)
        WHERE (branch_id=1 AND department_code IN ('FB','LAU','SEC'))
           OR (branch_id=2 AND department_code='SEC'))<>4
        THROW 52002,'Expected four legacy departments not found.',1;
    IF (SELECT COUNT(*) FROM dbo.departments WITH (UPDLOCK,HOLDLOCK)
        WHERE branch_id IN (1,2) AND department_code IN ('HK','MT','WH','FO') AND is_active=1)<>8
        THROW 52003,'Canonical departments are not all active.',1;
    IF EXISTS (SELECT 1 FROM dbo.departments WHERE is_active=1 AND department_code NOT IN ('HK','MT','WH','FO','FB','LAU','SEC'))
        THROW 52004,'Unexpected active department requires review.',1;

    DECLARE @Blockers TABLE(issue varchar(80),record_count bigint);
    INSERT INTO @Blockers
    SELECT 'ACTIVE_USERS',COUNT_BIG(*) FROM dbo.users u JOIN dbo.departments d ON d.department_id=u.department_id
      WHERE d.department_code IN ('FB','LAU','SEC') AND u.account_status='ACTIVE' AND u.deleted_at IS NULL
    UNION ALL
    SELECT 'ACTIVE_WAREHOUSES',COUNT_BIG(*) FROM dbo.warehouses w JOIN dbo.departments d ON d.department_id=w.department_id
      WHERE d.department_code IN ('FB','LAU','SEC') AND w.is_active=1
    UNION ALL
    SELECT 'OPEN_INTERNAL_REQUESTS',COUNT_BIG(*) FROM dbo.internal_requests r
      WHERE r.status NOT IN ('COMPLETED','CLOSED','CANCELLED') AND EXISTS
        (SELECT 1 FROM dbo.departments d WHERE d.department_code IN ('FB','LAU','SEC')
          AND d.department_id IN (r.requester_department_id,r.receiving_department_id))
    UNION ALL
    SELECT 'OPEN_OPERATIONAL_TASKS',COUNT_BIG(*) FROM dbo.operational_tasks t JOIN dbo.departments d ON d.department_id=t.department_id
      WHERE d.department_code IN ('FB','LAU','SEC') AND t.status NOT IN ('COMPLETED','CANCELLED')
    UNION ALL
    SELECT 'ACTIVE_OR_PLANNED_SHIFTS',COUNT_BIG(*) FROM dbo.work_shifts s JOIN dbo.departments d ON d.department_id=s.department_id
      WHERE d.department_code IN ('FB','LAU','SEC') AND s.status IN ('IN_PROGRESS','PLANNED')
    UNION ALL
    SELECT 'ACTIVE_REQUEST_TYPES',COUNT_BIG(*) FROM dbo.internal_request_types t JOIN dbo.departments d ON d.department_id=t.default_department_id
      WHERE d.department_code IN ('FB','LAU','SEC') AND t.is_active=1
    UNION ALL
    SELECT 'ACTIVE_WORK_CATEGORIES',COUNT_BIG(*) FROM dbo.work_categories c JOIN dbo.departments d ON d.department_id=c.department_id
      WHERE d.department_code IN ('FB','LAU','SEC') AND c.is_active=1;
    SELECT issue,record_count FROM @Blockers ORDER BY issue;
    -- The commit path fails before UPDATE. This is a conservative trial gate, not a replacement
    -- for a complete production preflight (inventory, assets, future statuses and concurrency).
    IF @Commit=1 AND EXISTS (SELECT 1 FROM @Blockers WHERE record_count>0)
        THROW 52005,'Unresolved live dependencies: no approved reassignment/retirement rules. COMMIT blocked.',1;

    UPDATE dbo.departments SET is_active=0
      WHERE is_active=1 AND ((branch_id=1 AND department_code IN ('FB','LAU','SEC'))
        OR (branch_id=2 AND department_code='SEC'));
    SELECT @@ROWCOUNT AS candidate_departments_changed;
    IF EXISTS (SELECT 1 FROM dbo.departments WHERE is_active=1 AND department_code NOT IN ('HK','MT','WH','FO'))
        THROW 52006,'Active noncanonical department remains.',1;
    SELECT branch_id,department_code,is_active FROM dbo.departments ORDER BY branch_id,department_code;
    IF @Commit=1
        COMMIT TRANSACTION;
    ELSE
        ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
