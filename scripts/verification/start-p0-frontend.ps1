$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$port=5174
if(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue){throw 'TEST frontend port already occupied; existing process preserved'}
$env:API_PROXY_TARGET='http://127.0.0.1:5774'
$out=Join-Path $root 'artifacts/p0-workflow'
$frontend=Join-Path $root 'frontend'
$vite=Join-Path $frontend 'node_modules/vite/bin/vite.js'
$process=Start-Process node -ArgumentList @($vite,'--host','127.0.0.1','--port',"$port",'--strictPort') -WorkingDirectory $frontend -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out 'frontend-5174.log') -RedirectStandardError (Join-Path $out 'frontend-5174.err.log')
@{pid=$process.Id;port=$port;proxy=$env:API_PROXY_TARGET;ownedBy='p0-integration'}|ConvertTo-Json|Set-Content (Join-Path $out 'frontend-5174-process.json')
Write-Output "P0 TEST frontend started: PID $($process.Id), port $port"
