param([Parameter(Mandatory=$true)][string]$PrivateActorsFile,[string]$Endpoint,[int]$ApiProcessId,[switch]$AllowSyntheticTestReceipt,[switch]$PrepareOnly)
$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$private=Join-Path $root 'artifacts/p0-workflow/private'
[void](New-Item -ItemType Directory -Force -Path $private)
$identity=[System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$acl=New-Object System.Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true,$false)
$rule=New-Object System.Security.AccessControl.FileSystemAccessRule($identity,'FullControl','ContainerInherit,ObjectInherit','None','Allow')
$acl.AddAccessRule($rule)
# Apply only the directory DACL, without requesting SACL privileges.
([IO.DirectoryInfo]$private).SetAccessControl($acl)
$credentials=Join-Path $private 'credentials.json';$manifest=Join-Path $private 'manifest.json'
$inputPath=(Resolve-Path -LiteralPath $PrivateActorsFile).Path
if(!(Test-Path -LiteralPath $credentials)){Copy-Item -LiteralPath $inputPath -Destination $credentials}
elseif($inputPath -ne $credentials -and (Get-FileHash -LiteralPath $inputPath).Hash -ne (Get-FileHash -LiteralPath $credentials).Hash){throw 'Existing fixture secrets differ; refusing to overwrite'}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']='HotelOpsCompactWorkdayTestP0261124'
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString);$connection.Open()
try{
 if(!(Test-Path -LiteralPath $manifest)){
  $cmd=$connection.CreateCommand();$cmd.CommandTimeout=120;$cmd.CommandText=[IO.File]::ReadAllText((Join-Path $root 'scripts/database/compact-v2/p0-fixture.sql'))
  [void]$cmd.Parameters.Add('@Credentials',[System.Data.SqlDbType]::NVarChar,-1);$cmd.Parameters['@Credentials'].Value=[IO.File]::ReadAllText($credentials)
  $reader=$cmd.ExecuteReader();$json='';while($reader.Read()){$json+=$reader.GetString(0)};$reader.Dispose()
  [void]($json|ConvertFrom-Json);[IO.File]::WriteAllText($manifest,$json)
 }
 if($PrepareOnly){Write-Output 'Fixture identities/catalog prepared. No stock created. Runtime not tested.';return}
 if(!$AllowSyntheticTestReceipt){throw 'Pass -AllowSyntheticTestReceipt for simulated TEST goods receipts'}
 $uri=[Uri]$Endpoint
 if(!$uri.IsAbsoluteUri -or $uri.Host -notin @('127.0.0.1','localhost') -or $uri.Port -in @(5080,5769,5186,5173) -or $ApiProcessId -le 0){throw 'Dedicated local TEST endpoint and API PID required'}
 $listener=Get-NetTCPConnection -State Listen -LocalPort $uri.Port -ErrorAction Stop
 if(!($listener|Where-Object OwningProcess -eq $ApiProcessId)){throw 'Endpoint is not owned by declared TEST API process'}
 # An invalid opaque token performs a read-only session lookup; health itself has no DB query.
 try{Invoke-WebRequest -UseBasicParsing -Uri ($Endpoint.TrimEnd('/')+'/api/v2/auth/me') -Headers @{Authorization=('Bearer '+('0'*64))} -TimeoutSec 10|Out-Null}catch{if(!$_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 401){throw}}
 $probe=$connection.CreateCommand();$probe.CommandText='SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE host_process_id=@pid AND database_id=DB_ID() AND is_user_process=1'
 [void]$probe.Parameters.Add('@pid',[System.Data.SqlDbType]::Int);$probe.Parameters['@pid'].Value=$ApiProcessId
 if([int]$probe.ExecuteScalar() -lt 1){throw 'Cannot prove API SQL session targets TEST clone. Warm its read-only health endpoint first.'}
 $proof=@{database=$builder.InitialCatalog;baseUrl=$Endpoint;apiProcessId=$ApiProcessId;verifiedAt=[DateTime]::UtcNow.ToString('o');allowSyntheticTestReceipt=$true}
 [IO.File]::WriteAllText((Join-Path $private 'target-proof.json'),($proof|ConvertTo-Json))
 & node (Join-Path $PSScriptRoot 'p0-fixture-bootstrap.mjs') $private
 if($LASTEXITCODE){throw 'Fixture bootstrap failed; private checkpoint retains completed steps. No reset performed.'}
}finally{$connection.Dispose()}
