param([switch]$CreateIfMissing)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Integration-Environment.ps1')
$config=Get-IntegrationConfig
$target='HotelOps_Authorization_Test'
$expectedServer=$config.ExpectedServer
$release=Join-Path $PSScriptRoot '../../database/release'
$receiptPath=Join-Path $PSScriptRoot 'environment.json'
$master=Open-IntegrationDb 'master'
try {
    if((Identity $master).server -cne $expectedServer){throw 'Unexpected server'}
    $engine=(Query $master "SELECT CONVERT(int,SERVERPROPERTY('ProductMajorVersion')) major,CONVERT(nvarchar(50),SERVERPROPERTY('ProductVersion')) version").Tables[0].Rows[0]
    if($engine.major -lt 16){throw 'SQL Server 2022+ required by release package'}
    if(Test-Path $receiptPath){
        $saved=Get-Content -Raw -Encoding UTF8 $receiptPath|ConvertFrom-Json
        $test=Open-IntegrationDb $target
        try {
            $id=Identity $test
            foreach($key in @('database','databaseId','server','createStamp')){if($id[$key] -cne $saved.identity.$key){throw 'Existing test database identity changed'}}
            if((Query $test "SELECT CAST(value AS nvarchar(100)) v FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.Authorization.Integration'").Tables[0].Rows[0].v -cne 'Task06/v1'){throw 'Provenance marker mismatch'}
            foreach($f in $saved.releaseHashes.PSObject.Properties){if((Get-FileHash (Join-Path $release $f.Name)).Hash -cne $f.Value){throw 'Release changed; no automatic upgrade/reseed'}}
            Write-Output 'PASS: existing owned test database verified; no schema reset or reseed'
        } finally {$test.Dispose()}
        exit 0
    }
    if((Query $master "SELECT DB_ID(N'$target') id").Tables[0].Rows[0].id -isnot [DBNull]){throw 'Test database already exists without this package receipt; inspect provenance manually, no overwrite'}
    if(Test-Path (Join-Path $PSScriptRoot 'provisioning-checkpoint.json')){throw 'Previous provisioning checkpoint exists; inspect partial state, no replay'}
    if(!$CreateIfMissing){throw 'Database absent; review provisioning rights and explicitly use -CreateIfMissing. No main database fallback.'}
    $masterGuard="IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$expectedServer' OR DB_NAME()<>N'master' THROW 54060,'Wrong provisioning server/context',1;`r`n"
    $schema=Get-Content -Raw -Encoding UTF8 (Join-Path $release '01_HotelOps_Full_Schema.sql')
    $schema=$schema.Replace('HotelOpsDB',$target)
    $batches=@([regex]::Split($schema,'(?im)^GO\s*\r?$')|Where-Object {$_.Trim()})
    if($batches[2].Trim() -cne "CREATE DATABASE [$target];" -or !$batches[3].Contains("USE [$target]")){throw 'Reviewed installer preamble differs'}
    # The sole master-context write is the explicitly approved CREATE of this absent database.
    Execute $master ($masterGuard+"IF DB_ID(N'$target') IS NOT NULL THROW 54061,'No overwrite',1;`r`n"+$batches[2])
    $test=Open-IntegrationDb $target
    try {
        $id=Identity $test
        Save-Json ([ordered]@{identity=$id;status='PARTIAL_DO_NOT_REPLAY'}) (Join-Path $PSScriptRoot 'provisioning-checkpoint.json')
        $guard="IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$expectedServer' OR DB_NAME()<>N'$target' OR DB_ID()<>$($id.databaseId) THROW 54062,'Wrong test database; write denied',1;`r`n"
        foreach($batch in $batches[4..($batches.Count-1)]){Execute $test ($guard+$batch)}
        $seed=(Get-Content -Raw -Encoding UTF8 (Join-Path $release '03_HotelOps_RBAC_Catalog_Seed.sql')).Replace('HotelOpsDB',$target)
        foreach($batch in [regex]::Split($seed,'(?im)^GO\s*\r?$')){if($batch.Trim()){Execute $test ($guard+$batch)}}
        $n=(Query $test 'SELECT (SELECT COUNT(*) FROM dbo.roles) r,(SELECT COUNT(*) FROM dbo.permissions) p,(SELECT COUNT(*) FROM dbo.role_permissions) g').Tables[0].Rows[0]
        if($n.r -ne 12 -or $n.p -ne 62 -or $n.g -ne 140){throw 'RBAC catalog counts differ'}
        Execute $test ($guard+"EXEC sys.sp_addextendedproperty @name=N'HotelOps.Authorization.Integration',@value=N'Task06/v1';")
        $hashes=[ordered]@{};foreach($name in @('01_HotelOps_Full_Schema.sql','03_HotelOps_RBAC_Catalog_Seed.sql')){$hashes[$name]=(Get-FileHash (Join-Path $release $name)).Hash}
        Save-Json ([ordered]@{identity=$id;status='SCHEMA_READY';releaseHashes=$hashes;schemaBatches=$batches.Count-4;seed='canonical 03, no Upgrade';sqlVersion=[string]$engine.version;createdUtc=[DateTime]::UtcNow.ToString('o')}) $receiptPath
        Write-Output 'PASS: new isolated Full Schema + canonical RBAC Seed; no Upgrade, DROP or RESTORE'
    } finally {$test.Dispose()}
} finally {$master.Dispose()}
