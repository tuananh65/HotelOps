param([string]$Database='HotelOpsCompactWorkdayTestP0261124',[string[]]$Scripts=@('p0-role-migration.sql','p0-supplies.sql','p0-readiness.sql'),[switch]$Rollback)
$ErrorActionPreference='Stop'
if($Database -notmatch '^HotelOpsCompactWorkdayTestP0[0-9A-Za-z_]+$'){throw 'Only isolated P0 TEST database accepted'}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$Database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$connection.Open()
try{
 $begin=$connection.CreateCommand();$begin.CommandText='SET XACT_ABORT ON; BEGIN TRANSACTION;';[void]$begin.ExecuteNonQuery()
 foreach($script in $Scripts){
  if($script -notmatch '^p0-[a-z-]+\.sql$'){throw 'Only reviewed P0 migration scripts accepted'}
  $source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot $script))
  foreach($batch in [regex]::Split($source,'(?m)^GO\s*\r?$')){
   if([string]::IsNullOrWhiteSpace($batch)){continue}
   $cmd=$connection.CreateCommand();$cmd.CommandTimeout=120;$cmd.CommandText=$batch
   $reader=$cmd.ExecuteReader()
   do {while($reader.Read()){$row=@{};for($i=0;$i -lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=$reader.GetValue($i)};$row|ConvertTo-Json -Compress}} while($reader.NextResult())
   $reader.Dispose()
  }
  Write-Output ('PASS '+$script)
 }
 $check=$connection.CreateCommand();$check.CommandTimeout=180;$check.CommandText=@'
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.triggers WHERE is_disabled=1) THROW 51990,'Untrusted or disabled database guards.',1;
DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;
'@
 $reader=$check.ExecuteReader();$violations=0
 do {while($reader.Read()){$violations++}}while($reader.NextResult());$reader.Dispose()
 if($violations -gt 0){throw ('Constraint violations: '+$violations)}
 $counts=$connection.CreateCommand();$counts.CommandText="SELECT (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) tables,(SELECT COUNT(*) FROM sys.foreign_keys) fks,(SELECT COUNT(*) FROM sys.check_constraints) checks,(SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1) triggers";
 $reader=$counts.ExecuteReader();while($reader.Read()){$row=@{};for($i=0;$i -lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=$reader.GetValue($i)};$row|ConvertTo-Json -Compress};$reader.Dispose()
 $finish=$connection.CreateCommand();$finish.CommandText=if($Rollback){'ROLLBACK;'}else{'COMMIT;'};[void]$finish.ExecuteNonQuery()
 Write-Output $(if($Rollback){'PASS all batches and trusted constraints; ROLLED BACK as requested.'}else{'PASS committed to isolated TEST database.'})
}catch{
 try{$undo=$connection.CreateCommand();$undo.CommandText='IF @@TRANCOUNT>0 ROLLBACK;';[void]$undo.ExecuteNonQuery()}catch{}
 throw
}finally{$connection.Dispose()}
