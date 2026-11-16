param([switch]$Apply)
$ErrorActionPreference='Stop'
# The Project Leader confirmed this database is development-only. This installer
# admits that exact database identity, not any database matching a name pattern.
$server='meita\MSSQLSERVER01'
$database='HotelOpsDB'
$databaseGuid='BF470FF1-E41C-4E8A-A98D-51E6FAF96D3E'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$reviewed=@(
    @{Path='scripts/verification/CanonicalDatabase/InternalRequestViewIsolated.sql';Hash='146364460A55B7A1D98F8F81657AA21E5FA1261F62B7531E9D7373051B349AAB'},
    @{Path='scripts/verification/CanonicalDatabase/InternalRequestDecisionIsolated.sql';Hash='8FCE8FF21C288559DAEF840BE261C7859E284C777A69689E6A5D82D4D34C45E3'},
    @{Path='scripts/verification/CanonicalDatabase/InternalRequestAssignmentIsolated.sql';Hash='A91A3E79F5F17BF60F78B958D5B70E5950559902E3789D88509466D0F802446A'},
    @{Path='scripts/verification/CanonicalDatabase/InternalRequestCompletionIsolated.sql';Hash='42E05B3F4BB72816DAE91C30DC9BB3210760FBC635C53F2FC3456C31436C5C90'},
    @{Path='scripts/verification/CanonicalDatabase/InternalRequestProgressIsolated.sql';Hash='0D30923F32DEEC9C0F9BF25EE419E6F262771B50DEB48A27C2521A18F5E92FC7'},
    @{Path='scripts/verification/CanonicalDatabase/InternalRequestEvidenceIsolated.sql';Hash='6B6065F0EFBF8520100CD68DA988C45165E9A6A4EED8074EBA9DCDE9C9134C38'},
    @{Path='scripts/verification/CanonicalDatabase/InternalRequestIntakeIsolated.sql';Hash='FB92662E9E896F9BEB63BAE6CF844B2B5E660D7FD110E862AC808B8112E14AE9'},
    @{Path='scripts/verification/CanonicalDatabase/InternalRequestRouteIsolated.sql';Hash='15CFC7F3FD147610183C34CD54B30E3870A8888C07FA15D12CB8DEAB7FFE6214'},
    @{Path='scripts/verification/CanonicalDatabase/InternalRequestWithdrawalIsolated.sql';Hash='7081D8698256B493DF75F262BB3F8E99AACC162E76F65A89CEC862E1E512AE7D'},
    @{Path='scripts/verification/CanonicalDatabase/InternalRequestHandoverIsolated.sql';Hash='BDD1CF8FB77932EFD5606846782C5F558C131A09412ED37FBD51C04943822D42'},
    @{Path='scripts/verification/CanonicalDatabase/InternalRequestEscalationIsolated.sql';Hash='C078D70DE34272883B46A198C1844FF49CA4CFFB5CB4A375841699C2F187F344'}
)
$packages=@()
foreach($item in $reviewed){
    $path=Join-Path $workspace $item.Path
    if((Get-FileHash $path -Algorithm SHA256).Hash -ne $item.Hash){throw 'Reviewed migration changed; review before installing.'}
    $sql=[IO.File]::ReadAllText($path)
    # Only the historical disposable identity preamble is replaced. The pinned
    # packages retain every schema/data guard and their original transactions.
    $begin=$sql.IndexOf("IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))")
    $finish=$sql.IndexOf("'Exact owned disposable database required.',1;",$begin)
    if($begin -lt 0 -or $finish -lt 0){throw 'Reviewed identity boundary changed.'}
    $finish+="'Exact owned disposable database required.',1;".Length
    $guard=@"
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'$server'
 OR DB_NAME()<>N'$database'
 OR NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='$databaseGuid')
 THROW 51983,'Approved development identity required.',1;
"@
    $sql=$sql.Substring(0,$begin)+$guard+$sql.Substring($finish)
    $packages+=@{Path=$item.Path;Sql=$sql}
}
if(!$Apply){Write-Output 'Prepared reviewed Internal Request workflow installation. Use -Apply to install on the approved development database.';return}
$connection=[System.Data.SqlClient.SqlConnection]::new('Server=localhost\MSSQLSERVER01;Database=HotelOpsDB;Integrated Security=true;Encrypt=true;TrustServerCertificate=true')
$transaction=$null
try{
    $connection.Open()
    $command=$connection.CreateCommand()
    $command.CommandText=@'
SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))=@server
 AND DB_NAME()=@database AND EXISTS(SELECT 1 FROM sys.database_recovery_status
 WHERE database_id=DB_ID() AND database_guid=@guid) THEN 1 ELSE 0 END
'@
    [void]$command.Parameters.AddWithValue('@server',$server)
    [void]$command.Parameters.AddWithValue('@database',$database)
    [void]$command.Parameters.AddWithValue('@guid',[Guid]$databaseGuid)
    try{if([int]$command.ExecuteScalar() -ne 1){throw 'Development database identity mismatch.'}}finally{$command.Dispose()}
    $transaction=$connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    foreach($package in $packages){
        $command=$connection.CreateCommand();$command.Transaction=$transaction;$command.CommandTimeout=30
        $command.CommandText=$package.Sql
        try{[void]$command.ExecuteNonQuery()}finally{$command.Dispose()}
    }
    # Packages retain their inner transactions; this outer commit makes all reviewed
    # installations atomic. On error the connection rolls back the whole batch.
    $transaction.Commit()
    Write-Output 'APPLIED: Internal Request catalog and workflow storage on verified HotelOpsDB. No role grants or business rows seeded.'
}catch{
    if($transaction){try{$transaction.Rollback()}catch{}}
    throw
}finally{
    if($transaction){$transaction.Dispose()};$connection.Dispose()
}
