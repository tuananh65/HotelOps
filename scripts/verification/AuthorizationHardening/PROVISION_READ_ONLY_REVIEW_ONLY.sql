/* REVIEW ONLY. NOT EXECUTED BY TASK08 OR ANY RUNNER.
   DBA supplies an existing, dedicated, non-administrative server principal.
   Do not reuse the development application principal or grant a server role.
   No password/login creation in this script. Replace the quoted placeholder
   after review; run only against the intended server and HotelOpsDB.
   SELECT is limited to verification tables; users is column-limited so this
   principal does not need password hashes, contact details or session keys.
*/
USE [HotelOpsDB];
GO
IF DB_NAME() <> N'HotelOpsDB' THROW 51908, 'Wrong database', 1;
IF SUSER_ID(N'REVIEW_EXISTING_READONLY_LOGIN') IS NULL
    THROW 51909, 'DBA must provision and approve a dedicated server principal first', 1;
IF DATABASE_PRINCIPAL_ID(N'HotelOpsAuthorizationReader') IS NOT NULL
    THROW 51910, 'Existing user: inspect effective permissions; do not overwrite', 1;
CREATE USER [HotelOpsAuthorizationReader] FOR LOGIN [REVIEW_EXISTING_READONLY_LOGIN];
GRANT CONNECT TO [HotelOpsAuthorizationReader];
GRANT SELECT ON OBJECT::dbo.users (user_id, account_status, deleted_at) TO [HotelOpsAuthorizationReader];
GRANT SELECT ON OBJECT::dbo.branches TO [HotelOpsAuthorizationReader];
GRANT SELECT ON OBJECT::dbo.roles TO [HotelOpsAuthorizationReader];
GRANT SELECT ON OBJECT::dbo.permissions TO [HotelOpsAuthorizationReader];
GRANT SELECT ON OBJECT::dbo.role_permissions TO [HotelOpsAuthorizationReader];
GRANT VIEW DEFINITION ON OBJECT::dbo.role_permissions TO [HotelOpsAuthorizationReader];
GRANT SELECT ON OBJECT::dbo.user_roles TO [HotelOpsAuthorizationReader];
GRANT SELECT ON OBJECT::dbo.user_branch_access TO [HotelOpsAuthorizationReader];
GRANT SELECT ON OBJECT::dbo.rooms TO [HotelOpsAuthorizationReader];
GRANT SELECT ON OBJECT::dbo.cleaning_tasks TO [HotelOpsAuthorizationReader];
GRANT SELECT ON OBJECT::dbo.cleaning_assignments TO [HotelOpsAuthorizationReader];
DENY INSERT, UPDATE, DELETE, EXECUTE, ALTER TO [HotelOpsAuthorizationReader];
GO
-- Validate while actually connected as that principal, including inherited
-- server/database/object permissions. The runner rejects broader rights.
