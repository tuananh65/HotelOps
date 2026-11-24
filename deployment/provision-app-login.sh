#!/usr/bin/env bash
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
deployment_lock
require_stopped_api
: "${DB_APP_PASSWORD:?Provide a random application password via the private environment}"
# Restricted alphabet makes SQL quoting and connection-string parsing unambiguous.
[[ "$DB_APP_PASSWORD" =~ ^[A-Za-z0-9_!@#%+=-]{24,128}$ ]] || { echo 'Use a 24+ character random application password without quotes/semicolons' >&2; exit 1; }
[[ ! -e "$DB_CONNECTION_FILE" ]] || { echo 'Existing connection file refused; use an explicit credential rotation procedure' >&2; exit 1; }
bash "$DEPLOY_ROOT/deployment/backup-database.sh"
sql -d master <<SQL
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM sys.server_principals WHERE name=N'hotelops_app') THROW 51900,'Application login already exists',1;
CREATE LOGIN hotelops_app WITH PASSWORD=N'$DB_APP_PASSWORD',CHECK_POLICY=ON;
USE [$DATABASE_NAME];
IF EXISTS(SELECT 1 FROM sys.database_principals WHERE name=N'hotelops_app') THROW 51900,'Application database user already exists; review restore mapping',1;
CREATE USER hotelops_app FOR LOGIN hotelops_app;
ALTER ROLE db_datareader ADD MEMBER hotelops_app;
ALTER ROLE db_datawriter ADD MEMBER hotelops_app;
GRANT VIEW DEFINITION TO hotelops_app;
GRANT EXECUTE TO hotelops_app;
COMMIT;
SQL
mkdir -p "$(dirname "$DB_CONNECTION_FILE")"
printf 'Server=db;Database=%s;User ID=hotelops_app;Password=%s;Encrypt=True;TrustServerCertificate=True\n' "$DATABASE_NAME" "$DB_APP_PASSWORD" > "$DB_CONNECTION_FILE"
# Parent directory must be root-only (0700). Container app UID can read only the mounted file.
chmod 444 "$DB_CONNECTION_FILE"
echo 'Created application database login and private connection file; no business role grants changed.'
