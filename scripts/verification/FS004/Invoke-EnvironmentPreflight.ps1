[CmdletBinding()]
param([switch]$SqlMetadata,[switch]$AllowLocalSqlCertificateException,
      [ValidatePattern('^HotelOps_Canonical_[0-9]{8}_[0-9]{6}_[a-f0-9]{7,8}_Fresh$')][string]$TrialDatabase)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$result=[ordered]@{kind='READ_ONLY_PREFLIGHT';sqlWrites=$false;backendStarted=$false;dotnetSdk=(& dotnet --version);node=(& node --version);npm=(& npm --version);sqlExpressService='Unknown';localDbInstalled=$false;sqlcmdInstalled=[bool](Get-Command sqlcmd -ErrorAction SilentlyContinue);apiSecretConfigured=$false;currentCatalogIsProposedTrial=$false;frontendEnvLocalExists=(Test-Path (Join-Path $root 'frontend/.env.local'));environmentKeys=@();certificateCheck='Unknown';sqlMetadata='NotRequested';sqlCertificateException=[bool]$AllowLocalSqlCertificateException;trialExists=$null;schema=$null;liveE2E='NOT_CERTIFIED_BY_PREFLIGHT'}
try{$service=Get-Service -Name 'MSSQL$SQLEXPRESS' -ErrorAction Stop;$result.sqlExpressService=[string]$service.Status}catch{$result.sqlExpressService='Unavailable'}
$result.localDbInstalled=[bool](Get-Command SqlLocalDB -ErrorAction SilentlyContinue)
$result.environmentKeys=@(Get-ChildItem Env: | Where-Object {$_.Name -match 'HOTEL|SQL|CONNECTION|HTTPS|CERT|PROXY|FS004|ASPNET|DOTNET'} | Select-Object -ExpandProperty Name)
$secretPath=Join-Path $env:APPDATA 'Microsoft/UserSecrets/b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json'
if(Test-Path -LiteralPath $secretPath){
 $secrets=Get-Content -Raw -Encoding utf8 -LiteralPath $secretPath|ConvertFrom-Json
 $value=[string]$secrets.'ConnectionStrings:HotelOpsDb';$result.apiSecretConfigured=![string]::IsNullOrWhiteSpace($value)
 if($value){try{$b=New-Object System.Data.Common.DbConnectionStringBuilder;$b.set_ConnectionString($value);$catalog=if($b.ContainsKey('Initial Catalog')){[string]$b.get_Item('Initial Catalog')}elseif($b.ContainsKey('Database')){[string]$b.get_Item('Database')}else{''};$result.currentCatalogIsProposedTrial=($TrialDatabase -and $catalog -ceq $TrialDatabase)}catch{}}
}
$certOutput=@(& dotnet dev-certs https --check --trust 2>&1);$certExit=$LASTEXITCODE
$result.certificateCheck=if($certExit -eq 0){'ValidAndTrusted'}else{'NoValidTrustedDevCertificate'}
if($SqlMetadata){
 $connection=$null
 try{
  $b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
  $b['Data Source']='.\SQLEXPRESS';$b['Initial Catalog']='master';$b['Integrated Security']=$true
  $b['Encrypt']=$true;$b['TrustServerCertificate']=[bool]$AllowLocalSqlCertificateException;$b['Connect Timeout']=5;$b['Application Name']='HotelOps FS004 readonly preflight'
  $connection=New-Object System.Data.SqlClient.SqlConnection $b.ConnectionString
  $budget=New-Object Threading.CancellationTokenSource;$budget.CancelAfter(10000)
  $connection.OpenAsync($budget.Token).GetAwaiter().GetResult()
  $cmd=$connection.CreateCommand();$cmd.CommandTimeout=5
  $cmd.CommandText="SELECT CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')),CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),CONVERT(varchar(20),SERVERPROPERTY('ProductVersion')),CONVERT(varchar(40),SERVERPROPERTY('Edition')),CASE WHEN IS_SRVROLEMEMBER('sysadmin')=1 OR HAS_PERMS_BY_NAME(NULL,NULL,'CREATE ANY DATABASE')=1 THEN 1 ELSE 0 END"
  $reader=$cmd.ExecuteReaderAsync($budget.Token).GetAwaiter().GetResult()
  try{if(!$reader.ReadAsync($budget.Token).GetAwaiter().GetResult() -or $reader.GetString(0) -ine [Environment]::MachineName -or $reader.GetString(1) -ine 'SQLEXPRESS'){throw 'LOCAL_SQL_IDENTITY_MISMATCH'};$result.sqlVersion=$reader.GetString(2);$result.sqlEdition=$reader.GetString(3);$result.canCreateDatabase=($reader.GetInt32(4)-eq 1)}finally{$reader.Dispose();$cmd.Dispose()}
  $result.sqlMetadata='PassedLocalIdentity'
  if($TrialDatabase){
   $cmd=$connection.CreateCommand();$cmd.CommandText='SELECT COUNT(*) FROM sys.databases WHERE name=@name';[void]$cmd.Parameters.AddWithValue('@name',$TrialDatabase)
   try{$result.trialExists=([int]$cmd.ExecuteScalarAsync($budget.Token).GetAwaiter().GetResult() -eq 1)}finally{$cmd.Dispose()}
   if($result.trialExists){
    $connection.ChangeDatabase($TrialDatabase)
    $cmd=$connection.CreateCommand();$cmd.CommandText="SELECT COUNT(*) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FS004.Owner' AND CONVERT(nvarchar(128),value)=N'DEV1_FS004'"
    try{$owned=([int]$cmd.ExecuteScalarAsync($budget.Token).GetAwaiter().GetResult()-eq 1)}finally{$cmd.Dispose()}
    if(!$owned){$result.schema='UnownedDatabaseNotInspected'}else{
     $cmd=$connection.CreateCommand();$cmd.CommandText="SELECT (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') AND name='row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0),(SELECT COUNT(*) FROM sys.extended_properties WHERE class=0 AND name IN('HotelOps.Checklists.V1','HotelOps.Housekeeping.V1','HotelOps.Progress.V1')),(SELECT COUNT(*) FROM sys.tables WHERE name IN('cleaning_item_definitions','cleaning_progress_versions','cleaning_task_items','task_progress_versions','task_completion_criteria','progress_evidence_links'))"
     $reader=$cmd.ExecuteReaderAsync($budget.Token).GetAwaiter().GetResult()
     try{[void]$reader.ReadAsync($budget.Token).GetAwaiter().GetResult();$result.schema=[ordered]@{userRowVersion=$reader.GetInt32(0);optionalMarkers=$reader.GetInt32(1);progressTables=$reader.GetInt32(2);runtimeCompatibility='RequiresActualValidators'}}finally{$reader.Dispose();$cmd.Dispose()}
    }
   }
  }
 }catch{$result.sqlErrorType=$_.Exception.GetBaseException().GetType().Name;$message=[string]$_.Exception.ToString();$result.sqlMetadata=if($message -match 'certificate|SSL|trust chain'){'BlockedCertificate'}elseif($message -match 'LOCAL_SQL_IDENTITY_MISMATCH'){'BlockedIdentity'}elseif($message -match 'Login failed|permission'){'BlockedPermission'}else{'BlockedConnectivityOrProvider'}
 }finally{if($connection){$connection.Dispose()};if($budget){$budget.Dispose()}}
}
$result|ConvertTo-Json -Depth 6
