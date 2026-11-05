# HTTPS, sessions and reusable commands

Topology: Edge https://localhost:5173 → HTTPS gateway → HTTP loopback Vite127.0.0.1:5189 → strict HTTPS APIlocalhost:7443 → approved SQL trial. Gateway uses the existing Windows-store private-key handle and exports public PEM only. Vite trusts public PEM through process-scoped NODE_EXTRA_CA_CERTS; browser TLS errors are not ignored. Gateway WebSocket/HMR forwarding is not implemented; use manual reload.

Launcher overrides child-process environment only; shared appsettings/User Secrets/auth remain unchanged. Cookie session, SQL session validation, CSRF and same-origin /api stay intact.

Harness performs actual /api/auth/csrf, /login and /session calls, then uses shared browser cookies for React screens. This is **API-assisted browser login**, not acceptance of DEV3 frontend sign-in/protected-navigation/session UX.

From repository root after existing setup:
```powershell
dotnet build scripts/verification/FS004/FS004.Verification.csproj
powershell -NoProfile -File scripts/verification/FS004/Start-LiveEnvironment.ps1 -StartApprovedTrial
```
From frontend, using the same approved Windows account:
```powershell
$env:FS004_LIVE_ENABLED='APPROVED_FS004_LOCAL_TRIAL'
$env:NODE_EXTRA_CA_CERTS=(Resolve-Path '../scripts/verification/FS004/.private/localhost-public.pem').Path
node node_modules/@playwright/test/cli.js test --config playwright.live.config.ts
Remove-Item Env:FS004_LIVE_ENABLED
Remove-Item Env:NODE_EXTRA_CA_CERTS
```
Finally from repository root:
```powershell
powershell -NoProfile -File scripts/verification/FS004/Stop-LiveEnvironment.ps1
```

Start defaults to plan-only and refuses occupied ports/existing receipts. Stop validates PID/start-time identity; it never kills by port. Inspect stale receipts rather than deleting blindly. Missing certificate/DPAPI identity/fixtures fail closed. Never invoke credentials-ipc manually: its decrypted child stdout is private IPC captured only in harness memory.
