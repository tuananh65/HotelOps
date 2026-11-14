param([int]$AllowedProcessId=0,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$rows=@();$status='FAIL_UNINSPECTABLE'
try{
    $processes=Get-CimInstance Win32_Process -ErrorAction Stop
    foreach($process in $processes){
        if($process.ProcessId -in @($PID,$AllowedProcessId)){continue}
        $candidate=$process.Name -match '^(dotnet|HotelOps.Api|Dev2AllActions|powershell|pwsh)(\.exe)?$'
        if(!$candidate){continue}
        $readable=![string]::IsNullOrEmpty($process.CommandLine)
        $match=$process.Name -match '^(HotelOps.Api|Dev2AllActions)(\.exe)?$' -or ($process.CommandLine -match 'HotelOps\.Api(\.dll|\.csproj)|Dev2AllActions(\.dll|\.csproj)|RUN_ALL61_WINDOWS_HOST\.ps1')
        if($match -or !$readable){$rows+=@{pid=$process.ProcessId;parentPid=$process.ParentProcessId;name=$process.Name;commandReadable=$readable;trialProcessMatch=[bool]$match}}
    }
    $status=if($rows.Count -eq 0){'PASS_NO_ACTIVE_TRIAL_PROCESSES'}else{'BLOCKED_ACTIVE_OR_UNINSPECTABLE_PROCESS'}
}catch{$status='BLOCKED_PROCESS_INSPECTION_UNAVAILABLE'}
$value=@{status=$status;utc=[DateTime]::UtcNow.ToString('o');processes=$rows;commandLinesRecorded=$false;processesStopped=$false}
[IO.File]::WriteAllText($OutputPath,(ConvertTo-Json -Depth 6 -InputObject $value),[Text.UTF8Encoding]::new($false))
if($status -ne 'PASS_NO_ACTIVE_TRIAL_PROCESSES'){exit 1}
exit 0
