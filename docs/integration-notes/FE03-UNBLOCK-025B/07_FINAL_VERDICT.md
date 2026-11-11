# [HOTELOPS DEV2 FE06 → PROJECT LEADER / DEV1 FE03]
## FE03-UNBLOCK-025B — UC049 INTEGRATION DELIVERY

UC049_ROUTE_STATUS=PROPOSED
ROOM_BRANCH_CONTEXT=PROPOSED
INCIDENT_BACKEND_CONTRACT=PROPOSED
AUTHORIZATION_STATUS=PROPOSED
INTEGRATION_SHA_OR_PR=NOT_PUBLISHED
READY_FOR_FE03=NO

Implementation candidate đã có source FE06 và focused tests thực, không chỉ audit. Starting 6aee2a0e692a7814f41c81224709cddfbe1e2ec4; actual implementation commit NONE vì metadata Git không writable. Remote publication NOT_PUBLISHED; REMOTE_FRESHNESS=UNKNOWN. Các source pins là verified reference, không phải approval tích hợp.

Backend build Passed (0 warning), backend focused49 assertions Passed, frontend focused34 tests Passed + focused typecheck/bundle Passed. Solution test exit0 nhưng zero test; SQL/full-host/live integration NOT_RUN. Không claim production readiness.

Source/test files:

- `src/HotelOps.Api/Controllers/IncidentsController.cs`
- `src/HotelOps.Api/DTOs/Incidents/CreateIncidentRequest.cs`
- `src/HotelOps.Api/Services/Incidents/IIncidentService.cs`
- `src/HotelOps.Api/Services/Incidents/IncidentService.cs`
- `frontend/src/incidents/ReportIncidentRoute.tsx`
- `frontend/src/incidents/api.ts`
- `frontend/src/incidents/navigation.ts`
- `frontend/src/screens/maintenance/ReportIncidentScreen.tsx`
- `frontend/tests/incidents/uc049.test.cjs`
- `src/HotelOps.Api/Controllers/IncidentAccessFilterAttribute.cs`
- `src/HotelOps.Api/DTOs/Incidents/IncidentOptionsDto.cs`
- `src/HotelOps.Api/Services/Incidents/IncidentCreateAuthority.cs`
- `src/HotelOps.Api/Services/Incidents/IncidentPlacementGuard.cs`
- `src/HotelOps.Api/Services/Incidents/IncidentService.Options.cs`
- `tests/HotelOps.Incident.UnitTests/HotelOps.Incident.UnitTests.csproj`
- `tests/HotelOps.Incident.UnitTests/Program.cs`

Report files và hash source đầy đủ: CHANGED_FILES_SHA256.json. Các blocker:

- Router Owner + PL: Confirm target host and approve/mount exact route; production route remains disabled
- FE06 + FE03 + PL: Accept exact v1 href, missing/invalid context policy and current-state stale semantics
- Security/RBAC + PL: Accept optional legacy reporter compatibility and options eligibility candidate; verify actual deployed INCIDENT_CREATE catalog/grants for FE03 actor, including create-only actor; no grants made here
- FE06 integration owner: Run approved isolated SQL tests for persistence/rollback/locking, room/area/asset failures and real session/grant revocation; no shared SQL mutations in this task
- Developer/workspace administrator: Stage explicit source/test files and create local source-only commit outside sandbox; index.lock permission denied (git add exit 128); then PL chooses publication/PR target

Safety kiểm tra theo phạm vi thao tác và source diff:

```text
ORIGINAL_REPOSITORY_MODIFIED=false
FE03_SOURCE_CHANGED=false
FE04_SOURCE_CHANGED=false
FE05_SOURCE_CHANGED=false
SHARED_SQL_CHANGED=false
SQL_MUTATIONS_EXECUTED=false
ROOMBLOCK_POLICY_CHANGED=false
ROOMSTATUS_WRITER_CHANGED=false
NOTIFICATION_WORKER_ENABLED=false
MERGED=false
DEPLOYED=false
```

