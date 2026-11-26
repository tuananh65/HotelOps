param([switch]$ApplyMigration,[ValidatePattern('^HotelOpsCompactWorkdayTest[A-Za-z0-9_]+$')][string]$Database='HotelOpsCompactWorkdayTestNote2511260145')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if($ApplyMigration){& "$root/scripts/database/compact-v2/Backup-Workday.ps1" -Database $database}
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$database
$db=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
try {
 $db.OpenAsync().GetAwaiter().GetResult()
 $cmd=$db.CreateCommand();$cmd.CommandTimeout=180
 if($ApplyMigration){
  $cmd.CommandText=Get-Content "$root/scripts/database/compact-v2/note-25-11-refill-om.sql" -Raw -Encoding UTF8
  [void]$cmd.ExecuteNonQueryAsync().GetAwaiter().GetResult()
 }
 $cmd.CommandText="SELECT DB_NAME() database_name,(SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) tables,(SELECT COUNT(*) FROM dbo.request_workflows WHERE request_type='CONSUMABLE_REFILL_REQUEST' AND status='PENDING_SUPERVISOR') legacy_pending"
 $r=$cmd.ExecuteReader();while($r.Read()){Write-Output "PASS database=$($r['database_name']) tables=$($r['tables']) legacyPending=$($r['legacy_pending'])"};$r.Close()
}finally{$db.Dispose()}
