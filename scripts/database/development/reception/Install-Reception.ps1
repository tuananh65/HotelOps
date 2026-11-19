param(
 [ValidateSet('Access','Links','Demo')][string]$Package,
 [string]$ExpectedDatabase='HotelOpsDB',
 [switch]$Apply
)
$ErrorActionPreference='Stop'
if(!$Package){throw 'Specify -Package Access, Links or Demo'}
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$project=[xml](Get-Content (Join-Path $repo 'src/HotelOps.Api/HotelOps.Api.csproj') -Raw)
$secretId=($project.Project.PropertyGroup.UserSecretsId | Where-Object {$_} | Select-Object -First 1)
if(!$secretId){throw 'UserSecretsId missing'}
$settings=Get-Content (Join-Path $env:APPDATA "Microsoft/UserSecrets/$secretId/secrets.json") -Raw | ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.'ConnectionStrings:HotelOpsDb')
if($builder.InitialCatalog -ne $ExpectedDatabase){throw 'Unexpected database'}
$scripts=switch($Package){'Access'{@('ReceptionOwnRequests.sql','HuyPrimaryMembership.sql')};'Links'{@('InternalRequestLinks.sql')};'Demo'{@('SeedReadyRooms.sql')}}
$c=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
function Scalar([string]$sql){$q=$c.CreateCommand();$q.CommandText=$sql;try{return $q.ExecuteScalarAsync().GetAwaiter().GetResult()}finally{$q.Dispose()}}
function Snapshot {
 Scalar "SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT (SELECT COUNT(*) FROM dbo.rooms) rooms,(SELECT COUNT(*) FROM dbo.cleaning_tasks) tasks,(SELECT COUNT(*) FROM dbo.room_inspections) inspections,(SELECT COUNT(*) FROM dbo.audit_logs) audits,(SELECT COUNT(*) FROM dbo.employee_department_memberships) memberships,(SELECT role_id,permission_id,data_scope FROM dbo.role_permissions ORDER BY role_permission_id FOR JSON PATH) grants,(SELECT name FROM sys.tables ORDER BY name FOR JSON PATH) tables,(SELECT name,CONVERT(nvarchar(128),value) value FROM sys.extended_properties WHERE class=0 ORDER BY name FOR JSON PATH) markers FOR JSON PATH)),2)"
}
function ExecutePackage($tx){
 foreach($file in $scripts){
  $q=$c.CreateCommand();$q.Transaction=$tx;$q.CommandTimeout=180;$q.CommandText=[IO.File]::ReadAllText((Join-Path $PSScriptRoot $file))
  [void]$q.Parameters.AddWithValue('@ExpectedDatabaseName',$ExpectedDatabase)
  [void]$q.Parameters.AddWithValue('@ExpectedServer',$script:server)
  try{[void]$q.ExecuteNonQueryAsync().GetAwaiter().GetResult()}finally{$q.Dispose()}
 }
}
try{
 $c.OpenAsync().GetAwaiter().GetResult()
 $script:server=[string](Scalar "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))")
 $dir=[string](Scalar "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'))")
 $backup=$dir.TrimEnd('\')+'\Reception_'+$Package+'_'+[guid]::NewGuid().ToString('N')+'.bak'
 $q=$c.CreateCommand();$q.CommandTimeout=180;$q.CommandText='BACKUP DATABASE '+('['+$ExpectedDatabase.Replace(']',']]')+']')+' TO DISK=@path WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM;'
 [void]$q.Parameters.AddWithValue('@path',$backup)
 try{[void]$q.ExecuteNonQueryAsync().GetAwaiter().GetResult()}finally{$q.Dispose()}
 $before=Snapshot
 $tx=$c.BeginTransaction([Data.IsolationLevel]::Serializable)
 try{ExecutePackage $tx;$tx.Rollback()}catch{if($tx.Connection){$tx.Rollback()};throw}finally{$tx.Dispose()}
 if((Snapshot) -ne $before){throw 'Rollback verification differs'}
 if($Apply){
  $tx=$c.BeginTransaction([Data.IsolationLevel]::Serializable)
  try{ExecutePackage $tx;$tx.Commit()}catch{if($tx.Connection){$tx.Rollback()};throw}finally{$tx.Dispose()}
 }
 $outDir=Join-Path $repo 'artifacts/reception-completion'
 New-Item -ItemType Directory -Force $outDir | Out-Null
 $result=[pscustomobject]@{package=$Package;backup=$backup;backupVerified=$true;rollbackVerified=$true;applied=[bool]$Apply;at=(Get-Date).ToString('o')}
 $result|ConvertTo-Json|Set-Content (Join-Path $outDir "install-$Package.json") -Encoding utf8
 $result|ConvertTo-Json
}finally{$c.Dispose()}
