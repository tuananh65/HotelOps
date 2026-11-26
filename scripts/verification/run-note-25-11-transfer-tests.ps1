$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']='HotelOpsCompactWorkdayTestNote2511260145'
$env:HOTELOPS_NOTE2511_TEST=$builder.ConnectionString
try { & dotnet test "$root/tests/HotelOps.Compact.Tests/HotelOps.Compact.Tests.csproj" -c Release --no-restore --filter FullyQualifiedName~Note2511TransferDatabaseTests -m:1 -p:UseSharedCompilation=false; if($LASTEXITCODE){throw 'Note2511 transfer SQL tests failed.'} }
finally {Remove-Item Env:HOTELOPS_NOTE2511_TEST -ErrorAction SilentlyContinue}
