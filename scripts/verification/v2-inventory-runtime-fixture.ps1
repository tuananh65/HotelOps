param([ValidateSet('Setup','Start','Stop','Inspect','ConversionUnit','IssueSource','ApproveReturn','Cleanup')][string]$Action,[long]$ItemId=0,[decimal]$Quantity=0,[long]$DocumentId=0)
$ErrorActionPreference='Stop'
$RunId=[string]$env:HOTELOPS_V2_RUN_ID
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'));Set-Location $root
$database='HotelOpsCompactWorkdayTestV22611250010'
$directory=Join-Path $root 'artifacts/restructure-v2/inventory-private';New-Item -ItemType Directory -Force $directory|Out-Null
$fixturePath=Join-Path $directory 'fixture.json';$processPath=Join-Path $directory 'process.json'
$settings=Get-Content (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($settings.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$database
if($Action -eq 'Start'){
 if(Test-Path $processPath){throw 'Owned server metadata exists.'}
 if(-not $RunId -or -not(Test-Path $fixturePath)){throw 'Owned fixture required before Start.'}
 if(@(Get-NetTCPConnection -State Listen -ErrorAction Stop|Where-Object LocalPort -eq 5779).Count -gt 0){throw 'TEST port already in use; refusing to reuse another listener.'}
 $warningFixture=Get-Content $fixturePath -Raw|ConvertFrom-Json
 if([string]$warningFixture.runId -ne $RunId){throw 'Fixture belongs to another run.'}
 $server=Join-Path $directory 'server';New-Item -ItemType Directory -Force $server|Out-Null
 $build=if($env:HOTELOPS_V2_TEST_BUILD){[IO.Path]::GetFullPath($env:HOTELOPS_V2_TEST_BUILD)}else{Join-Path $root 'src/HotelOps.Api/bin/Debug/net10.0'}
 if(-not $build.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or -not(Test-Path (Join-Path $build 'HotelOps.Api.dll'))){throw 'Build must be an existing workspace assembly directory.'}
 Copy-Item -Path (Join-Path $build '*') -Destination $server -Recurse -Force
 $env:ConnectionStrings__HotelOpsDb=$builder.ConnectionString;$env:ASPNETCORE_ENVIRONMENT='Development';$env:Compact__WorkerEnabled='false';$env:Compact__CleaningOverdueEnabled='false';$env:Compact__AutoEndShiftsEnabled='false';$env:Email__Enabled='false';$env:AI__Enabled='false'
 $env:Inventory__ExpiryWarningDays='7'
 [Environment]::SetEnvironmentVariable('Inventory__Branches__'+$warningFixture.branch+'__ExpiryWarningDays','60','Process')
 $dll=Join-Path $server 'HotelOps.Api.dll'
 $p=Start-Process dotnet -ArgumentList @($dll,'--urls','http://127.0.0.1:5779') -WorkingDirectory (Join-Path $root 'src/HotelOps.Api') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $directory 'api.log') -RedirectStandardError (Join-Path $directory 'api.err.log')
 @{pid=$p.Id;created=$p.StartTime.ToUniversalTime().ToString('o');database=$database;port=5779;runId=$RunId}|ConvertTo-Json|Set-Content $processPath
 for($i=0;$i -lt 160;$i++){
  try{$health=Invoke-RestMethod 'http://127.0.0.1:5779/health' -TimeoutSec 1}catch{$health=$null}
  if($health.tables -eq 57){
   $listeners=@(Get-NetTCPConnection -State Listen -ErrorAction Stop|Where-Object LocalPort -eq 5779)
   if($p.HasExited -or $listeners.Count -eq 0 -or @($listeners|Where-Object OwningProcess -ne $p.Id).Count -gt 0){throw 'TEST API listener ownership mismatch.'}
   '{"ready":true}';exit 0
  }
  if($p.HasExited){throw 'API exited; inspect private log.'};Start-Sleep -Milliseconds 250
 }
 throw 'Readiness timeout.'
}
if($Action -eq 'Stop'){
 if(Test-Path $processPath){
  $owned=Get-Content $processPath -Raw|ConvertFrom-Json
  if([string]$owned.runId -ne $RunId){'{"stopped":false,"notOwned":true}';exit 0}
  $p=Get-Process -Id $owned.pid -ErrorAction SilentlyContinue
  if($p){if($p.StartTime.ToUniversalTime().ToString('o') -ne $owned.created){throw 'PID reuse guard.'};Stop-Process -Id $p.Id -Force}
  Remove-Item -LiteralPath $processPath
 };'{"stopped":true}';exit 0
}
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
function Query([string]$sql,[hashtable]$parameters=@{}){
 $cmd=$connection.CreateCommand();$cmd.CommandText=$sql;$cmd.CommandTimeout=45
 foreach($key in $parameters.Keys){[void]$cmd.Parameters.AddWithValue('@'+$key,$parameters[$key])}
 $reader=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$rows=@()
 try{while($reader.ReadAsync().GetAwaiter().GetResult()){$row=@{};for($i=0;$i -lt $reader.FieldCount;$i++){$v=$reader.GetValue($i);$row[$reader.GetName($i)]=if($v -is [DBNull]){$null}else{$v}};$rows+=,$row}}finally{$reader.Dispose();$cmd.Dispose()};return ,$rows
}
try{
 $connection.OpenAsync().GetAwaiter().GetResult()
 if($Action -eq 'Setup'){
  if(Test-Path $fixturePath){throw 'Existing fixture; inspect/cleanup first.'}
  $backup=Get-Content (Join-Path $root ('artifacts/workday/backup-'+$database+'.json')) -Raw|ConvertFrom-Json
  if(!$backup.verified -or $backup.database -ne $database -or ([DateTime]::UtcNow-[DateTime]::Parse($backup.at).ToUniversalTime()).TotalMinutes -gt 15){throw 'Fresh verified backup required.'}
  $branches=Query 'SELECT branch_id FROM dbo.branches WHERE is_active=1 ORDER BY branch_id';$branch=$branches[0].branch_id;$other=$branches[1].branch_id
  $warehouse=(Query 'SELECT TOP(1) warehouse_id FROM dbo.warehouses WHERE branch_id=@b AND is_active=1 ORDER BY warehouse_id' @{b=$branch})[0].warehouse_id
  $otherWarehouse=(Query 'SELECT TOP(1) warehouse_id FROM dbo.warehouses WHERE branch_id=@b AND is_active=1 ORDER BY warehouse_id' @{b=$other})[0].warehouse_id
  $room=(Query 'SELECT TOP(1) room_id FROM dbo.rooms WHERE branch_id=@b AND is_active=1 ORDER BY room_id' @{b=$branch})[0].room_id
  $unit=(Query "SELECT TOP(1) reference_id FROM dbo.reference_values WHERE kind='UOM' AND is_active=1 ORDER BY reference_id")[0].reference_id
  $fixture=@{database=$database;runId=$RunId;branch=$branch;otherBranch=$other;warehouse=$warehouse;otherWarehouse=$otherWarehouse;room=$room;unit=$unit;prefix='V2INV-'+[Guid]::NewGuid().ToString('N').Substring(0,16);sessions=@{};items=@{};backupAt=$backup.at}
  $fixture|ConvertTo-Json -Depth 6|Set-Content $fixturePath -Encoding utf8
  foreach($actor in @(@{key='store';role='STOREKEEPER';branch=$branch},@{key='manager';role='OPERATION_MANAGER';branch=$branch},@{key='otherManager';role='OPERATION_MANAGER';branch=$other},@{key='otherStore';role='STOREKEEPER';branch=$other})){
   $users=Query "SELECT TOP(1) u.user_id FROM dbo.users u JOIN dbo.user_roles ur ON ur.user_id=u.user_id JOIN dbo.roles r ON r.role_id=ur.role_id WHERE r.code=@role AND ur.branch_id=@b AND ur.revoked_at IS NULL AND ur.assigned_at<=SYSUTCDATETIME() AND (ur.expires_at IS NULL OR ur.expires_at>SYSUTCDATETIME()) AND r.is_active=1 AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL AND u.temporary_password_expires_at IS NULL ORDER BY u.user_id" @{role=$actor.role;b=$actor.branch}
   if($users.Count -ne 1){throw 'Missing fixture role user.'}
   $bytes=New-Object byte[] 32;$rng=[Security.Cryptography.RandomNumberGenerator]::Create();$rng.GetBytes($bytes);$rng.Dispose();$token=([BitConverter]::ToString($bytes)).Replace('-','');$sha=[Security.Cryptography.SHA256]::Create();$hash=$sha.ComputeHash($bytes);$sha.Dispose()
   $session=Query 'INSERT dbo.user_sessions(user_id,token_hash,security_stamp,expires_at) OUTPUT inserted.session_id SELECT user_id,@hash,security_stamp,DATEADD(hour,2,SYSUTCDATETIME()) FROM dbo.users WHERE user_id=@u' @{hash=$hash;u=$users[0].user_id}
   $fixture.sessions[$actor.key]=@{user=$users[0].user_id;session=$session[0].session_id;token=$token}
   $fixture|ConvertTo-Json -Depth 6|Set-Content $fixturePath -Encoding utf8
  }
  foreach($type in @('fefo','fifo','reusable','unknown')){
   $id=(Query 'INSERT dbo.inventory_items(code,name,base_unit_id,description) OUTPUT inserted.item_id VALUES(@code,@name,@unit,@description)' @{code=$fixture.prefix+'-'+$type;name='TEST V2 Inventory '+$type+' '+$fixture.prefix;unit=$unit;description='Synthetic isolated runtime fixture; not operational stock.'})[0].item_id
   $classification=if($type -eq 'reusable'){'REUSABLE'}else{'CONSUMABLE'};$policy=if($type -eq 'fefo'){'REQUIRED'}elseif($type -eq 'unknown'){'UNKNOWN'}else{'NONE'}
   [void](Query 'INSERT dbo.item_lifecycle_profiles(branch_id,item_id,classification,expiry_policy,updated_at) VALUES(@b,@item,@class,@policy,SYSUTCDATETIME())' @{b=$branch;item=$id;class=$classification;policy=$policy})
   [void](Query 'INSERT dbo.item_lifecycle_profiles(branch_id,item_id,classification,expiry_policy,updated_at) VALUES(@b,@item,@class,@policy,SYSUTCDATETIME())' @{b=$other;item=$id;class=$classification;policy=$policy})
   $fixture.items[$type]=$id;$fixture|ConvertTo-Json -Depth 6|Set-Content $fixturePath -Encoding utf8
  }
  '{"setup":true}';exit 0
 }
 $fixture=Get-Content $fixturePath -Raw|ConvertFrom-Json
 if($fixture.database -ne $database){throw 'TEST guard failed.'}
 if([string]$fixture.runId -ne $RunId){throw 'Fixture belongs to another run.'}
 $params=@{prefix=$fixture.prefix+'%';b=[long]$fixture.branch}
 if($Action -eq 'ConversionUnit'){
  $unit=(Query "INSERT dbo.reference_values(kind,code,name,base_unit_id,conversion_factor,sort_order,is_active) OUTPUT inserted.reference_id VALUES('UOM',@code,'Synthetic TEST pack of two',@base,2,0,1)" @{code=$fixture.prefix+'-UOM';base=[long]$fixture.unit})[0].reference_id
  @{unit=$unit;factor=2}|ConvertTo-Json -Compress;exit 0
 }
 if($Action -eq 'IssueSource'){
  if($ItemId -notin @($fixture.items.fefo,$fixture.items.fifo,$fixture.items.unknown,$fixture.items.reusable)){throw 'Nonfixture item refused.'}
  $params.room=if($ItemId -eq $fixture.items.reusable){[long]$fixture.room}else{[DBNull]::Value}
  $params.item=$ItemId;$params.qty=$Quantity;$params.warehouse=[long]$fixture.warehouse;$params.creator=[long]$fixture.sessions.store.user;$params.approver=[long]$fixture.sessions.manager.user;$params.unit=[long]$fixture.unit;$params.number=$fixture.prefix+'-MR-'+[Guid]::NewGuid().ToString('N').Substring(0,8)
  $rows=Query @'
SET XACT_ABORT ON;BEGIN TRANSACTION;
INSERT dbo.inventory_documents(branch_id,document_no,kind,status,source_warehouse_id,created_by,approved_by,approved_at,reason,receiving_room_id) VALUES(@b,@number,'MATERIAL_REQUEST','APPROVED',@warehouse,@creator,@approver,SYSUTCDATETIME(),'TEST synthetic approved source for batch allocation verification',@room);
DECLARE @doc bigint=SCOPE_IDENTITY();
INSERT dbo.inventory_document_lines(branch_id,document_id,line_no,item_id,unit_id,conversion_factor,requested_qty,approved_qty,actual_qty) VALUES(@b,@doc,1,@item,@unit,1,@qty,@qty,@qty);
SELECT @doc documentId,CAST(SCOPE_IDENTITY() AS bigint) lineId;COMMIT;
'@ $params
  $rows[0]|ConvertTo-Json -Compress;exit 0
 }
 if($Action -eq 'ApproveReturn'){
  $sql="DECLARE @changed TABLE(document_id bigint); UPDATE d SET approved_by=@approver,approved_at=SYSUTCDATETIME() OUTPUT inserted.document_id INTO @changed FROM dbo.inventory_documents d WHERE d.document_id=@id AND d.branch_id=@b AND d.kind='RETURN' AND d.status='DRAFT' AND d.posted_at IS NULL AND d.created_by=@creator AND EXISTS(SELECT 1 FROM dbo.inventory_document_lines l JOIN dbo.inventory_items i ON i.item_id=l.item_id WHERE l.document_id=d.document_id AND i.code LIKE @prefix) AND NOT EXISTS(SELECT 1 FROM dbo.inventory_document_lines l JOIN dbo.inventory_items i ON i.item_id=l.item_id WHERE l.document_id=d.document_id AND i.code NOT LIKE @prefix); SELECT document_id FROM @changed"
  $rows=Query $sql @{id=$DocumentId;b=[long]$fixture.branch;approver=[long]$fixture.sessions.manager.user;creator=[long]$fixture.sessions.store.user;prefix=$fixture.prefix+'%'}
  if($rows.Count -ne 1){throw 'Fixture RETURN approval guard failed.'}
  '{"approvedFixtureReturn":true}';exit 0
 }
 if($Action -eq 'Inspect'){
  $items=Query 'SELECT i.item_id,COALESCE((SELECT SUM(m.signed_base_qty) FROM dbo.stock_movements m WHERE m.item_id=i.item_id AND m.branch_id=@b),0) ledger,COALESCE((SELECT SUM(b.warehouse_quantity) FROM dbo.stock_batches b WHERE b.item_id=i.item_id AND b.branch_id=@b),0) batches,(SELECT COUNT(*) FROM dbo.stock_movements m WHERE m.item_id=i.item_id AND m.branch_id=@b) movementCount FROM dbo.inventory_items i WHERE i.code LIKE @prefix' $params
  $entries=Query 'SELECT e.batch_id,e.quantity,e.movement_id,m.item_id FROM dbo.stock_batch_entries e JOIN dbo.stock_movements m ON m.movement_id=e.movement_id JOIN dbo.inventory_items i ON i.item_id=m.item_id WHERE i.code LIKE @prefix ORDER BY e.entry_id' $params
  $positions=Query 'SELECT p.position_id,p.item_id,p.status,p.quantity,p.location_key FROM dbo.reusable_positions p JOIN dbo.inventory_items i ON i.item_id=p.item_id WHERE i.code LIKE @prefix' $params
  $warnings=Query "SELECT notification_id,recipient_user_id,branch_id,event_key,read_at,expires_at,message FROM dbo.notifications WHERE notification_type='EXPIRY_WARNING' AND message LIKE @prefix" $params
  @{items=$items;entries=$entries;positions=$positions;warnings=$warnings}|ConvertTo-Json -Depth 5 -Compress;exit 0
 }
 if($Action -eq 'Cleanup'){
  foreach($s in $fixture.sessions.psobject.Properties.Value){[void](Query 'UPDATE dbo.user_sessions SET revoked_at=COALESCE(revoked_at,SYSUTCDATETIME()) WHERE session_id=@s AND user_id=@u' @{s=[long]$s.session;u=[long]$s.user})}
  [void](Query 'UPDATE dbo.inventory_items SET is_active=0 WHERE code LIKE @prefix' $params)
  [void](Query "UPDATE dbo.reference_values SET is_active=0 WHERE kind='UOM' AND code LIKE @prefix" $params)
  $n=(Query 'SELECT COUNT(*) n FROM dbo.inventory_items WHERE code LIKE @prefix AND is_active=1' $params)[0].n
  Remove-Item -LiteralPath $fixturePath;@{cleaned=$true;activeFixtureItems=$n;ledgerHistoryRetained=$true}|ConvertTo-Json -Compress
 }
}finally{$connection.Dispose()}

