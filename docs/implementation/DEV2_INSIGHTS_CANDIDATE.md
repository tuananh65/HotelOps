# DEV2 Insights read-only candidate

Dashboard aggregation, seven report previews and redacted persisted-audit queries use existing DASHBOARD_VIEW, REPORT_VIEW and AUDIT_VIEW permissions and branch-scoped authorization. No grant, schema or producer/worker change is included. Unknown KPI inputs remain explicitly unavailable, report export remains blocked, and audit access is limited/redacted. Classification: CANDIDATE_WITH_OPEN_CONTRACTS, not full UC131–137 acceptance.

Baseline: 6aee2a0e692a7814f41c81224709cddfbe1e2ec4. The only inherited dirty dependency promoted with this module is MaintenanceWorkAccessReader.cs, SHA256 fc2d09e8befd5828181e94d55adafe541830885e0640cb7f0542176dbf121db2; it projects the authoritative source branch and withholds ambiguous sources. Program.cs adds one Insights DI registration. Other Acceptance05/Notification/Core modules are not imported.

Build: dotnet build HotelOps.slnx
Functional checks: dotnet run --project tests/HotelOps.Insights.NoSqlChecks/HotelOps.Insights.NoSqlChecks.csproj
Solution dotnet test discovers no functional test projects; do not count it as business acceptance.

Open contracts include mandatory KPI provenance, report field/export+audit authority, current-cycle SLA/Work-Time/RoomBlock inputs and final UC acceptance. No production activation or develop/main merge is authorized by this candidate. Full UI composition and Notification have separate provenance and evidence; this Backend scope makes no UI or UC029 claim.
