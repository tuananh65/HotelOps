param([Parameter(Mandatory=$true)][ValidateSet('target','stock','additional','inventory','balances')][string]$Mode,[string]$Endpoint,[long]$BranchId=0,[long]$ItemId=0,[long]$AllocationId=0)
$ErrorActionPreference='Stop'
$database='HotelOpsCompactWorkdayTestP0261124'
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw|ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$connection.Open()
try {
 $command=$connection.CreateCommand();$command.CommandTimeout=60
 foreach($entry in @{branch=$BranchId;item=$ItemId;allocation=$AllocationId}.GetEnumerator()) { [void]$command.Parameters.Add('@'+$entry.Key,[System.Data.SqlDbType]::BigInt);$command.Parameters['@'+$entry.Key].Value=$entry.Value }
 if($Mode -eq 'target') {
  $uri=[Uri]$Endpoint
  if($uri.Host -notin @('localhost','127.0.0.1') -or $uri.Port -in @(5080,5769,5186,5173) -or $uri.Port -le 0){throw 'Dedicated TEST endpoint required'}
  $owners=@(Get-NetTCPConnection -State Listen -LocalPort $uri.Port | Select-Object -ExpandProperty OwningProcess -Unique)
  if($owners.Count -ne 1){throw 'Exactly one API process must own TEST listener'}
  [void]$command.Parameters.Add('@pid',[System.Data.SqlDbType]::Int);$command.Parameters['@pid'].Value=$owners[0]
  $command.CommandText='SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE host_process_id=@pid AND database_id=DB_ID() AND is_user_process=1'
  if([int]$command.ExecuteScalar() -lt 1){throw 'No live SQL connection from API process to exact TEST database'}
  @{database=$database;baseUrl=$Endpoint;processId=[int]$owners[0];verifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Compress
  return
 }
 $command.CommandText=switch($Mode){
  'stock' {'SELECT COALESCE(SUM(signed_base_qty),0) FROM dbo.stock_movements WHERE branch_id=@branch AND item_id=@item'}
  'additional' {'SELECT COALESCE(SUM(additional_issued),0) FROM dbo.staff_supply_balances WHERE allocation_id=@allocation AND item_id=@item'}
  'inventory' {@'
SELECT m.branch_id branchId,m.item_id itemId,m.warehouse_id warehouseId,CAST(0 AS decimal(18,4)) opening,
 SUM(CASE WHEN m.signed_base_qty>0 AND d.kind NOT IN ('RETURN','SUPPLY_RETURN') THEN m.signed_base_qty ELSE 0 END) receipts,
 SUM(CASE WHEN m.signed_base_qty<0 THEN -m.signed_base_qty ELSE 0 END) issues,
 SUM(CASE WHEN m.signed_base_qty>0 AND d.kind IN ('RETURN','SUPPLY_RETURN') THEN m.signed_base_qty ELSE 0 END) returns,
 COALESCE(MAX(v.base_qty),0) closing
FROM dbo.stock_movements m JOIN dbo.branches b ON b.branch_id=m.branch_id
 JOIN dbo.inventory_document_lines l ON l.line_id=m.line_id JOIN dbo.inventory_documents d ON d.document_id=l.document_id
 LEFT JOIN dbo.v_stock_balances v ON v.branch_id=m.branch_id AND v.warehouse_id=m.warehouse_id AND v.item_id=m.item_id
WHERE b.code IN ('P0_TEST_DVBV','P0_TEST_DVCT')
GROUP BY m.branch_id,m.item_id,m.warehouse_id FOR JSON PATH
'@}
  'balances' {@'
SELECT s.balance_id balanceId,s.initial_issued initialIssued,s.additional_issued additionalIssued,s.reserved,s.used,s.returned,
 s.initial_issued+s.additional_issued-s.used-s.returned remaining,
 s.initial_issued+s.additional_issued-s.used-s.returned-s.reserved available,
 CAST(CASE WHEN a.status='CLOSED' THEN 1 ELSE 0 END AS bit) closed
FROM dbo.staff_supply_balances s JOIN dbo.staff_supply_allocations a ON a.allocation_id=s.allocation_id
 JOIN dbo.branches b ON b.branch_id=a.branch_id WHERE b.code IN ('P0_TEST_DVBV','P0_TEST_DVCT') FOR JSON PATH
'@}
 }
 if($Mode -in @('stock','additional')) { $command.ExecuteScalar()|ConvertTo-Json -Compress }
 else { $reader=$command.ExecuteReader();$json='';while($reader.Read()){$json+=$reader.GetString(0)};$reader.Dispose();if(!$json){$json='[]'};Write-Output $json }
} finally {$connection.Dispose()}
