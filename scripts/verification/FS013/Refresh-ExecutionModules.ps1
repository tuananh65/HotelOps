[CmdletBinding()]
param([switch]$RunApprovedTrial)
$ErrorActionPreference='Stop'
if(!$RunApprovedTrial){'PLAN_ONLY: refresh existing FS013 module definitions, never recreate tables';return}
$c=New-Object System.Data.SqlClient.SqlConnection('Data Source=.\SQLEXPRESS;Initial Catalog=HotelOps_Canonical_20261106_f130001_Fresh;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5')
try {
 $c.OpenAsync().GetAwaiter().GetResult()
 $guard=$c.CreateCommand()
 $guard.CommandText="IF DB_NAME()<>'HotelOps_Canonical_20261106_f130001_Fresh' OR CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))<>HOST_NAME() OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),'')<>'SQLEXPRESS' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS013.Owner' AND CONVERT(nvarchar(100),value)='DEV1_FS013') OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS013.Lifecycle' AND CONVERT(nvarchar(100),value)='V1') THROW 51960,'Existing FS013 lifecycle required',1;"
 try{[void]$guard.ExecuteNonQueryAsync().GetAwaiter().GetResult()}finally{$guard.Dispose()}
 $source=Join-Path $PSScriptRoot '../../database/release/fs013-candidate/11_CleaningExecution.sql'
 $batches=[regex]::Split([IO.File]::ReadAllText($source),'(?im)^\s*GO\s*\r?$')
 foreach($batch in @($batches|Select-Object -Skip 1)) {
  if(!$batch.Trim()){continue}
  if($batch -notmatch '^\s*(CREATE OR ALTER PROCEDURE dbo\.fs013_(verify_reassignment|reassign_with_evidence)|DENY INSERT,)'){throw 'Unexpected non-module refresh batch'}
  $cmd=$c.CreateCommand();$cmd.CommandTimeout=15;$cmd.CommandText=$batch
  try{[void]$cmd.ExecuteNonQueryAsync().GetAwaiter().GetResult()}finally{$cmd.Dispose()}
 }
 'FS013_EXISTING_MODULES_REFRESHED_NO_TABLE_RESET'
} finally {$c.Dispose()}
