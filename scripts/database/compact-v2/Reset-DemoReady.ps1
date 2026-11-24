param([Parameter(Mandatory=$true)][string]$Database,[switch]$Apply,[switch]$AllowReadinessBaselineChange)
$ErrorActionPreference='Stop'
if($Database -notmatch '^HotelOpsCompact(DB|WorkdayTestUx[A-Za-z0-9_]*)$'){throw 'Unexpected database scope.'}
if(-not $Apply -or -not $AllowReadinessBaselineChange){throw 'Requires explicit Apply and AllowReadinessBaselineChange after reviewing reset-demo-ready.sql.'}
& (Join-Path $PSScriptRoot 'Backup-Workday.ps1') -Database $Database
if($LASTEXITCODE -and $LASTEXITCODE -ne 0){throw 'Backup failed.'}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb');$builder['Initial Catalog']=$Database
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString);$connection.Open()
try{$cmd=$connection.CreateCommand();$cmd.CommandTimeout=120;$cmd.CommandText=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'reset-demo-ready.sql') -Raw -Encoding UTF8
 $table=New-Object Data.DataTable;$reader=$cmd.ExecuteReader();$table.Load($reader);$reader.Dispose()
 $table|Select-Object deletedDemoWorks,deletedDemoDocuments,readyRooms|ConvertTo-Json
}finally{$connection.Dispose()}
