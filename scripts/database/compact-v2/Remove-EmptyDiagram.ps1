param([Parameter(Mandatory=$true)][string]$Database)
$ErrorActionPreference='Stop'
if($Database -notmatch '^HotelOpsCompact(DB|WorkdayTest[A-Za-z0-9_]+)$'){throw 'Database outside approved scope.'}
& "$PSScriptRoot/Backup-Workday.ps1" -Database $Database
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$Database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString);$connection.Open()
try {
 $cmd=$connection.CreateCommand();$cmd.CommandText=@'
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOpsCompactSchema' AND CONVERT(nvarchar(20),value)='2.1.0') THROW 51411,'Expected schema 2.1.0.',1;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>41 OR OBJECT_ID('dbo.sysdiagrams','U') IS NULL THROW 51412,'Expected 40 business tables plus sysdiagrams.',1;
IF EXISTS(SELECT 1 FROM dbo.sysdiagrams WITH(TABLOCKX,HOLDLOCK)) THROW 51413,'Diagram table is not empty; nothing removed.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.sysdiagrams') OR referenced_object_id=OBJECT_ID('dbo.sysdiagrams')) THROW 51414,'Unexpected diagram relationships.',1;
DROP TABLE dbo.sysdiagrams;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>40 THROW 51415,'Table budget mismatch.',1;
COMMIT;
'@
 [void]$cmd.ExecuteNonQuery();Write-Output "PASS: empty sysdiagrams removed after backup; $Database has 40 tables."
}finally{$connection.Dispose()}
