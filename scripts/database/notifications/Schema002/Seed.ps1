#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Manifest,[Parameter(Mandatory=$true)][string]$CredentialHelper,
 [Parameter(Mandatory=$true)][string]$SecretDirectory)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Safety.ps1')
$m=Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
$null=Assert-NotificationTrialManifest $m ([Security.Principal.WindowsIdentity]::GetCurrent().Name)
$null=New-Item -ItemType Directory -Path $SecretDirectory
# DPAPI credentials stay outside Git; lock directory to this user and SYSTEM.
$acl=Get-Acl $SecretDirectory;$acl.SetAccessRuleProtection($true,$false)
foreach($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User.Value,'S-1-5-18')){
 $identity=New-Object Security.Principal.SecurityIdentifier $sid
 $rule=New-Object Security.AccessControl.FileSystemAccessRule($identity,'FullControl','ContainerInherit,ObjectInherit','None','Allow')
 $acl.AddAccessRule($rule)
}
Set-Acl -LiteralPath $SecretDirectory -AclObject $acl
$hashes=@{}
foreach($name in @('Supervisor','Staff')){
 $bytes=New-Object byte[] 32;$rng=[Security.Cryptography.RandomNumberGenerator]::Create()
 try{$rng.GetBytes($bytes)}finally{$rng.Dispose()}
 $plain='Trial!aA1-'+[Convert]::ToBase64String($bytes)
 $secure=ConvertTo-SecureString $plain -AsPlainText -Force
 $username=if($name -eq 'Supervisor'){'envnotif001.supervisor'}else{'envnotif001.staff'}
 $credential=[Management.Automation.PSCredential]::new($username,$secure)
 $credential | Export-Clixml -LiteralPath (Join-Path $SecretDirectory ($name+'.credential.clixml'))
 $psi=[Diagnostics.ProcessStartInfo]::new()
 $psi.FileName='dotnet';$psi.Arguments='"'+$CredentialHelper+'"';$psi.UseShellExecute=$false
 $psi.RedirectStandardInput=$true;$psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true;$psi.CreateNoWindow=$true
 $p=[Diagnostics.Process]::Start($psi)
 try{
  $p.StandardInput.WriteLine($plain);$p.StandardInput.Close();$plain=$null
  $hash=$p.StandardOutput.ReadToEnd().Trim();$null=$p.StandardError.ReadToEnd();$p.WaitForExit()
  if($p.ExitCode -ne 0 -or !$hash.StartsWith('AQAAAA') -or $hash.Length -gt 255){throw 'Existing credential helper failed; output redacted.'}
  $hashes[$name]=$hash
 }finally{$p.Dispose();$secure.Dispose();[Array]::Clear($bytes,0,$bytes.Length)}
}
$cs=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$cs['Data Source']='tcp:DESKTOP-JI8U50T,1433';$cs['Initial Catalog']=$m.database
$cs['Integrated Security']=$true;$cs['Encrypt']=$true;$cs['TrustServerCertificate']=$false;$cs['Pooling']=$false
$c=New-Object System.Data.SqlClient.SqlConnection $cs.ConnectionString
try{
 $c.OpenAsync().GetAwaiter().GetResult() | Out-Null
 $cmd=$c.CreateCommand();$cmd.CommandTimeout=60
 $cmd.CommandText="IF (SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID())<>@guid THROW 51962,'Database GUID mismatch',1;"+[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Seed.sql'))
 $null=$cmd.Parameters.AddWithValue('@guid',[guid]$m.database_guid)
 $null=$cmd.Parameters.AddWithValue('@SupervisorHash',$hashes.Supervisor)
 $null=$cmd.Parameters.AddWithValue('@StaffHash',$hashes.Staff)
 $null=$cmd.ExecuteNonQuery();$cmd.Dispose()
 'SYNTHETIC_MASTER_PERSONNEL_SEED_PASS; no tasks/assignments/inspections/notifications written.'
}finally{$c.Dispose();$hashes.Clear()}

