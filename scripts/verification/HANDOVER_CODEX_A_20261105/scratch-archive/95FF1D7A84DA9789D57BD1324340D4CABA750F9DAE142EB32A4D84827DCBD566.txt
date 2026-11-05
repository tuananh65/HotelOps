# Standalone helpers. Never read main database configuration or deployment state.
$integrationTarget='HotelOps_Authorization_Test'
function Get-IntegrationConfig {
    $path=Join-Path $env:APPDATA 'Microsoft/UserSecrets/HotelOps.Authorization.Integration.Task06/secrets.json'
    $local=$null;if(Test-Path -LiteralPath $path){$local=Get-Content -Raw -LiteralPath $path|ConvertFrom-Json}
    $value=$env:ConnectionStrings__Integration
    if(!$value -and $local){$value=$local.'ConnectionStrings:Integration';if(!$value){$value=$local.ConnectionStrings.Integration}}
    if(!$value){throw 'INTEGRATION_SETUP_REQUIRED: configure ConnectionStrings__Integration or separate Integration User Secrets; no main-DB fallback.'}
    $settings=New-Object System.Data.SqlClient.SqlConnectionStringBuilder $value
    if($settings.InitialCatalog -cne $integrationTarget -or $settings.AttachDBFilename){throw 'Refusing connection outside HotelOps_Authorization_Test'}
    $expected=$env:HotelOpsIntegration__ExpectedServer
    if(!$expected -and $local){$expected=$local.'Integration:ExpectedServer';if(!$expected){$expected=$local.Integration.ExpectedServer}}
    if(!$expected){$expected=$settings.DataSource}
    if($expected -notmatch '^[A-Za-z0-9_.-]+(\\[A-Za-z0-9_.-]+)?$'){throw 'Use canonical SERVER or SERVER\INSTANCE for expected server'}
    [pscustomobject]@{ConnectionString=$settings.ConnectionString;ExpectedServer=$expected}
}
function Open-IntegrationDb([ValidateSet('master','HotelOps_Authorization_Test')][string]$Database) {
    $config=Get-IntegrationConfig
    $settings=New-Object System.Data.SqlClient.SqlConnectionStringBuilder $config.ConnectionString
    $settings['Initial Catalog']=$Database;$settings['Application Name']='HotelOps isolated integration setup'
    $connection=New-Object System.Data.SqlClient.SqlConnection $settings.ConnectionString
    try {$connection.Open();return $connection} catch {$connection.Dispose();throw 'Integration SQL connection failed; check local settings (provider details suppressed).'}
}
function Query($Connection,[string]$Sql) {
    $command=$Connection.CreateCommand();$command.CommandText=$Sql;$command.CommandTimeout=60
    $adapter=New-Object System.Data.SqlClient.SqlDataAdapter $command;$data=New-Object System.Data.DataSet
    try {[void]$adapter.Fill($data);return ,$data} finally {$adapter.Dispose();$command.Dispose()}
}
function Identity($Connection) {
    $r=(Query $Connection "SELECT DB_NAME() d,DB_ID() i,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) s,CONVERT(varchar(23),create_date,121) c FROM sys.databases WHERE database_id=DB_ID()").Tables[0].Rows[0]
    [ordered]@{database=[string]$r.d;databaseId=[int]$r.i;server=[string]$r.s;createStamp=[string]$r.c}
}
function Execute($Connection,[string]$Sql) {
    $command=$Connection.CreateCommand();$command.CommandText=$Sql;$command.CommandTimeout=120
    try {[void]$command.ExecuteNonQuery()} catch {throw 'Integration setup batch failed; preserve partial database for administrator review (provider details suppressed).'} finally {$command.Dispose()}
}
function Save-Json($Value,[string]$Path) {$Value|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $Path -Encoding UTF8}
function Assert-IntegrationIdentity($Connection,$Expected) {
    $actual=Identity $Connection
    foreach($key in @('database','databaseId','server','createStamp')){if($actual[$key] -cne $Expected.$key){throw 'Integration identity changed; no reset/reseed permitted'}}
}
