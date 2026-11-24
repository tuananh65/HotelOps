param([string]$Database='HotelOpsCompactDB')
$ErrorActionPreference='Stop'
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
$builder['Initial Catalog']=$Database
$conn=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$conn.Open()
try {
 $cmd=$conn.CreateCommand();$cmd.CommandText=@'
SELECT b.code branch,u.full_name,r.code role,ur.assigned_at,CONVERT(varchar(19),DATEADD(hour,7,ur.assigned_at),120) assigned_local FROM user_roles ur JOIN users u ON u.user_id=ur.user_id JOIN roles r ON r.role_id=ur.role_id JOIN branches b ON b.branch_id=ur.branch_id WHERE r.code='MAINTENANCE_SUPERVISOR' AND ur.revoked_at IS NULL AND b.code IN('DVBV','DVCT');
SELECT b.code branch,r.code role,a.work_date,a.status,COUNT(*) assignments FROM workday_assignments a JOIN workday_shifts s ON s.shift_id=a.shift_id JOIN roles r ON r.role_id=s.role_id JOIN branches b ON b.branch_id=s.branch_id WHERE b.code IN('DVBV','DVCT') AND r.code IN('MAINTENANCE_LEAD','MAINTENANCE_STAFF') GROUP BY b.code,r.code,a.work_date,a.status ORDER BY a.work_date DESC;
SELECT a.assignment_id,b.assignment_id other_assignment,a.user_id,a.work_date,a.status,b.status other_status FROM workday_assignments a JOIN workday_assignments b ON a.user_id=b.user_id AND a.work_date=b.work_date AND a.assignment_id<b.assignment_id JOIN workday_shifts sa ON sa.shift_id=a.shift_id JOIN workday_shifts sb ON sb.shift_id=b.shift_id JOIN branches br ON br.branch_id=sa.branch_id WHERE br.code IN('DVBV','DVCT') AND a.status<>'CANCELLED' AND b.status<>'CANCELLED' AND sa.start_minute<sb.end_minute AND sa.end_minute>sb.start_minute;
SELECT s.shift_id,b.code branch,s.start_minute,s.end_minute,u.user_id,u.full_name FROM workday_shifts s JOIN branches b ON b.branch_id=s.branch_id JOIN roles r ON r.role_id=s.role_id JOIN user_roles ur ON ur.role_id=r.role_id AND ur.branch_id=s.branch_id JOIN users u ON u.user_id=ur.user_id WHERE b.code IN('DVBV','DVCT') AND r.code='MAINTENANCE_SUPERVISOR' AND s.is_active=1 AND ur.revoked_at IS NULL AND ur.assigned_at<CONVERT(datetime2,'2026-11-25T01:00:00') AND u.account_status='ACTIVE' AND EXISTS(SELECT 1 FROM memberships m WHERE m.user_id=u.user_id AND m.branch_id=s.branch_id AND m.department_id=s.department_id AND m.kind='EMPLOYMENT' AND m.valid_to IS NULL) AND NOT EXISTS(SELECT 1 FROM workday_assignments a JOIN workday_shifts t ON t.shift_id=a.shift_id WHERE a.user_id=u.user_id AND a.work_date='2026-11-25' AND a.status<>'CANCELLED' AND t.start_minute<s.end_minute AND t.end_minute>s.start_minute);
'@
 $da=New-Object System.Data.SqlClient.SqlDataAdapter($cmd);$ds=New-Object System.Data.DataSet;[void]$da.Fill($ds)
 $results=@();foreach($table in $ds.Tables){$rows=@();foreach($row in $table.Rows){$o=@{};foreach($c in $table.Columns){$o[$c.ColumnName]=$row[$c.ColumnName]};$rows+=$o};$results+=,@($rows);$table | Format-Table -AutoSize | Out-String -Width 200 | Write-Output}
 New-Item -ItemType Directory -Force artifacts/workday-role-validation | Out-Null
 @{database=$Database;at=[DateTime]::UtcNow.ToString('o');results=$results} | ConvertTo-Json -Depth 8 | Set-Content artifacts/workday-role-validation/audit.json -Encoding utf8
}finally{$conn.Dispose()}
