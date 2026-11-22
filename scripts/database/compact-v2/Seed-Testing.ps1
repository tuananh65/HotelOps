param([string]$Server='.\SQLEXPRESS',[string]$Database='HotelOpsCompactDB',[ValidateRange(1024,65535)][int]$Port=5190)
$ErrorActionPreference='Stop'
if($Database -notmatch '^HotelOpsCompact[A-Za-z0-9_]*$'){throw 'Only a HotelOpsCompact database is allowed.'}
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$folder=Join-Path $root "artifacts/compact-v2/seed/$Database"
$statePath=Join-Path $folder 'state.json';$credentialsPath=Join-Path $folder 'credentials.json'
$connection=$null;$api=$null;$tokens=@{};$state=@{version=1;server=$Server;database=$Database;pending=$null;complete=$false;steps=@{}}
$envNames=@('ConnectionStrings__HotelOpsDb','ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS','Compact__WorkerEnabled','Compact__Bootstrap__Username','Compact__Bootstrap__Password')
$saved=@{};foreach($name in $envNames){$saved[$name]=[Environment]::GetEnvironmentVariable($name)}
function Save-State {
    $json=ConvertTo-Json -InputObject $state -Depth 70
    [IO.File]::WriteAllText($statePath+'.tmp',$json,(New-Object Text.UTF8Encoding($false)))
    if(Test-Path -LiteralPath $statePath){[IO.File]::Replace($statePath+'.tmp',$statePath,$statePath+'.bak')}else{[IO.File]::Move($statePath+'.tmp',$statePath)}
}
function Sql($query){
    $cmd=$connection.CreateCommand();$cmd.CommandText=$query;$cmd.CommandTimeout=120
    try{$reader=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$table=New-Object Data.DataTable;$table.Load($reader);$reader.Dispose();return ,$table}finally{$cmd.Dispose()}
}
function Step($seedStepName,[scriptblock]$seedOperation){
    if($state.steps.ContainsKey($seedStepName)){return $state.steps[$seedStepName]}
    $state.pending=$seedStepName;Save-State
    try{$seedValue=& $seedOperation}catch{
        # A rejected HTTP request is transactional. A lost response is ambiguous: retain the checkpoint for review.
        if($_.Exception.Data['DefinitiveFailure']){$state.pending=$null;Save-State};throw
    }
    $state.steps[$seedStepName]=$seedValue;$state.pending=$null;Save-State;Write-Host "Seed: $seedStepName";return $seedValue
}
function Call($method,$path,$body=$null,$user='demo_manager',$expected=200){
    $headers=@{};if($tokens.ContainsKey($user)){$headers.Authorization='Bearer '+$tokens[$user]}
    $args=@{Uri="http://127.0.0.1:$Port$path";Method=$method;Headers=$headers;UseBasicParsing=$true}
    if($null-ne $body){$args.Body=[Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $body -Depth 25 -Compress));$args.ContentType='application/json; charset=utf-8'}
    try{$response=Invoke-WebRequest @args;$status=[int]$response.StatusCode;$content=$response.Content}
    catch{if(-not $_.Exception.Response){throw};$status=[int]$_.Exception.Response.StatusCode;$reader=New-Object IO.StreamReader($_.Exception.Response.GetResponseStream());$content=$reader.ReadToEnd();$reader.Dispose()}
    if($status-ne $expected){$e=New-Object Exception "$method $path expected $expected, got $status : $content";if($status-ge 400 -and $status-lt 500){$e.Data['DefinitiveFailure']=$true};throw $e}
    if($content){return ($content|ConvertFrom-Json)}
}
function Catalog($key,$resource,$fields,$branch=$null){
    $path="/api/v2/catalog/$resource";if($null-ne $branch){$path+='?branchId='+$branch}
    return (Step $key { (Call POST $path @{fields=$fields} 'demo_admin').fields })
}
function Work($key,$kind,$title,$extra=@{},$user='demo_manager',$branch=$sg){
    $body=@{branchId=$branch;kind=$kind;title='[DEMO] '+$title};foreach($k in $extra.Keys){$body[$k]=$extra[$k]}
    return (Step "$key.create" {(Call POST '/api/v2/work' $body $user).fields})
}
function Act($key,$work,$action,$user,$extra=@{}){
    return (Step "$key.$action" {
        $body=@{rowVersion=$work.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='Du lieu mau phuc vu kiem thu'}
        foreach($k in $extra.Keys){$body[$k]=$extra[$k]}
        (Call POST "/api/v2/work/$($work.workId)/actions/$action" $body $user).fields
    })
}
function Document($key,$kind,$extra=@{},$user='demo_store'){
    $body=@{branchId=$sg;kind=$kind;reason='DEMO testing fixture'};foreach($k in $extra.Keys){$body[$k]=$extra[$k]}
    return (Step "$key.create" {(Call POST '/api/v2/documents' $body $user).fields})
}
function Line($key,$doc,$item,$qty,$user='demo_store',$extra=@{}){
    return (Step "$key.line" {
        $body=@{rowVersion=$doc.rowVersion;itemId=$item.itemId;unitId=$unit.referenceId;quantity=$qty;unitCost=15000};foreach($k in $extra.Keys){$body[$k]=$extra[$k]}
        Call POST "/api/v2/documents/$($doc.documentId)/lines" $body $user
    })
}
function DocAct($key,$doc,$action,$user='demo_store'){
    return (Step "$key.$action" {(Call POST "/api/v2/documents/$($doc.documentId)/actions/$action" @{rowVersion=$doc.rowVersion;operationKey=[Guid]::NewGuid().ToString();reason='DEMO testing fixture'} $user).fields})
}
function Verify-Sql {
    $check=Sql ([IO.File]::ReadAllText((Join-Path $PSScriptRoot '05_Verify.sql')))
    if($check.Rows.Count-ne 1 -or $check.Rows[0].table_count-ne 37){throw 'Database integrity verification failed.'}
    $stats=Sql @'
SELECT 'users' entity,COUNT(*) total FROM users UNION ALL SELECT 'branches',COUNT(*) FROM branches
UNION ALL SELECT 'departments',COUNT(*) FROM departments UNION ALL SELECT 'rooms',COUNT(*) FROM rooms
UNION ALL SELECT 'assets',COUNT(*) FROM assets UNION ALL SELECT 'work_items',COUNT(*) FROM work_items
UNION ALL SELECT 'inventory_documents',COUNT(*) FROM inventory_documents UNION ALL SELECT 'inventory_items',COUNT(*) FROM inventory_items
UNION ALL SELECT 'stock_movements',COUNT(*) FROM stock_movements UNION ALL SELECT 'work_events',COUNT(*) FROM work_events
UNION ALL SELECT 'work_intervals',COUNT(*) FROM work_intervals UNION ALL SELECT 'notifications',COUNT(*) FROM notifications
UNION ALL SELECT 'audit_logs',COUNT(*) FROM audit_logs;
'@
    $rows=@($stats.Rows|ForEach-Object {@{entity=$_.entity;total=$_.total}})
    $report=@{database=$Database;verifiedAt=[DateTime]::UtcNow.ToString('o');tables=37;integrity='DBCC CHECKDB and CHECKCONSTRAINTS passed';counts=$rows;seedVersion=1}
    $report|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $folder 'verification.json') -Encoding UTF8
    $stats|Format-Table -AutoSize|Out-Host
}
try{
    $connection=New-Object Data.SqlClient.SqlConnection "Server=$Server;Database=$Database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
    $connection.OpenAsync().GetAwaiter().GetResult()
    $lock=Sql "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='HotelOps.SeedTesting.v1',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @r result;"
    if($lock.Rows[0].result-lt 0){throw 'Another demo seed process holds the database lock.'}
    $meta=Sql "SELECT CONVERT(varchar(20),value) version FROM sys.extended_properties WHERE class=0 AND name='HotelOpsCompactSchema';"
    if($meta.Rows.Count-ne 1 -or $meta.Rows[0].version-ne '2.0.0'){throw 'Schema v2.0.0 required.'}
    if(Test-Path -LiteralPath $statePath){
        $loaded=Get-Content -LiteralPath $statePath -Raw|ConvertFrom-Json
        if($loaded.server-ne $Server -or $loaded.database-ne $Database -or $loaded.version-ne 1){throw 'Checkpoint target/version mismatch.'}
        $state.complete=$loaded.complete;$state.pending=$loaded.pending
        foreach($p in $loaded.steps.PSObject.Properties){$state.steps[$p.Name]=$p.Value}
        if($state.pending){throw "Unconfirmed seed step '$($state.pending)'. Inspect the database and checkpoint before retrying; no automatic replay."}
        if($state.complete){Verify-Sql;Write-Host 'Seed already complete. No rows inserted or changed.';return}
        $credentials=Get-Content -LiteralPath $credentialsPath -Raw|ConvertFrom-Json
    }else{
        $count=(Sql 'SELECT COALESCE(SUM(p.rows),0) n FROM sys.partitions p JOIN sys.tables t ON t.object_id=p.object_id WHERE p.index_id IN (0,1) AND t.is_ms_shipped=0').Rows[0].n
        if($count-ne 0){throw 'Initial demo seeding requires an empty database. Existing business data will not be overwritten.'}
        [void][IO.Directory]::CreateDirectory($folder)
        $credentials=@{database=$Database;password='Demo!'+[Guid]::NewGuid().ToString('N');purpose='LOCAL TEST DATA ONLY - all demo accounts share this generated password';usernames=@('demo_admin','demo_manager','demo_reception','demo_hk1','demo_hk2','demo_tech','demo_store','demo_head','demo_auditor','demo_dn_manager','demo_dn_hk')}
        $credentials|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $credentialsPath -Encoding UTF8
        Save-State
    }
    $env:ConnectionStrings__HotelOpsDb=$connection.ConnectionString;$env:ASPNETCORE_ENVIRONMENT='Development';$env:ASPNETCORE_URLS="http://127.0.0.1:$Port";$env:Compact__WorkerEnabled='false'
    if([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()|Where-Object Port -eq $Port){throw "Port $Port is already in use. Choose a free -Port; existing processes are not stopped."}
    $dll=Join-Path $root 'src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll'
    if(-not(Test-Path -LiteralPath $dll)){throw 'Build HotelOps.slnx before seeding.'}
    $null=Step 'bootstrap' {
        $env:Compact__Bootstrap__Username='demo_admin';$env:Compact__Bootstrap__Password=$credentials.password
        try{$p=Start-Process dotnet -ArgumentList @($dll,'--bootstrap-admin') -WorkingDirectory $root -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $folder 'bootstrap.log') -RedirectStandardError (Join-Path $folder 'bootstrap-error.log')
            if($p.ExitCode-ne 0){throw 'Demo bootstrap failed. Inspect local log.'};$true
        }finally{$env:Compact__Bootstrap__Password=$null}
    }
    $null=Step 'identities' {$null=Sql ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Seed-Testing-Identity.sql')));$true}
    $ids=@{};foreach($r in (Sql 'SELECT username,user_id FROM users').Rows){$ids[$r.username]=[long]$r.user_id}
    $sg=[long](Sql "SELECT branch_id FROM branches WHERE code='DEMO-SG'").Rows[0].branch_id
    $dn=[long](Sql "SELECT branch_id FROM branches WHERE code='DEMO-DN'").Rows[0].branch_id
    $departments=@{};foreach($r in (Sql 'SELECT b.code branch_code,d.code,d.department_id FROM departments d JOIN branches b ON b.branch_id=d.branch_id').Rows){$departments[$r.branch_code+'.'+$r.code]=[long]$r.department_id}
    $hk=$departments['DEMO-SG.HK'];$eng=$departments['DEMO-SG.ENG'];$storeDept=$departments['DEMO-SG.STORE']
    $api=Start-Process dotnet -ArgumentList @($dll) -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $folder 'api.log') -RedirectStandardError (Join-Path $folder 'api-error.log')
    $ready=$false;for($i=0;$i-lt 100;$i++){try{$health=Call GET '/health';$ready=$true;break}catch{if($api.HasExited){throw 'API exited; inspect api-error.log'};Start-Sleep -Milliseconds 250}}
    if(-not $ready -or $health.tables-ne 37){throw 'Compact API did not become ready.'}
    foreach($name in $credentials.usernames){$tokens[$name]=(Call POST '/api/v2/auth/login' @{username=$name;password=$credentials.password} '').accessToken}
    . (Join-Path $PSScriptRoot 'Seed-Testing-Scenarios.ps1')
    . (Join-Path $PSScriptRoot 'Verify-Testing-Api.ps1')
    Verify-Sql
    $state.complete=$true;Save-State
    Write-Host "Seed completed. Local credentials: $credentialsPath"
}finally{
    foreach($token in @($tokens.Keys)){try{$null=Call POST '/api/v2/auth/logout' $null $token}catch{}}
    if($api -and -not $api.HasExited){Stop-Process -Id $api.Id -Force}
    if($connection){$connection.Dispose()}
    foreach($name in $envNames){[Environment]::SetEnvironmentVariable($name,$saved[$name])}
}
