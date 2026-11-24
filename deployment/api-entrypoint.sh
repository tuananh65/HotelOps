#!/bin/sh
set -eu
test -r /run/secrets/db-connection || { echo 'Missing database connection secret' >&2; exit 1; }
ConnectionStrings__HotelOpsDb=$(cat /run/secrets/db-connection)
export ConnectionStrings__HotelOpsDb
exec dotnet HotelOps.Api.dll "$@"
