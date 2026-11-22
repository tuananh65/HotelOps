param(
 [ValidateSet('HotelOpsCompactDB','HotelOpsCompactWorkdayTestStaffingV4202611221659')][string]$Database='HotelOpsCompactDB',
 [ValidateSet('PREFLIGHT','APPLY','FAIL_BEFORE_COMMIT','STALE_SNAPSHOT')][string]$Mode='PREFLIGHT',
 [Guid]$BatchId='c7046ec2-244c-4eaa-8799-b9e946422659'
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$out=Join-Path $root 'artifacts/staffing-v4';[void](New-Item -ItemType Directory -Force $out)
$roster=(Get-Content -Raw -Encoding UTF8 (Join-Path $root 'artifacts/staffing-v3-implementation/validation-HotelOpsCompactDB.json') | ConvertFrom-Json).roster
$grants=(Get-Content -Raw -Encoding UTF8 (Join-Path $root 'artifacts/staffing-v3/diagnostics.json') | ConvertFrom-Json).role_grants
$new=@(
 [pscustomobject]@{employee_code='DH002';username='MinhNQ1';full_name='Nguyễn Quốc Minh';email='MinhNQ1@ducvuong.com';role_code='DEPT_MANAGER';department_code='HK'},
 [pscustomobject]@{employee_code='EMP050';username='KhangTD1';full_name='Trần Đức Khang';email='KhangTD1@ducvuong.com';role_code='STOREKEEPER';department_code='STORE'}
)
$credentials='[]'
if($Mode -in @('APPLY','FAIL_BEFORE_COMMIT')){
 $approval=Get-Content -Raw -Encoding UTF8 -LiteralPath 'C:/Users/Hax/.codex/attachments/82bc7a51-170d-4678-8cc5-509c7bd68b67/Pasted text.txt'
 $match=[regex]::Match($approval,'Mật khẩu dùng chung cho 56 tài khoản:\s*`([^`]+)`')
 if(-not $match.Success){throw 'Approved credential source not found.'}
 $inputJson=@{password=$match.Groups[1].Value;usernames=@($roster.username)+@($new.username)} | ConvertTo-Json -Compress
 $hasher=Join-Path $out 'hasher/bin/Release/net10.0/StaffingV4Hasher.dll'
 if(-not (Test-Path -LiteralPath $hasher)){throw 'Build the isolated Identity helper before applying.'}
 $credentials=($inputJson | & dotnet $hasher) -join ''
 $inputJson=$null;$approval=$null;$match=$null
 $credentialExit=$LASTEXITCODE;$parsedCredentials=$credentials | ConvertFrom-Json
 if($credentialExit -ne 0 -or $parsedCredentials.Count -ne 56){throw "Credential helper failed: exit=$credentialExit count=$($parsedCredentials.Count)."}
 $parsedCredentials=$null
}
if($Mode -ne 'PREFLIGHT'){
 & (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Database $Database
 Copy-Item -LiteralPath (Join-Path $root "artifacts/workday/backup-$Database.json") -Destination (Join-Path $out "backup-$Database-$Mode.json")
}
$config=Get-Content -Raw -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') | ConvertFrom-Json
$b=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$b['Initial Catalog']=$Database
$c=New-Object System.Data.SqlClient.SqlConnection($b.ConnectionString)
function JsonResult($cmd){$r=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$sb=New-Object Text.StringBuilder;try{while($r.ReadAsync().GetAwaiter().GetResult()){[void]$sb.Append($r.GetString(0))}}finally{$r.Dispose()};return $sb.ToString()}
try{
 $c.OpenAsync().GetAwaiter().GetResult();$cmd=$c.CreateCommand();$cmd.CommandTimeout=180
 # Canonical snapshot includes rowversions and sessions; kept in memory only. The locked SQL batch rechecks every table.
 $cmd.CommandText=@'
SET NOCOUNT ON;
DECLARE @result TABLE(table_name sysname,digest varchar(66));
DECLARE @table sysname,@order nvarchar(max),@sql nvarchar(max),@hash varbinary(32);
DECLARE t CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.tables WHERE is_ms_shipped=0 ORDER BY name;
OPEN t;FETCH NEXT FROM t INTO @table;
WHILE @@FETCH_STATUS=0 BEGIN
 SELECT @order=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal) FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID('dbo.'+@table) AND i.is_primary_key=1;
 SET @sql=N'SELECT @h=HASHBYTES(''SHA2_256'',(SELECT * FROM dbo.'+QUOTENAME(@table)+N' ORDER BY '+@order+N' FOR JSON PATH,INCLUDE_NULL_VALUES))';
 EXEC sys.sp_executesql @sql,N'@h varbinary(32) OUTPUT',@h=@hash OUTPUT;
 INSERT @result VALUES(@table,CONVERT(varchar(66),@hash,1));FETCH NEXT FROM t INTO @table;
END;CLOSE t;DEALLOCATE t;
SELECT * FROM @result ORDER BY table_name FOR JSON PATH;
'@
 $snapshot=JsonResult $cmd
 if($Mode -eq 'STALE_SNAPSHOT'){$s=$snapshot | ConvertFrom-Json;$s[0].digest='0x'+('00'*32);$snapshot=ConvertTo-Json -InputObject $s -Compress}
 $cmd.CommandText=Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot '10_StaffingV4.sql')
 foreach($entry in @{Snapshot=$snapshot;Roster=(ConvertTo-Json -InputObject @($roster) -Depth 10 -Compress);Grants=(ConvertTo-Json -InputObject @($grants) -Depth 10 -Compress);NewUsers=(ConvertTo-Json -InputObject $new -Compress);Credentials=$credentials}.GetEnumerator()){
  [void]$cmd.Parameters.Add('@'+$entry.Key,[Data.SqlDbType]::NVarChar,-1);$cmd.Parameters['@'+$entry.Key].Value=$entry.Value
 }
 [void]$cmd.Parameters.AddWithValue('@Mode',$Mode);[void]$cmd.Parameters.AddWithValue('@BatchId',$BatchId)
 try{
  $r=$cmd.ExecuteReaderAsync().GetAwaiter().GetResult();$rows=@();try{while($r.ReadAsync().GetAwaiter().GetResult()){$row=[ordered]@{};for($i=0;$i -lt $r.FieldCount;$i++){$row[$r.GetName($i)]=if($r.IsDBNull($i)){$null}else{$r.GetValue($i)}};$rows+=[pscustomobject]$row}}finally{$r.Dispose()}
  if($Mode -in @('FAIL_BEFORE_COMMIT','STALE_SNAPSHOT')){throw 'Expected fault was not raised.'}
 }catch{
  $errorNumber=$_.Exception.InnerException.Number
  if(($Mode -eq 'FAIL_BEFORE_COMMIT' -and $errorNumber -eq 51799) -or ($Mode -eq 'STALE_SNAPSHOT' -and $errorNumber -eq 51703)){$rows=@([pscustomobject]@{result='EXPECTED_REJECTION';mode=$Mode;sql_error=$errorNumber})}else{throw}
 }
 $json=ConvertTo-Json -InputObject $rows -Depth 10
 [IO.File]::WriteAllText((Join-Path $out "$Database-$Mode.json"),$json,(New-Object Text.UTF8Encoding($false)))
 $json
}finally{$credentials=$null;$snapshot=$null;if($cmd){$cmd.Parameters.Clear();$cmd.Dispose()};$c.Dispose()}
