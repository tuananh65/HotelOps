param([string]$Database='HotelOpsCompactDB',[string]$RestoreAs,[string]$Server)
$ErrorActionPreference='Stop'
if($Database -notmatch '^HotelOpsCompact[A-Za-z0-9_]*$' -or ($RestoreAs -and $RestoreAs -notmatch '^HotelOpsCompactWorkdayTest[A-Za-z0-9_]+$')){throw 'Invalid database scope.'}
$config=Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json') -Raw | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.'ConnectionStrings:HotelOpsDb')
if($Server){$builder['Data Source']=$Server}
$builder['Initial Catalog']='master'
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$connection.Open()
try {
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=180
 $cmd.CommandText="SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'))"
 $folder=[string]$cmd.ExecuteScalar();if(-not $folder){throw 'No SQL backup directory.'}
 $backup=Join-Path $folder ($Database+'_before_workday_'+(Get-Date -Format 'yyyyMMdd_HHmmssfff')+'.bak')
 $cmd.CommandText="BACKUP DATABASE [$Database] TO DISK=@file WITH COPY_ONLY,CHECKSUM"
 [void]$cmd.Parameters.AddWithValue('@file',$backup);[void]$cmd.ExecuteNonQuery()
 $cmd.CommandText='RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM';[void]$cmd.ExecuteNonQuery()
 if($RestoreAs){
  $cmd.CommandText='RESTORE FILELISTONLY FROM DISK=@file';$reader=$cmd.ExecuteReader();$files=@()
  while($reader.Read()){$files+=@{name=$reader['LogicalName'];type=$reader['Type']}};$reader.Close()
  $cmd.Parameters.Clear();$cmd.CommandText="SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath'))";$data=[string]$cmd.ExecuteScalar()
  $moves=@();$i=0
  foreach($file in $files){$i++;$suffix=if($file.type -eq 'L'){'.ldf'}else{'.mdf'};$path=Join-Path $data ($RestoreAs+'_'+$i+$suffix);$moves+="MOVE N'"+$file.name.Replace("'","''")+"' TO N'"+$path.Replace("'","''")+"'"}
  $cmd.CommandText="IF DB_ID(@target) IS NOT NULL THROW 51400,'Test target already exists; restore refused.',1; RESTORE DATABASE [$RestoreAs] FROM DISK=@file WITH "+($moves -join ',')+',CHECKSUM'
  [void]$cmd.Parameters.AddWithValue('@file',$backup);[void]$cmd.Parameters.AddWithValue('@target',$RestoreAs);[void]$cmd.ExecuteNonQuery()
 }
 $out=Join-Path $PSScriptRoot '../../../artifacts/workday';[void](New-Item -ItemType Directory -Force $out)
 @{database=$Database;backup=$backup;verified=$true;restoredAs=$RestoreAs;at=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $out ('backup-'+$Database+'.json')) -Encoding UTF8
 Write-Output "PASS: backup verified; source=$Database; isolated=$RestoreAs"
}finally{$connection.Dispose()}
