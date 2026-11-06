# Route inventory / composition review

| Method | Route (relative to host) | Authority | Result |
| --- | --- | --- | --- |
| GET | /api/branches/{branchId:long}/cleaning-task-options?roomId=... | existing CLEANING_ASSIGN branch gate | 200 options DTO; 400/401/403/404/409/503 |
| POST | /api/branches/{branchId:long}/cleaning-tasks | existing CLEANING_ASSIGN branch gate + existing CSRF middleware | 201 CreatedCleaningTask DTO + ETag; 400/401/403/404/409/503 |
| GET | /api/branches/{branchId:long}/cleaning-tasks/{id:long}?assignmentId=... | existing CLEANING_VIEW OWN/BRANCH/GLOBAL via exact source evaluator | 200 AssignedCleaningHeader DTO; 400/401/403/404/503 |
| POST (existing, changed behavior) | /api/branches/{branchId:long}/cleaning-tasks/{id:long}/assign | Cookie/current-session middleware; syntactic validation then dependency unavailable | 503, no mutation until accepted A01/A02 integration |

No other controller recovery marker changed. CleaningMonitor GET list/detail, planning GET, progress GET/PUT, and inspection/rework routes retained. New literal/segment shapes inspected against active controllers: no duplicate method+route ownership found. Existing scoped RoomTypeCreationService registration is reused via partial class; no DI/authentication/Program/DbContext/config edits. Action discovery regression still confirms inactive recovery controllers remain inactive. Real route execution is recorded in LIVE_RESULTS.json. Full solution build verifies composition; no claim an unused recovery action was enabled.

All new controller actions use existing cookie scheme, HTTPS-only handling and no-store DTO responses. Source header query assignmentId changes source authorization; it does not grant access to monitor. Assigned history beyond ACTIVE/unended OWN is deliberately not certified. Public Assign fails closed before its historical core; this is dependency behavior rather than a substitute authorization policy. Shared accepted stage and full actor/resource gates must precede future activation.
