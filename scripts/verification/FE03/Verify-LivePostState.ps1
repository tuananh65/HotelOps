param([string]$EnvRoot='D:\SEP490\Projects\HotelOps-Integration\obj\env-fe03-001')
$ErrorActionPreference='Stop'
. (Join-Path $EnvRoot 'Common.ps1')
Assert-Hax
$m=Get-Content (Join-Path $EnvRoot 'manifest.json') -Raw|ConvertFrom-Json
$admin=Open-Sql $TrialDatabase
try{
 Assert-Trial $admin $m -Lease
 $row=(Sql-Rows $admin "SELECT (SELECT COUNT(*) FROM dbo.rooms) rooms,(SELECT COUNT(*) FROM dbo.users) users,(SELECT COUNT(*) FROM dbo.cleaning_tasks) cleaning_tasks,(SELECT COUNT(*) FROM dbo.room_inspections) inspections,(SELECT COUNT(*) FROM dbo.room_status_history) recorded_history,(SELECT COUNT(*) FROM dbo.room_blocks) recorded_blocks,(SELECT COUNT(*) FROM dbo.user_sessions WHERE revoked_at IS NULL) unrevoked_sessions").Rows[0]
 if($row.rooms -ne 4 -or $row.users -ne 2 -or $row.cleaning_tasks -ne 0 -or $row.inspections -ne 0 -or $row.recorded_history -ne 1 -or $row.recorded_blocks -ne 1 -or $row.unrevoked_sessions -ne 0){throw 'Post-acceptance fixture/session state unexpected'}
 $result=@{status='PASS';rooms=[int]$row.rooms;users=[int]$row.users;cleaning_tasks=[int]$row.cleaning_tasks;inspections=[int]$row.inspections;recorded_history=[int]$row.recorded_history;recorded_blocks=[int]$row.recorded_blocks;unrevoked_sessions=[int]$row.unrevoked_sessions;lease_verified=$true;utc=[DateTimeOffset]::UtcNow.ToString('o')}
 $result|ConvertTo-Json|Set-Content (Join-Path $PSScriptRoot '../../../obj/fe03-independent-004/post-acceptance.json') -Encoding UTF8
 'POST_ACCEPTANCE_PASS: unchanged fixture counts; zero unrevoked sessions'
}finally{$admin.Dispose()}
