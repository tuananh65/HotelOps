param([string]$Server='.\SQLEXPRESS',[ValidatePattern('^HotelOpsCompact[A-Za-z0-9_]*$')][string]$Database='HotelOpsCompactDB',[int]$Port=5193)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$folder=Join-Path $root 'artifacts/compact-v2/functional/primary'
[void][IO.Directory]::CreateDirectory($folder)
$seedFolder=Join-Path $root "artifacts/compact-v2/seed/$Database"
$connection=$null;$api=$null;$tokens=@{}
$envNames=@('ConnectionStrings__HotelOpsDb','ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS','Compact__WorkerEnabled')
$saved=@{};foreach($name in $envNames){$saved[$name]=[Environment]::GetEnvironmentVariable($name)}
function Sql($query){$cmd=$connection.CreateCommand();$cmd.CommandText=$query;$cmd.CommandTimeout=120;try{$r=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$t=New-Object Data.DataTable;$t.Load($r);$r.Dispose();return ,$t}finally{$cmd.Dispose()}}
function Call($method,$path,$body=$null,$user='demo_manager',$expected=200){
    $headers=@{};if($tokens.ContainsKey($user)){$headers.Authorization='Bearer '+$tokens[$user]}
    $args=@{Uri="http://127.0.0.1:$Port$path";Method=$method;Headers=$headers;UseBasicParsing=$true}
    if($null-ne $body){$args.Body=[Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $body -Depth 20 -Compress));$args.ContentType='application/json; charset=utf-8'}
    try{$response=Invoke-WebRequest @args;$status=[int]$response.StatusCode;$content=$response.Content}
    catch{if(-not $_.Exception.Response){throw};$status=[int]$_.Exception.Response.StatusCode;$r=New-Object IO.StreamReader($_.Exception.Response.GetResponseStream());$content=$r.ReadToEnd();$r.Dispose()}
    if($status-ne $expected){throw "$method $path expected $expected, got $status : $content"}
    if($content){return ($content|ConvertFrom-Json)}
}
function Counts{
    return @((Sql "SELECT t.name,SUM(p.rows) n FROM sys.tables t JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN (0,1) WHERE t.is_ms_shipped=0 AND t.name<>'user_sessions' GROUP BY t.name ORDER BY t.name").Rows|ForEach-Object {[ordered]@{table=$_.name;rows=$_.n}})
}
try{
    $state=Get-Content -LiteralPath (Join-Path $seedFolder 'state.json') -Raw|ConvertFrom-Json
    if(-not $state.complete -or $state.database-ne $Database -or $state.server-ne $Server){throw 'Completed matching local seed checkpoint required.'}
    $credentials=Get-Content -LiteralPath (Join-Path $seedFolder 'credentials.json') -Raw|ConvertFrom-Json
    if([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()|Where-Object Port -eq $Port){throw "Port $Port is already in use."}
    $connection=New-Object Data.SqlClient.SqlConnection "Server=$Server;Database=$Database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";$connection.OpenAsync().GetAwaiter().GetResult()
    $before=Counts
    $ids=@{};foreach($r in (Sql 'SELECT username,user_id FROM users').Rows){$ids[$r.username]=[long]$r.user_id}
    $sg=[long](Sql "SELECT branch_id FROM branches WHERE code='DEMO-SG'").Rows[0].branch_id
    $dn=[long](Sql "SELECT branch_id FROM branches WHERE code='DEMO-DN'").Rows[0].branch_id
    $main=$state.steps.'warehouse.main';$items=@($state.steps.'item.0');$partialMr=$state.steps.'mr.partial.approve';$live=$state.steps.'live.clean.start'
    $env:ConnectionStrings__HotelOpsDb=$connection.ConnectionString;$env:ASPNETCORE_ENVIRONMENT='Development';$env:ASPNETCORE_URLS="http://127.0.0.1:$Port";$env:Compact__WorkerEnabled='false'
    $dll=Join-Path $root 'src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll'
    $api=Start-Process dotnet -ArgumentList @($dll) -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $folder 'api.log') -RedirectStandardError (Join-Path $folder 'api-error.log')
    $ready=$false;for($i=0;$i-lt 100;$i++){try{$health=Call GET '/health';$ready=$true;break}catch{if($api.HasExited){throw 'API exited; inspect api-error.log'};Start-Sleep -Milliseconds 250}}
    if(-not $ready -or $health.tables-ne 37){throw 'Compact schema gate failed.'}
    foreach($name in $credentials.usernames){$tokens[$name]=(Call POST '/api/v2/auth/login' @{username=$name;password=$credentials.password} '').accessToken}
    . (Join-Path $PSScriptRoot 'Verify-Testing-Api.ps1')
    foreach($kind in @('CLEANING','REWORK','INSPECTION','OPERATIONAL','INCIDENT','MAINTENANCE','INTERNAL_REQUEST')){
        $page=Call GET "/api/v2/work?branchId=$sg&kind=$kind" $null demo_manager
        Check "Primary $kind inbox and detail" ($page.total-gt 0)
        $null=Call GET "/api/v2/work/$($page.items[0].fields.workId)" $null demo_manager
    }
    foreach($kind in @('MATERIAL_REQUEST','RECEIPT','ISSUE','REPLENISHMENT','STOCKTAKE')){
        $page=Call GET "/api/v2/documents?branchId=$sg&kind=$kind" $null demo_store
        Check "Primary $kind document list and detail" ($page.total-gt 0)
        $null=Call GET "/api/v2/documents/$($page.items[0].fields.documentId)" $null demo_store
    }
    foreach($resource in @('branches','references','items','roles','permissions')){
        $page=Call GET "/api/v2/catalog/$resource" $null demo_admin;Check "Primary $resource shared catalog" ($page.total-gt 0)
    }
    foreach($resource in @('departments','locations','room-types','rooms','warehouses','stock-policies','assets')){
        $page=Call GET "/api/v2/catalog/$resource`?branchId=$sg" $null demo_admin;Check "Primary $resource branch catalog" ($page.total-gt 0)
    }
    $users=Call GET '/api/v2/users' $null demo_admin;Check 'Primary user catalog exposes eleven test identities' ($users.Count-eq 11)
    $templates=Call GET "/api/v2/templates?branchId=$sg" $null demo_manager
    Check 'Primary published checklist and recurring schedules' ($templates.checklists.Count-eq 1 -and $templates.schedules.Count-eq 2)
    foreach($report in @('tasks','cleaning','incidents','maintenance','requests','stock','documents')){
        $data=Call GET "/api/v2/insights/reports/$report`?branchId=$sg" $null demo_auditor
        Check "Primary populated $report report" (@($data).Count-gt 0)
    }
    $integrity=Sql ([IO.File]::ReadAllText((Join-Path $PSScriptRoot '05_Verify.sql')))
    Check 'Primary DBCC and exact 37-table schema' ($integrity.Rows.Count-eq 1 -and $integrity.Rows[0].table_count-eq 37)
    $after=Counts
    Check 'Primary functional checks did not add or remove business rows' ((ConvertTo-Json -InputObject $before -Compress)-eq (ConvertTo-Json -InputObject $after -Compress))
    @{database=$Database;verifiedAt=[DateTime]::UtcNow.ToString('o');passed=$checks.Count;failed=0;checks=@($checks.ToArray());before=$before;after=$after}|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $folder 'functional-verification.json') -Encoding UTF8
}finally{
    foreach($name in @($tokens.Keys)){try{$null=Call POST '/api/v2/auth/logout' $null $name}catch{}}
    if($api -and -not $api.HasExited){Stop-Process -Id $api.Id -Force}
    if($connection){$connection.Dispose()}
    foreach($name in $envNames){[Environment]::SetEnvironmentVariable($name,$saved[$name])}
}
