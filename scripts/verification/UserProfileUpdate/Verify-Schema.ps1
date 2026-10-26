param([Parameter(Mandatory=$true)][string]$Server)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$setup=Join-Path $root 'scripts/verification/UserDirectory/Prepare-Database.ps1'
$forward=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'rowversion-forward.sql'))
$rollback=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'rowversion-rollback.sql'))
$checks=0
function Check([bool]$ok,[string]$label){if(-not $ok){throw "FAIL: $label"};$script:checks++;Write-Output "PASS: $label"}
function Scalar($connection,[string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandTimeout=120;$cmd.CommandText=$sql;try{return $cmd.ExecuteScalar()}finally{$cmd.Dispose()}}
function Execute($connection,[string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandTimeout=120;$cmd.CommandText=$sql;try{[void]$cmd.ExecuteNonQuery()}finally{$cmd.Dispose()}}
$trials=@()
foreach($kind in @('fresh','upgrade')){
 $name='HotelOps_Canonical_'+[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')+'_'+[guid]::NewGuid().ToString('N').Substring(0,8)+'_Fresh'
 & $setup -Database $name
 $c=New-Object System.Data.SqlClient.SqlConnection "Data Source=$Server;Initial Catalog=$name;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10;Application Name=HotelOps Task26 Schema Trial"
 try{
  $c.Open()
  Check ((Scalar $c 'SELECT CONVERT(nvarchar(128),@@SERVERNAME)') -ceq $Server) "$kind server verified"
  Check ((Scalar $c 'SELECT DB_NAME()') -ceq $name) "$kind target verified"
  Check ((Scalar $c 'SELECT COUNT(*) FROM dbo.users') -eq 0) "$kind starts with empty owned personnel"
  Check ((Scalar $c 'SELECT COUNT(*) FROM sys.tables') -eq 82) "$kind combined baseline has82 tables"
  Check ((Scalar $c "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') AND name='row_version'") -eq 0) "$kind baseline has no rowversion"
  if($kind -eq 'upgrade'){
   Execute $c "INSERT dbo.users(employee_code,username,email,full_name,account_status,employment_status,created_at) VALUES('T26_UPGRADE','t26.upgrade','t26.upgrade@example.invalid',N'Upgrade Fixture','INACTIVE','ACTIVE',SYSUTCDATETIME());"
  }
  $snapshot=Scalar $c 'SELECT user_id,employee_code,username,email,full_name,account_status,employment_status,primary_branch_id,department_id FROM dbo.users ORDER BY user_id FOR JSON PATH,INCLUDE_NULL_VALUES'
  Execute $c $forward
  Check ((Scalar $c "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') AND name='row_version' AND system_type_id=189 AND is_nullable=0") -eq 1) "$kind forward creates nonnullable SQL rowversion"
  Check ((Scalar $c 'SELECT user_id,employee_code,username,email,full_name,account_status,employment_status,primary_branch_id,department_id FROM dbo.users ORDER BY user_id FOR JSON PATH,INCLUDE_NULL_VALUES') -ceq $snapshot) "$kind forward preserves all existing user fixture values"
  Execute $c $forward
  Check ((Scalar $c "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') AND name='row_version'") -eq 1) "$kind forward replay is idempotent"
  if($kind -eq 'upgrade'){
   Check ((Scalar $c 'SELECT COUNT(*) FROM dbo.users WHERE DATALENGTH(row_version)=8') -eq 1) 'existing upgrade row receives8byte token'
   Execute $c "DECLARE @old binary(8)=(SELECT row_version FROM dbo.users WHERE username='t26.upgrade'); UPDATE dbo.users SET full_name=N'Changed Fixture' WHERE username='t26.upgrade' AND row_version=@old; IF @@ROWCOUNT<>1 THROW 51980,'Expected one write',1; UPDATE dbo.users SET phone='stale' WHERE username='t26.upgrade' AND row_version=@old; IF @@ROWCOUNT<>0 THROW 51981,'Stale token updated row',1; UPDATE dbo.users SET full_name=N'Upgrade Fixture' WHERE username='t26.upgrade'; IF EXISTS(SELECT 1 FROM dbo.users WHERE username='t26.upgrade' AND row_version=@old) THROW 51982,'ABA token repeated',1;"
   Check $true 'upgrade token changes, rejects stale write and detects ABA'
  }
  Execute $c $rollback
  Check ((Scalar $c "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') AND name='row_version'") -eq 0) "$kind rollback removes only owned token"
  Check ((Scalar $c 'SELECT user_id,employee_code,username,email,full_name,account_status,employment_status,primary_branch_id,department_id FROM dbo.users ORDER BY user_id FOR JSON PATH,INCLUDE_NULL_VALUES') -ceq $snapshot) "$kind rollback preserves fixture identity/profile"
  Execute $c "EXEC sys.sp_dropextendedproperty @name=N'HotelOps.UserDirectory.Trial';"
  $rejected=$false
  try{Execute $c $forward}catch{if($_.Exception.InnerException.Number -eq 51970){$rejected=$true}else{throw}}
  Check $rejected "$kind forward rejects unverified target before DDL"
  Execute $c "EXEC sys.sp_addextendedproperty @name=N'HotelOps.UserDirectory.Trial',@value=N'Task17';"
  Execute $c $forward
  Check ((Scalar $c 'SELECT COUNT(*) FROM sys.tables') -eq 82) "$kind reapply adds no tables"
  $trials+=@{kind=$kind;database=$name}
 }finally{$c.Dispose()}
}
$report=@{success=$true;checks=$checks;trials=$trials;secretsPersisted=$false}
$evidence=Join-Path $PSScriptRoot ('schema-'+[guid]::NewGuid().ToString('N')+'.local.json')
$report|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $evidence -Encoding UTF8
Write-Output "PASS: $checks schema checks; receipt $evidence"
