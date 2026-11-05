# Environment setup

Backend global.json: SDK10.0.402/latestFeature; net10.0; EF Core SQLServer/Design10.0.8. One ASP.NET Core API, Database First. Keep generated legacy entities/mappings; no migrations. Configure ConnectionStrings:HotelOpsDb using User Secrets, never copy a host from receipts. WorkTime model source does not prove shared schema deployment.

From repository root:

```powershell
dotnet --info
dotnet restore HotelOps.slnx
dotnet build HotelOps.slnx
dotnet test HotelOps.slnx
dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj
node docs/fullstack-integration/verify-di.cjs
cd frontend
npm ci --no-audit --no-fund
npm run build
npm test
npm run dev -- --host localhost --port 5173
```

For live integration only after separate trial approval: copy frontend/.env.example to .env.local; set API_PROXY_TARGET and DEV_HTTPS_CERT/KEY to local PEM files. Trust backend CA using NODE_EXTRA_CA_CERTS if required; do not use secure:false. Keep certificates/private keys outside Git.

```powershell
dotnet user-secrets set 'ConnectionStrings:HotelOpsDb' '<APPROVED_LOCAL_TRIAL_CONNECTION>' --project src/HotelOps.Api
dotnet run --project src/HotelOps.Api --no-launch-profile -- --environment Development --urls https://localhost:7443
```

The run command requires trial authorization: StartupSchemaValidator connects before starting the listener. FS-001 did not run API/SQL and did not use HotelOpsDB or Task82-86 trials. Check ports; never kill another worktree's service. Windows Development has the existing key protection contract; other deployments need approved persistent encrypted Data Protection keys, not a guard bypass. Prototype HTTP does not verify Secure-cookie integration. Legacy Playwright configuration needs a portable browser path and isolated port before E2E.
