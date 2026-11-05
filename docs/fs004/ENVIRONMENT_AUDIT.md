# Environment audit

Read-only discovery preceded setup. SDK10.0.402, ASP.NET Core runtime10.0.13, Node24.21.0/npm12.2.0 are available. MSSQL$SQLEXPRESS is running; SQL16.0.1000.6 Express64. SQLBrowser is disabled/stopped. sqlcmd and LocalDB tooling exist; this trial uses SQL Express only. No service or SQL privilege changes.

Existing API User Secrets were present but their database was unclassified: not used or modified. No frontend .env.local existed. Environment inspection emitted names/status only. Launch settings HTTPS7019 and proxy default7443 differ; launcher aligns trial API7443 via child-process overrides.

Restricted execution initially reported no trusted cert/SQL access. Read-only checks under the actual Windows account found an already trusted, valid localhost development certificate. SQL encrypted connection without a trust exception failed; user explicitly approved TrustServerCertificate only for local SQL Express. No certificate creation/private-key export/trust changes.

Historical Codex A runners pin another machine and were not executed blindly. Their tracked sources/guards informed the 18-file manifest. Approved target was absent before creation; other databases remain unclassified.

Read-only preflight:
```powershell
powershell -NoProfile -File scripts/verification/FS004/Invoke-EnvironmentPreflight.ps1 -SqlMetadata -AllowLocalSqlCertificateException -TrialDatabase HotelOps_Canonical_20261105_180000_f040001_Fresh
```
Preflight does not start the API or write SQL. Restricted-account visibility may differ; do not automatically repair trust or switch databases.
